using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Handlers;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Application.Helpers;

namespace RikiPath.Application.Services
{
    public class ContentApprovalService(
        IUnitOfWork unitOfWork,
        IExcelParser excelParser,
        IClaimService claimService,
        IEnumerable<ISheetImportHandler> sheetHandlers,
        IEnumerable<IApprovalDiffBuilder> diffBuilders,
        IContentApprovalNotifierService notifier) : IContentApprovalService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        // Theo dõi các cặp (Lesson, Kanji/Grammar/Vocabulary) đã được "stage" (AddAsync) trong CHÍNH request
        // HTTP hiện tại nhưng chưa SaveChanges. Service này là scoped theo request (dùng chung DbContext qua
        // IUnitOfWork), nên field instance này tự reset cho mỗi request - không rò rỉ giữa các lần gọi khác nhau.
        // Lý do cần: nếu 2 ContentApprovalRequest khác nhau (VD: 2 bản ghi trùng đang chờ duyệt, hoặc Modified
        // cùng trỏ 1 TargetEntityId) cùng được duyệt trong 1 lần cascade từ Lesson, EF identity map có thể trả
        // về CÙNG 1 instance Kanji/Vocabulary/Grammar cho cả 2 - nếu không chặn ở đây, lần add LessonKanji/...
        // thứ 2 sẽ bị EF từ chối ngay vì trùng khóa, trước khi kịp SaveChanges.
        private readonly HashSet<(object Lesson, object Entity)> _stagedLessonLinks = new();

        public async Task<ApiResponse<List<SheetImportResponse>>> PreviewImportAsync(
            IFormFile file, CancellationToken cancellationToken)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return ApiResponse<List<SheetImportResponse>>.Fail("File Excel không hợp lệ hoặc rỗng.");

                using var stream = file.OpenReadStream();
                var excelData = await excelParser.ParseSheetsAsync(stream, cancellationToken);

                var handlerDict = sheetHandlers.ToDictionary(h => h.SheetName, StringComparer.OrdinalIgnoreCase);
                var responses = new List<SheetImportResponse>();

                foreach (var sheet in excelData.Sheets)
                {
                    if (handlerDict.TryGetValue(sheet.SheetName, out var handler))
                    {
                        var excelRows = sheet.Rows.Select(r => new ExcelRow
                        {
                            RowNumber = r.RowNumber,
                            Fields = r.Fields.ToDictionary(
                         kvp => ExcelSheetReader.Normalize(kvp.Key),
                         kvp => kvp.Value)
                        }).ToList();

                        var diffResponse = await handler.BuildDiffAsync(excelRows, cancellationToken);
                        responses.Add(diffResponse);
                    }
                }

                return ApiResponse<List<SheetImportResponse>>.Success(responses);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<SheetImportResponse>>.Fail($"Không thể xem trước dữ liệu import: {ex.Message}");
            }
        }

        public async Task<ApiResponse<int>> SubmitImportRequestsAsync(
            List<SheetImportResponse> sheetDiffs, CancellationToken cancellationToken)
        {
            try
            {
                var authorId = claimService.GetUserClaim().Id;
                var createdCount = 0;

                foreach (var sheet in sheetDiffs)
                {
                    foreach (var row in sheet.Rows)
                    {
                        if (row.Action is ImportRowAction.Error or ImportRowAction.Unchanged)
                            continue;

                        var approvalRequest = new ContentApprovalRequest
                        {
                            EntityType = sheet.EntityType,
                            Action = row.Action,
                            RowKey = row.RowKey,
                            TargetEntityId = row.TargetEntityId,
                            PayloadJson = row.PayloadJson ?? "{}",
                            DiffSummaryJson = JsonSerializer.Serialize(row.ChangedFields),
                            Status = ContentApprovalStatus.PendingReview,
                            SubmittedByUserId = authorId,
                            SubmittedAt = DateTime.UtcNow
                        };

                        await unitOfWork.ContentApprovalRequests.AddAsync(approvalRequest, cancellationToken);
                        createdCount++;
                    }
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<int>.Success(createdCount);
            }
            catch (Exception ex)
            {
                return ApiResponse<int>.Fail($"Lỗi khi gửi yêu cầu phê duyệt: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<PendingApprovalItemResponse>>> GetPendingApprovalsAsync(
            ImportEntityType? entityType, CancellationToken cancellationToken)
        {
            try
            {
                var query = unitOfWork.ContentApprovalRequests.Query()
                    .Include(r => r.SubmittedBy)
                    .Where(r => !r.IsDeleted && (r.Status == ContentApprovalStatus.Draft || r.Status == ContentApprovalStatus.PendingReview));

                if (entityType.HasValue)
                    query = query.Where(r => r.EntityType == entityType.Value);

                var requests = await query.OrderByDescending(r => r.SubmittedAt).ToListAsync(cancellationToken);
                if (requests.Count == 0)
                    return ApiResponse<List<PendingApprovalItemResponse>>.Success([]);

                var builderDict = diffBuilders.ToDictionary(b => b.EntityType);
                var responseList = new List<PendingApprovalItemResponse>();

                var groupedRequests = requests.GroupBy(r => r.EntityType);

                foreach (var group in groupedRequests)
                {
                    Dictionary<int, ApprovalDiffResult> diffResults;

                    if (builderDict.TryGetValue(group.Key, out var builder))
                    {
                        diffResults = await builder.BuildAsync(group.ToList(), cancellationToken);
                    }
                    else
                    {
                        diffResults = group.ToDictionary(
                            r => r.Id,
                            r => GenericApprovalDiff.Build(r, ApprovalSummary.Parse(r.DiffSummaryJson)));
                    }

                    foreach (var r in group)
                    {
                        var diff = diffResults.GetValueOrDefault(r.Id) ?? new ApprovalDiffResult();

                        responseList.Add(new PendingApprovalItemResponse
                        {
                            RequestId = r.Id,
                            EntityType = r.EntityType,
                            Action = r.Action,
                            RowKey = r.RowKey,
                            TargetEntityId = r.TargetEntityId,
                            SubmittedByName = $"{r.SubmittedBy?.FirstName} {r.SubmittedBy?.LastName}".Trim(),
                            SubmittedAt = r.SubmittedAt,
                            Fields = diff.Fields,
                            Warnings = diff.Warnings,
                            TargetMissing = diff.TargetMissing
                        });
                    }
                }

                await AttachLessonRelatedContentAsync(requests, responseList, cancellationToken);
                return ApiResponse<List<PendingApprovalItemResponse>>.Success(responseList);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<PendingApprovalItemResponse>>.Fail($"Không thể lấy danh sách chờ duyệt: {ex.Message}");
            }
        }

        private async Task AttachLessonRelatedContentAsync(
            IReadOnlyList<ContentApprovalRequest> requests,
            List<PendingApprovalItemResponse> responses,
            CancellationToken cancellationToken)
        {
            var lessons = new Dictionary<int, LessonImportPayloadResponse>();
            foreach (var request in requests.Where(r => r.EntityType == ImportEntityType.Lesson))
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<LessonImportPayloadResponse>(request.PayloadJson, JsonOptions);
                    if (payload is not null) lessons[request.Id] = payload;
                }
                catch (JsonException) { }
            }
            if (lessons.Count == 0) return;

            var lessonKeys = lessons.Values.Select(p => LessonKey(p.Title, p.CertificateLevelCode)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var relatedRequests = await unitOfWork.ContentApprovalRequests.Query()
                .Where(r => !r.IsDeleted && (r.Status == ContentApprovalStatus.Approved
                    || r.Status == ContentApprovalStatus.Draft || r.Status == ContentApprovalStatus.PendingReview)
                    && (r.EntityType == ImportEntityType.Kanji || r.EntityType == ImportEntityType.Grammar
                        || r.EntityType == ImportEntityType.Vocabulary || r.EntityType == ImportEntityType.PracticeExercise))
                .ToListAsync(cancellationToken);
            var related = new List<(ContentApprovalRequest Request, string LessonKey)>();
            foreach (var request in relatedRequests)
            {
                try
                {
                    using var doc = JsonDocument.Parse(request.PayloadJson);
                    if (!doc.RootElement.TryGetProperty("LessonTitle", out var titleNode) || titleNode.ValueKind != JsonValueKind.String) continue;
                    var title = titleNode.GetString();
                    var code = doc.RootElement.TryGetProperty("CertificateLevelCode", out var codeNode) ? codeNode.GetString() : null;
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    if (string.IsNullOrWhiteSpace(code)
                        && doc.RootElement.TryGetProperty("CertificateLevelId", out var relatedLevelNode)
                        && relatedLevelNode.TryGetInt32(out var relatedLevelId))
                        code = lessons.Values.FirstOrDefault(x => x.CertificateLevelId == relatedLevelId && x.Title == title)?.CertificateLevelCode;
                    if (string.IsNullOrWhiteSpace(code)) continue;
                    var key = LessonKey(title, code);
                    if (lessonKeys.Contains(key)) related.Add((request, key));
                }
                catch (JsonException) { }
            }

            var builderDict = diffBuilders.ToDictionary(b => b.EntityType);
            var relatedDiffs = new Dictionary<int, ApprovalDiffResult>();
            foreach (var group in related.GroupBy(x => x.Request.EntityType))
            {
                if (builderDict.TryGetValue(group.Key, out var builder))
                {
                    var built = await builder.BuildAsync(group.Select(x => x.Request).ToList(), cancellationToken);
                    foreach (var pair in built) relatedDiffs[pair.Key] = pair.Value;
                }
                else
                {
                    foreach (var item in group)
                        relatedDiffs[item.Request.Id] = GenericApprovalDiff.Build(item.Request, ApprovalSummary.Parse(item.Request.DiffSummaryJson));
                }
            }

            // Include current DB relations as already approved items, plus in-flight import requests.
            var lessonRequestByKey = lessons.ToDictionary(x => x.Key, x => LessonKey(x.Value.Title, x.Value.CertificateLevelCode));
            foreach (var response in responses.Where(x => x.EntityType == ImportEntityType.Lesson))
            {
                if (!lessonRequestByKey.TryGetValue(response.RequestId, out var key)) continue;
                response.RelatedLessonContent = related.Where(x => x.LessonKey == key).Select(x =>
                {
                    var diff = relatedDiffs.GetValueOrDefault(x.Request.Id) ?? new ApprovalDiffResult();
                    return new PendingLessonContentResponse
                    {
                        RequestId = x.Request.Id,
                        EntityType = x.Request.EntityType.ToString(),
                        Action = x.Request.Action.ToString(),
                        RowKey = x.Request.RowKey,
                        Status = x.Request.Status.ToString(),
                        Fields = diff.Fields,
                    };
                }).ToList();
            }

            var dbLessons = await unitOfWork.Lessons.Query()
                .Include(l => l.CertificateLevel)
                .Include(l => l.LessonKanjis!).ThenInclude(x => x.Kanji)
                .Include(l => l.LessonGrammars!).ThenInclude(x => x.GrammarPattern)
                .Include(l => l.LessonVocabularies!).ThenInclude(x => x.Vocabulary)
                .Where(l => !l.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var response in responses.Where(x => x.EntityType == ImportEntityType.Lesson))
            {
                var payload = lessons.GetValueOrDefault(response.RequestId);
                if (payload is null) continue;
                var dbLesson = dbLessons.FirstOrDefault(l => l.Title == payload.Title && l.CertificateLevelId == payload.CertificateLevelId);
                if (dbLesson is null) continue;
                response.RelatedLessonContent.RemoveAll(x => x.Action == "Existing");
                AddDbRelated(response, "Kanji", dbLesson.LessonKanjis?.Where(x => x.Kanji is { IsDeleted: false }).Select(x => (x.Kanji.Character, x.Kanji.Meaning)));
                AddDbRelated(response, "Grammar", dbLesson.LessonGrammars?.Where(x => x.GrammarPattern is { IsDeleted: false }).Select(x => (x.GrammarPattern.Title, x.GrammarPattern.Structure)));
                AddDbRelated(response, "Vocabulary", dbLesson.LessonVocabularies?.Where(x => x.Vocabulary is { IsDeleted: false }).Select(x => ($"{x.Vocabulary.Word} ({x.Vocabulary.Reading})", x.Vocabulary.Meaning)));
                var exercises = await unitOfWork.PracticeExercises.Query()
                    .Include(x => x.Questions!).ThenInclude(x => x.Options)
                    .Where(x => !x.IsDeleted && x.LessonId == dbLesson.Id)
                    .ToListAsync(cancellationToken);
                foreach (var exercise in exercises)
                    response.RelatedLessonContent.Add(new PendingLessonContentResponse
                    {
                        EntityType = "PracticeExercise",
                        Action = "Existing",
                        Status = "Approved",
                        RowKey = exercise.Title,
                        Fields = [new ApprovalFieldDiffResponse { Field = "description", Label = "Mô tả", NewValue = exercise.Description },
                            new ApprovalFieldDiffResponse { Field = "questions", Label = "Số câu hỏi", NewValue = (exercise.Questions?.Count(q => !q.IsDeleted) ?? 0).ToString() }]
                    });
            }
        }

        private static string LessonKey(string title, string code) => $"{title.Trim()}|{code.Trim()}";

        private static void AddDbRelated(PendingApprovalItemResponse response, string entityType, IEnumerable<(string, string)>? items)
        {
            if (items is null) return;
            foreach (var (name, meaning) in items)
            {
                response.RelatedLessonContent.Add(new PendingLessonContentResponse
                {
                    EntityType = entityType,
                    Action = "Existing",
                    Status = "Approved",
                    RowKey = name,
                    Fields = [new ApprovalFieldDiffResponse { Field = "meaning", Label = "Nghĩa / nội dung", NewValue = meaning }]
                });
            }
        }

        public async Task<ApiResponse<bool>> ReviewRequestAsync(
              int requestId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            if (!request.Approve && string.IsNullOrWhiteSpace(request.ReviewNote))
                return ApiResponse<bool>.Fail("Vui lòng nhập lý do từ chối.");

            try
            {
                var req = await unitOfWork.ContentApprovalRequests.GetByIdAsync(requestId, cancellationToken);

                if (req is null || !IsReviewable(req.Status))
                    return ApiResponse<bool>.Fail("Yêu cầu không tồn tại hoặc đã được xử lý.");

                await ApplyReviewAsync(req, request.Approve, request.ReviewNote, cancellationToken);

                unitOfWork.ContentApprovalRequests.Update(req);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await NotifyAuthorsAsync([req], cancellationToken);

                return ApiResponse<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail($"Duyệt yêu cầu thất bại: {ex.Message}");
            }
        }

        public async Task<ApiResponse<BulkReviewResponse>> ReviewLessonGroupAsync(
            int lessonRequestId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            if (!request.Approve && string.IsNullOrWhiteSpace(request.ReviewNote))
                return ApiResponse<BulkReviewResponse>.Fail("Vui lòng nhập lý do từ chối.");
            try
            {
                var lessonRequest = await unitOfWork.ContentApprovalRequests.GetByIdAsync(lessonRequestId, cancellationToken);
                if (lessonRequest is null || lessonRequest.EntityType != ImportEntityType.Lesson || !IsReviewable(lessonRequest.Status))
                    return ApiResponse<BulkReviewResponse>.Fail("Yêu cầu Lesson không tồn tại hoặc đã được xử lý.");

                var payload = JsonSerializer.Deserialize<LessonImportPayloadResponse>(lessonRequest.PayloadJson, JsonOptions)
                    ?? throw new InvalidOperationException("Payload Lesson rỗng.");
                var requests = await unitOfWork.ContentApprovalRequests.Query()
                    .Where(r => !r.IsDeleted && (r.Status == ContentApprovalStatus.Draft || r.Status == ContentApprovalStatus.PendingReview))
                    .ToListAsync(cancellationToken);

                var group = new List<ContentApprovalRequest> { lessonRequest };
                foreach (var candidate in requests.Where(r => r.Id != lessonRequest.Id && r.EntityType is ImportEntityType.Kanji or ImportEntityType.Grammar or ImportEntityType.Vocabulary or ImportEntityType.PracticeExercise))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(candidate.PayloadJson);
                        if (doc.RootElement.TryGetProperty("LessonTitle", out var titleNode)
                            && titleNode.GetString() == payload.Title
                            && doc.RootElement.TryGetProperty("CertificateLevelId", out var levelNode)
                            && levelNode.GetInt32() == payload.CertificateLevelId)
                            group.Add(candidate);
                    }
                    catch (JsonException) { }
                }

                var result = new BulkReviewResponse();
                // For approval, save the parent first so a new Lesson has an ID before child links are written.
                if (request.Approve)
                {
                    await ApplyReviewAsync(lessonRequest, true, request.ReviewNote, cancellationToken);
                    unitOfWork.ContentApprovalRequests.Update(lessonRequest);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                else
                    foreach (var item in group) await ApplyReviewAsync(item, false, request.ReviewNote, cancellationToken);

                result.SuccessCount = group.Count;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await NotifyAuthorsAsync(request.Approve ? [lessonRequest] : group, cancellationToken);
                return ApiResponse<BulkReviewResponse>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<BulkReviewResponse>.Fail($"Duyệt nhóm Lesson thất bại: {ex.Message}");
            }
        }

        public async Task<ApiResponse<BulkReviewResponse>> ReviewBatchAsync(
               BulkReviewRequest request, CancellationToken cancellationToken)
        {
            if (request.Items is not { Count: > 0 })
                return ApiResponse<BulkReviewResponse>.Fail("Chưa chọn yêu cầu nào để duyệt.");

            var result = new BulkReviewResponse();
            var seen = new HashSet<int>();
            var reviewed = new List<ContentApprovalRequest>();

            foreach (var item in request.Items)
            {
                // Bỏ qua nếu FE lỡ gửi trùng RequestId nhiều lần trong cùng 1 lô.
                if (!seen.Add(item.RequestId)) continue;

                if (!item.Approve && string.IsNullOrWhiteSpace(item.ReviewNote))
                {
                    result.Failures.Add(new BulkReviewItemResult
                    {
                        RequestId = item.RequestId,
                        Reason = "Vui lòng nhập lý do từ chối."
                    });
                    continue;
                }

                try
                {
                    var req = await unitOfWork.ContentApprovalRequests.GetByIdAsync(item.RequestId, cancellationToken);
                    if (req is null || !IsReviewable(req.Status))
                    {
                        result.Failures.Add(new BulkReviewItemResult
                        {
                            RequestId = item.RequestId,
                            Reason = "Yêu cầu không tồn tại hoặc đã được xử lý."
                        });
                        continue;
                    }

                    await ApplyReviewAsync(req, item.Approve, item.ReviewNote, cancellationToken);
                    unitOfWork.ContentApprovalRequests.Update(req);
                    reviewed.Add(req);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Failures.Add(new BulkReviewItemResult { RequestId = item.RequestId, Reason = ex.Message });
                }
            }

            // Lưu 1 lần cho cả lô: các request đã xử lý thành công (staged ở trên) được ghi xuống DB,
            // các request lỗi/bị bỏ qua ở trên không ảnh hưởng tới các request khác.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await NotifyAuthorsAsync(reviewed, cancellationToken);

            if (result.SuccessCount == 0 && result.Failures.Count > 0)
                return ApiResponse<BulkReviewResponse>.Fail(
                    string.Join("; ", result.Failures.Select(f => $"#{f.RequestId}: {f.Reason}")));

            return ApiResponse<BulkReviewResponse>.Success(result);
        }

        public async Task<ApiResponse<List<ContentApprovalListItemResponse>>> GetMyApprovalsAsync(CancellationToken cancellationToken)
        {
            var userId = claimService.GetUserClaim().Id;
            var rows = await unitOfWork.ContentApprovalRequests.Query()
                .Where(request => !request.IsDeleted && request.SubmittedByUserId == userId)
                .OrderByDescending(request => request.SubmittedAt)
                .Take(50)
                .ToListAsync(cancellationToken);
            return ApiResponse<List<ContentApprovalListItemResponse>>.Success(rows.Select(request => new ContentApprovalListItemResponse
            {
                Id = request.Id,
                BatchId = request.BatchId,
                EntityType = request.EntityType,
                Action = request.Action,
                TargetEntityId = request.TargetEntityId,
                RowKey = request.RowKey,
                Status = request.Status,
                SourceFileName = request.SourceFileName,
                SourceRowNumber = request.SourceRowNumber,
                SubmittedByUserId = request.SubmittedByUserId,
                SubmittedAt = request.SubmittedAt,
                ReviewedByName = request.ReviewedByName,
                ReviewedAt = request.ReviewedAt,
                ReviewNote = request.ReviewNote,
            }).ToList());
        }

        private async Task NotifyAuthorsAsync(IEnumerable<ContentApprovalRequest> reviewed, CancellationToken cancellationToken)
        {
            foreach (var group in reviewed.Where(request => request.SubmittedByUserId > 0).GroupBy(request => request.SubmittedByUserId))
            {
                var rejected = group
                    .Where(request => request.Status == ContentApprovalStatus.Rejected)
                    .Select(request => (request.RowKey, request.ReviewNote ?? "Không có ghi chú"))
                    .ToList();
                await notifier.NotifyAuthorOnReviewResultAsync(group.Key, group.Count(), rejected, cancellationToken);
            }
        }

        private static bool IsReviewable(ContentApprovalStatus status) =>
            status is ContentApprovalStatus.Draft or ContentApprovalStatus.PendingReview;

        private async Task ApplyReviewAsync(
            ContentApprovalRequest req, bool approve, string? reviewNote, CancellationToken cancellationToken)
        {
            if (!approve)
            {
                req.Status = ContentApprovalStatus.Rejected;
                req.ReviewNote = reviewNote;
                req.ReviewedAt = DateTime.UtcNow;
                return;
            }

            // Thực thi ghi dữ liệu vào DB theo từng loại Entity
            switch (req.EntityType)
            {
                case ImportEntityType.Lesson:
                    await ApplyLessonApprovalAsync(req, cancellationToken);
                    break;
                case ImportEntityType.PracticeExercise:
                    await ApplyPracticeExerciseApprovalAsync(req, cancellationToken);
                    break;
                case ImportEntityType.Vocabulary:
                    await ApplyVocabularyApprovalAsync(req, cancellationToken);
                    break;
                case ImportEntityType.Kanji:
                    await ApplyKanjiApprovalAsync(req, cancellationToken);
                    break;
                case ImportEntityType.Grammar:
                    await ApplyGrammarApprovalAsync(req, cancellationToken);
                    break;
                case ImportEntityType.MockTest:
                    await ApplyMockTestApprovalAsync(req, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"Chưa hỗ trợ phê duyệt tự động cho loại {req.EntityType}.");
            }

            req.Status = ContentApprovalStatus.Approved;
            req.ReviewNote = reviewNote;
            req.ReviewedAt = DateTime.UtcNow;
        }

        #region Helper Methods Thực thi lưu Entity + ContentLevelMapping
        private async Task ApplyVocabularyApprovalAsync(ContentApprovalRequest req, CancellationToken cancellationToken)
        {
            var p = JsonSerializer.Deserialize<VocabularyImportPayloadResponse>(req.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Payload Vocabulary rỗng.");

            Vocabulary vocab;
            if (req.Action == ImportRowAction.New)
            {
                vocab = new Vocabulary
                {
                    Word = p.Word,
                    Reading = p.Reading,
                    Meaning = p.Meaning,
                    ExampleSentence = p.ExampleSentence,
                    ExampleSentenceMeaning = p.ExampleSentenceMeaning,
                    AudioUrl = p.AudioUrl
                };
                await unitOfWork.Vocabularies.AddAsync(vocab, cancellationToken);
            }
            else
            {
                vocab = await unitOfWork.Vocabularies.GetByIdAsync(req.TargetEntityId!.Value, cancellationToken)
                    ?? throw new InvalidOperationException("Không tìm thấy từ vựng gốc.");

                vocab.Meaning = p.Meaning;
                vocab.ExampleSentence = p.ExampleSentence;
                vocab.ExampleSentenceMeaning = p.ExampleSentenceMeaning;
                if (!string.IsNullOrEmpty(p.AudioUrl)) vocab.AudioUrl = p.AudioUrl;

                unitOfWork.Vocabularies.Update(vocab);
            }

            if (!string.IsNullOrWhiteSpace(p.LessonTitle))
                await LinkVocabularyToLessonAsync(vocab, p.LessonTitle, p.CertificateLevelId, cancellationToken);

            // Gắn Cấp độ vào ContentLevelMappings
            if (p.CertificateLevelId > 0)
            {
                var existingMapping = await unitOfWork.ContentLevelMappings.FirstOrDefaultAsync(
                    m => m.VocabularyId == vocab.Id && m.CertificateLevelId == p.CertificateLevelId && !m.IsDeleted, cancellationToken);

                if (existingMapping == null)
                {
                    await unitOfWork.ContentLevelMappings.AddAsync(new ContentLevelMapping
                    {
                        Vocabulary = vocab,
                        CertificateLevelId = p.CertificateLevelId
                    }, cancellationToken);
                }
            }

        }

        private async Task ApplyKanjiApprovalAsync(ContentApprovalRequest req, CancellationToken cancellationToken)
        {
            var p = JsonSerializer.Deserialize<KanjiImportPayloadResponse>(req.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Payload Kanji rỗng.");

            Kanji kanji;
            if (req.Action == ImportRowAction.New)
            {
                kanji = new Kanji
                {
                    Character = p.Character,
                    Meaning = p.Meaning,
                    SinoVietnamese = p.SinoVietnamese,
                    OnYomi = p.OnYomi,
                    KunYomi = p.KunYomi,
                    StrokeCount = p.StrokeCount
                };
                await unitOfWork.Kanjis.AddAsync(kanji, cancellationToken);
            }
            else
            {
                kanji = await unitOfWork.Kanjis.GetByIdAsync(req.TargetEntityId!.Value, cancellationToken)
                    ?? throw new InvalidOperationException("Không tìm thấy Kanji gốc.");

                kanji.Meaning = p.Meaning;
                kanji.SinoVietnamese = p.SinoVietnamese;
                kanji.OnYomi = p.OnYomi;
                kanji.KunYomi = p.KunYomi;
                kanji.StrokeCount = p.StrokeCount;

                unitOfWork.Kanjis.Update(kanji);
            }

            if (p.CertificateLevelId > 0)
            {
                var existingMapping = await unitOfWork.ContentLevelMappings.FirstOrDefaultAsync(
                    m => m.KanjiId == kanji.Id && m.CertificateLevelId == p.CertificateLevelId && !m.IsDeleted, cancellationToken);

                if (existingMapping == null)
                {
                    await unitOfWork.ContentLevelMappings.AddAsync(new ContentLevelMapping
                    {
                        Kanji = kanji,
                        CertificateLevelId = p.CertificateLevelId
                    }, cancellationToken);
                }
            }

            if (!string.IsNullOrWhiteSpace(p.LessonTitle))
                await LinkKanjiToLessonAsync(kanji, p.LessonTitle, p.CertificateLevelId, cancellationToken);
        }

        private async Task<Lesson?> FindLessonAsync(string title, int levelId, CancellationToken cancellationToken) =>
            await unitOfWork.Lessons.FirstOrDefaultAsync(l => !l.IsDeleted && l.Title == title && l.CertificateLevelId == levelId, cancellationToken);

        private async Task ApplyLessonApprovalAsync(ContentApprovalRequest req, CancellationToken cancellationToken)
        {
            var p = JsonSerializer.Deserialize<LessonImportPayloadResponse>(req.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Payload Lesson rỗng.");
            Lesson lesson;
            if (req.Action == ImportRowAction.New)
            {
                lesson = new Lesson { Title = p.Title, Description = p.Description, SortOrder = p.SortOrder, CertificateLevelId = p.CertificateLevelId, LanguageSkillId = p.LanguageSkillId };
                await unitOfWork.Lessons.AddAsync(lesson, cancellationToken);
            }
            else
            {
                lesson = await unitOfWork.Lessons.GetByIdAsync(req.TargetEntityId!.Value, cancellationToken)
                    ?? throw new InvalidOperationException("Không tìm thấy Lesson gốc.");
                lesson.Title = p.Title;
                lesson.Description = p.Description;
                lesson.SortOrder = p.SortOrder;
                lesson.CertificateLevelId = p.CertificateLevelId;
                lesson.LanguageSkillId = p.LanguageSkillId;
                unitOfWork.Lessons.Update(lesson);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await LinkPendingLessonContentAsync(lesson, req, p, cancellationToken);
        }

        private async Task ApplyPracticeExerciseApprovalAsync(ContentApprovalRequest req, CancellationToken cancellationToken)
        {
            var p = JsonSerializer.Deserialize<PracticeExerciseImportPayloadResponse>(req.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Payload Practice Exercise rỗng.");
            var lesson = await FindLessonAsync(p.LessonTitle, p.CertificateLevelId, cancellationToken)
                ?? throw new InvalidOperationException($"Không tìm thấy Lesson '{p.LessonTitle}' ({p.CertificateLevelId}) để lưu Practice Exercise.");

            static List<PracticeQuestion> MapQuestions(IEnumerable<PracticeQuestionPayloadResponse> questions) => questions
                .OrderBy(q => q.SortOrder).Select(q => new PracticeQuestion
                {
                    QuestionText = q.QuestionText,
                    Explanation = q.Explanation,
                    SortOrder = q.SortOrder,
                    Options = q.Options.OrderBy(o => o.SortOrder).Select(o => new PracticeQuestionOption
                    {
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect,
                        SortOrder = o.SortOrder
                    }).ToList()
                }).ToList();

            if (req.Action == ImportRowAction.New)
            {
                await unitOfWork.PracticeExercises.AddAsync(new PracticeExercise
                {
                    Title = p.Title,
                    Description = p.Description,
                    SortOrder = p.SortOrder,
                    LessonId = lesson.Id,
                    LanguageSkillId = p.LanguageSkillId,
                    Questions = MapQuestions(p.Questions)
                }, cancellationToken);
                return;
            }

            var exercise = await unitOfWork.PracticeExercises.GetWithQuestionsAndOptionsAsync(req.TargetEntityId!.Value, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy Practice Exercise gốc.");
            exercise.Title = p.Title;
            exercise.Description = p.Description;
            exercise.SortOrder = p.SortOrder;
            exercise.LessonId = lesson.Id;
            exercise.LanguageSkillId = p.LanguageSkillId;
            foreach (var question in (exercise.Questions ?? []).Where(q => !q.IsDeleted))
            {
                question.IsDeleted = true;
                foreach (var option in (question.Options ?? []).Where(o => !o.IsDeleted)) option.IsDeleted = true;
            }
            exercise.Questions = MapQuestions(p.Questions);
            unitOfWork.PracticeExercises.Update(exercise);
        }

        private async Task LinkPendingLessonContentAsync(Lesson lesson, ContentApprovalRequest lessonRequest, LessonImportPayloadResponse lessonPayload, CancellationToken cancellationToken)
        {
            var pending = await unitOfWork.ContentApprovalRequests.Query()
                .Where(r => !r.IsDeleted
                    && (r.Status == ContentApprovalStatus.Draft || r.Status == ContentApprovalStatus.PendingReview || r.Status == ContentApprovalStatus.Approved)
                    && r.Id != lessonRequest.Id
                    && (r.EntityType == ImportEntityType.Kanji || r.EntityType == ImportEntityType.Grammar
                        || r.EntityType == ImportEntityType.Vocabulary || r.EntityType == ImportEntityType.PracticeExercise))
                .ToListAsync(cancellationToken);

            var matching = new List<ContentApprovalRequest>();
            foreach (var related in pending)
            {
                try
                {
                    using var doc = JsonDocument.Parse(related.PayloadJson);
                    if (!doc.RootElement.TryGetProperty("LessonTitle", out var titleNode) || titleNode.GetString() != lessonPayload.Title) continue;
                    if (!doc.RootElement.TryGetProperty("CertificateLevelId", out var levelNode) || levelNode.GetInt32() != lessonPayload.CertificateLevelId) continue;
                    matching.Add(related);
                }
                catch (JsonException) { throw new InvalidOperationException($"Không đọc được nội dung liên quan #{related.Id}."); }
            }

            // First create/update content so generated entity IDs exist, then persist links to the lesson.
            foreach (var related in matching.Where(x => x.Status != ContentApprovalStatus.Approved && x.EntityType == ImportEntityType.Kanji)) await ApplyKanjiApprovalAsync(related, cancellationToken);
            foreach (var related in matching.Where(x => x.Status != ContentApprovalStatus.Approved && x.EntityType == ImportEntityType.Grammar)) await ApplyGrammarApprovalAsync(related, cancellationToken);
            foreach (var related in matching.Where(x => x.Status != ContentApprovalStatus.Approved && x.EntityType == ImportEntityType.Vocabulary)) await ApplyVocabularyApprovalAsync(related, cancellationToken);
            foreach (var related in matching.Where(x => x.Status != ContentApprovalStatus.Approved && x.EntityType == ImportEntityType.PracticeExercise)) await ApplyPracticeExerciseApprovalAsync(related, cancellationToken);
            foreach (var related in matching)
            {
                // Requests newly approved above already create their lesson link in Apply*ApprovalAsync.
                // Only repair links for content that was approved before its Lesson existed.
                if (related.Status != ContentApprovalStatus.Approved) continue;
                if (related.EntityType == ImportEntityType.Kanji)
                {
                    var p = JsonSerializer.Deserialize<KanjiImportPayloadResponse>(related.PayloadJson, JsonOptions)!;
                    var entity = await unitOfWork.Kanjis.FirstOrDefaultAsync(x => !x.IsDeleted && x.Character == p.Character, cancellationToken);
                    if (entity is not null) await LinkKanjiToLessonAsync(entity, lessonPayload.Title, lessonPayload.CertificateLevelId, cancellationToken);
                }
                else if (related.EntityType == ImportEntityType.Grammar)
                {
                    var p = JsonSerializer.Deserialize<GrammarImportPayloadResponse>(related.PayloadJson, JsonOptions)!;
                    var entity = await unitOfWork.GrammarPatterns.FirstOrDefaultAsync(x => !x.IsDeleted && x.Title == p.Title, cancellationToken);
                    if (entity is not null) await LinkGrammarToLessonAsync(entity, lessonPayload.Title, lessonPayload.CertificateLevelId, cancellationToken);
                }
                else if (related.EntityType == ImportEntityType.Vocabulary)
                {
                    var p = JsonSerializer.Deserialize<VocabularyImportPayloadResponse>(related.PayloadJson, JsonOptions)!;
                    var entity = await unitOfWork.Vocabularies.FirstOrDefaultAsync(x => !x.IsDeleted && x.Word == p.Word && x.Reading == p.Reading, cancellationToken);
                    if (entity is not null) await LinkVocabularyToLessonAsync(entity, lessonPayload.Title, lessonPayload.CertificateLevelId, cancellationToken);
                }
                else if (related.EntityType == ImportEntityType.PracticeExercise)
                {
                    var p = JsonSerializer.Deserialize<PracticeExerciseImportPayloadResponse>(related.PayloadJson, JsonOptions)!;
                    var lessonRow = await FindLessonAsync(lessonPayload.Title, lessonPayload.CertificateLevelId, cancellationToken);
                    if (lessonRow is null) continue;
                    var exercise = related.Action == ImportRowAction.Modified && related.TargetEntityId.HasValue
                        ? await unitOfWork.PracticeExercises.GetByIdAsync(related.TargetEntityId.Value, cancellationToken)
                        : await unitOfWork.PracticeExercises.FirstOrDefaultAsync(x => !x.IsDeleted && x.LessonId == lessonRow.Id && x.Title == p.Title, cancellationToken);
                    if (exercise is not null) exercise.LessonId = lessonRow.Id;
                }
            }
            foreach (var related in matching.Where(x => x.Status != ContentApprovalStatus.Approved)) MarkRelatedApproved(related);
        }

        private static void MarkRelatedApproved(ContentApprovalRequest request)
        {
            request.Status = ContentApprovalStatus.Approved;
            request.ReviewNote = "Được duyệt cùng Lesson.";
            request.ReviewedAt = DateTime.UtcNow;
        }

        private async Task LinkKanjiToLessonAsync(Kanji kanji, string lessonTitle, int levelId, CancellationToken cancellationToken)
        {
            var lesson = await FindLessonAsync(lessonTitle, levelId, cancellationToken);
            if (lesson is null) return;

            // Kanji có thể vừa được AddAsync trong CHÍNH lần duyệt này và CHƯA SaveChanges, nên kanji.Id vẫn
            // là 0. Chỉ query DB theo KanjiId khi kanji.Id đã là giá trị thật (đã lưu trước đó); nếu không,
            // query DB sẽ vô nghĩa (hoặc tệ hơn, trùng với Kanji "Mới" khác cũng đang có Id = 0 lúc này).
            var alreadyLinkedInDb = kanji.Id > 0 && lesson.Id > 0 && await unitOfWork.LessonKanjis
                .FirstOrDefaultAsync(x => x.LessonId == lesson.Id && x.KanjiId == kanji.Id, cancellationToken) is not null;

            // Add trả về false nếu cặp (lesson, kanji) NÀY (theo tham chiếu object) đã được stage trước đó
            // trong chính request hiện tại - chặn trường hợp 2 ContentApprovalRequest khác nhau (VD: trùng
            // nhau) cùng trỏ tới 1 Kanji đã tồn tại, khiến EF trả về cùng 1 instance tracked cho cả 2 lần gọi.
            var alreadyStagedThisRequest = !_stagedLessonLinks.Add((lesson, kanji));

            if (!alreadyLinkedInDb && !alreadyStagedThisRequest)
            {
                // QUAN TRỌNG: gán qua navigation property (Lesson = lesson, Kanji = kanji), KHÔNG gán thẳng
                // LessonId/KanjiId bằng số nguyên. Nếu gán bằng số, 2 Kanji "Mới" khác nhau trong cùng 1
                // Lesson sẽ tạo ra 2 LessonKanji có cùng khóa (LessonId=X, KanjiId=0) vì cả hai Kanji đều
                // chưa có Id thật lúc này - EF sẽ từ chối tracking bản ghi thứ 2 ngay lập tức. Gán qua
                // navigation property để EF tự fixup đúng Id thật của từng Kanji lúc SaveChanges.
                await unitOfWork.LessonKanjis.AddAsync(new LessonKanji { Lesson = lesson, Kanji = kanji }, cancellationToken);
            }
        }


        private async Task LinkGrammarToLessonAsync(GrammarPattern grammar, string lessonTitle, int levelId, CancellationToken cancellationToken)
        {
            var lesson = await FindLessonAsync(lessonTitle, levelId, cancellationToken);
            if (lesson is null) return;

            var alreadyLinkedInDb = grammar.Id > 0 && lesson.Id > 0 && await unitOfWork.LessonGrammars
                .FirstOrDefaultAsync(x => x.LessonId == lesson.Id && x.GrammarPatternId == grammar.Id, cancellationToken) is not null;

            var alreadyStagedThisRequest = !_stagedLessonLinks.Add((lesson, grammar));

            if (!alreadyLinkedInDb && !alreadyStagedThisRequest)
            {
                // Gán qua navigation property - lý do xem chú thích trong LinkKanjiToLessonAsync ở trên.
                await unitOfWork.LessonGrammars.AddAsync(new LessonGrammar { Lesson = lesson, GrammarPattern = grammar }, cancellationToken);
            }
        }

        private async Task LinkVocabularyToLessonAsync(Vocabulary vocabulary, string lessonTitle, int levelId, CancellationToken cancellationToken)
        {
            var lesson = await FindLessonAsync(lessonTitle, levelId, cancellationToken);
            if (lesson is null) return;

            var alreadyLinkedInDb = vocabulary.Id > 0 && lesson.Id > 0 && await unitOfWork.LessonVocabularies
                .FirstOrDefaultAsync(x => x.LessonId == lesson.Id && x.VocabularyId == vocabulary.Id, cancellationToken) is not null;

            var alreadyStagedThisRequest = !_stagedLessonLinks.Add((lesson, vocabulary));

            if (!alreadyLinkedInDb && !alreadyStagedThisRequest)
            {
                // Gán qua navigation property - lý do xem chú thích trong LinkKanjiToLessonAsync ở trên.
                await unitOfWork.LessonVocabularies.AddAsync(new LessonVocabulary { Lesson = lesson, Vocabulary = vocabulary }, cancellationToken);
            }
        }

        private async Task ApplyGrammarApprovalAsync(ContentApprovalRequest req, CancellationToken cancellationToken)
        {
            var p = JsonSerializer.Deserialize<GrammarImportPayloadResponse>(req.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Payload Grammar rỗng.");

            GrammarPattern grammar;
            if (req.Action == ImportRowAction.New)
            {
                grammar = new GrammarPattern
                {
                    Title = p.Title,
                    Structure = p.Structure,
                    UsageNotes = p.UsageNotes,
                    ExampleSentence = p.ExampleSentence,
                    ExampleSentenceMeaning = p.ExampleSentenceMeaning
                };
                await unitOfWork.GrammarPatterns.AddAsync(grammar, cancellationToken);
            }
            else
            {
                grammar = await unitOfWork.GrammarPatterns.GetByIdAsync(req.TargetEntityId!.Value, cancellationToken)
                    ?? throw new InvalidOperationException("Không tìm thấy Ngữ pháp gốc.");

                grammar.Structure = p.Structure;
                grammar.UsageNotes = p.UsageNotes;
                grammar.ExampleSentence = p.ExampleSentence;
                grammar.ExampleSentenceMeaning = p.ExampleSentenceMeaning;

                unitOfWork.GrammarPatterns.Update(grammar);
            }

            if (!string.IsNullOrWhiteSpace(p.LessonTitle))
                await LinkGrammarToLessonAsync(grammar, p.LessonTitle, p.CertificateLevelId, cancellationToken);

            if (p.CertificateLevelId > 0)
            {
                var existingMapping = await unitOfWork.ContentLevelMappings.FirstOrDefaultAsync(
                    m => m.GrammarPatternId == grammar.Id && m.CertificateLevelId == p.CertificateLevelId && !m.IsDeleted, cancellationToken);

                if (existingMapping == null)
                {
                    await unitOfWork.ContentLevelMappings.AddAsync(new ContentLevelMapping
                    {
                        GrammarPattern = grammar,
                        CertificateLevelId = p.CertificateLevelId
                    }, cancellationToken);
                }
            }
        }

        private async Task ApplyMockTestApprovalAsync(ContentApprovalRequest req, CancellationToken cancellationToken)
        {
            var p = JsonSerializer.Deserialize<MockTestImportPayloadResponse>(req.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Payload MockTest rỗng.");

            // Validate hết TRƯỚC khi add/sửa entity nào: trong ReviewBatchAsync, entity đã add mà sau đó throw
            // vẫn nằm trong ChangeTracker và bị SaveChanges cuối lô ghi xuống.
            if (req.Action == ImportRowAction.New || p.Sections is { Count: > 0 })
            {
                if (p.Sections is not { Count: > 0 })
                    throw new InvalidOperationException("Đề thi chưa có phần (section) nào.");

                foreach (var s in p.Sections)
                {
                    if (req.Action == ImportRowAction.New)
                    {
                        if (s.LanguageSkillId <= 0)
                            throw new InvalidOperationException($"Phần '{s.Title}' thiếu LanguageSkillId.");
                        if (string.IsNullOrWhiteSpace(s.CertificateSectionCode))
                            throw new InvalidOperationException($"Phần '{s.Title}' thiếu CertificateSectionCode.");
                    }

                    foreach (var q in s.Questions)
                    {
                        var type = NormalizeMockQuestionType(q.QuestionType);
                        if (type is "SingleChoice" or "Listening" or "Reading")
                        {
                            if (q.Options.Count < 2)
                                throw new InvalidOperationException($"Câu hỏi '{q.QuestionText}' phải có ít nhất 2 đáp án.");
                            if (q.Options.Count(o => o.IsCorrect) != 1)
                                throw new InvalidOperationException($"Câu hỏi '{q.QuestionText}' phải có đúng 1 đáp án đúng.");
                        }
                        else if (type == "MultipleChoice")
                        {
                            if (q.Options.Count < 2 || q.Options.All(o => !o.IsCorrect))
                                throw new InvalidOperationException($"Câu hỏi nhiều lựa chọn '{q.QuestionText}' cần ít nhất 2 lựa chọn và có đáp án đúng.");
                        }
                        else
                        {
                            if (q.Options.Count != 0)
                                throw new InvalidOperationException($"Câu hỏi {type} '{q.QuestionText}' không dùng Option1..Option4.");
                            if (q.InteractionData is null || q.AnswerKey is null)
                                throw new InvalidOperationException($"Câu hỏi {type} '{q.QuestionText}' cần InteractionDataJson và AnswerKeyJson.");
                        }
                    }
                }
            }

            if (req.Action == ImportRowAction.New)
            {
                // Tra FK từ Code (admin tạo sẵn CertificateSection) TRƯỚC khi tạo entity,
                // để lỗi thì throw sớm, không để entity dở dang trong ChangeTracker.
                var codes = p.Sections!
                    .Select(s => s.CertificateSectionCode!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var certSections = await unitOfWork.CertificateSections.Query()
                    .Where(c => !c.IsDeleted && codes.Contains(c.Code))
                    .ToListAsync(cancellationToken);

                var duplicated = certSections
                    .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                if (duplicated.Count > 0)
                    throw new InvalidOperationException(
                        $"Code CertificateSection bị trùng trong DB: {string.Join(", ", duplicated)}.");

                var sectionIdByCode = certSections.ToDictionary(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase);

                var missing = codes.Where(c => !sectionIdByCode.ContainsKey(c)).ToList();
                if (missing.Count > 0)
                    throw new InvalidOperationException(
                        $"Không tìm thấy CertificateSection có Code: {string.Join(", ", missing)}. Admin cần tạo sẵn trước khi duyệt.");

                var test = new MockTest
                {
                    Title = p.Title,
                    Description = p.Description,
                    TimeLimitMinutes = p.TimeLimitMinutes,
                    IsPlacementAssessment = p.IsPlacementAssessment,
                    CertificateLevelId = p.CertificateLevelId,
                    Sections = p.Sections!.Select(s => new MockTestSection
                    {
                        Title = s.Title,
                        SortOrder = s.SortOrder,
                        LanguageSkillId = s.LanguageSkillId,
                        CertificateSectionId = sectionIdByCode[s.CertificateSectionCode!.Trim()],
                        Questions = s.Questions.Select(q => new MockQuestion
                        {
                            QuestionText = q.QuestionText,
                            QuestionType = NormalizeMockQuestionType(q.QuestionType),
                            InteractionDataJson = q.InteractionData?.GetRawText(),
                            AnswerKeyJson = q.AnswerKey?.GetRawText(),
                            AudioUrl = q.AudioUrl,
                            ImageUrl = q.ImageUrl,
                            Explanation = q.Explanation,
                            SortOrder = q.SortOrder,
                            Options = q.Options.Select(o => new MockQuestionOption
                            {
                                OptionText = o.OptionText,
                                IsCorrect = o.IsCorrect,
                                SortOrder = o.SortOrder
                            }).ToList()
                        }).ToList()
                    }).ToList()
                };

                await unitOfWork.MockTests.AddAsync(test, cancellationToken);
            }
            else
            {
                var test = await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(req.TargetEntityId!.Value, cancellationToken)
                    ?? throw new InvalidOperationException("Không tìm thấy đề thi gốc.");

                test.Title = p.Title;
                test.Description = p.Description;
                test.TimeLimitMinutes = p.TimeLimitMinutes;
                test.IsPlacementAssessment = p.IsPlacementAssessment;
                test.CertificateLevelId = p.CertificateLevelId;

                if (p.Sections is { Count: > 0 })
                {
                    var sectionCodes = p.Sections.Select(s => s.CertificateSectionCode?.Trim())
                        .Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var certSections = await unitOfWork.CertificateSections.Query()
                        .Where(section => !section.IsDeleted && sectionCodes.Contains(section.Code))
                        .ToListAsync(cancellationToken);
                    var sectionIdByCode = certSections.ToDictionary(section => section.Code, section => section.Id, StringComparer.OrdinalIgnoreCase);
                    var missingCodes = sectionCodes.Where(code => !sectionIdByCode.ContainsKey(code)).ToList();
                    if (missingCodes.Count > 0)
                        throw new InvalidOperationException($"Không tìm thấy CertificateSection: {string.Join(", ", missingCodes)}.");

                    // Giữ lại câu cũ vì attempt lịch sử tham chiếu đến chúng; ẩn cấu trúc cũ và tạo phiên bản mới.
                    foreach (var oldSection in test.Sections ?? [])
                    {
                        oldSection.IsDeleted = true;
                        foreach (var oldQuestion in oldSection.Questions ?? [])
                        {
                            oldQuestion.IsDeleted = true;
                            foreach (var oldOption in oldQuestion.Options ?? []) oldOption.IsDeleted = true;
                        }
                    }

                    var replacementSections = p.Sections.Select(section => new MockTestSection
                    {
                        Title = section.Title,
                        SortOrder = section.SortOrder,
                        LanguageSkillId = section.LanguageSkillId,
                        CertificateSectionId = sectionIdByCode[section.CertificateSectionCode!.Trim()],
                        Questions = section.Questions.Select(question => new MockQuestion
                        {
                            QuestionText = question.QuestionText,
                            QuestionType = NormalizeMockQuestionType(question.QuestionType),
                            InteractionDataJson = question.InteractionData?.GetRawText(),
                            AnswerKeyJson = question.AnswerKey?.GetRawText(),
                            AudioUrl = question.AudioUrl,
                            ImageUrl = question.ImageUrl,
                            Explanation = question.Explanation,
                            SortOrder = question.SortOrder,
                            Options = question.Options.Select(option => new MockQuestionOption
                            {
                                OptionText = option.OptionText,
                                IsCorrect = option.IsCorrect,
                                SortOrder = option.SortOrder
                            }).ToList()
                        }).ToList()
                    }).ToList();
                    (test.Sections ??= []).AddRange(replacementSections);
                }

                unitOfWork.MockTests.Update(test);
            }
        }
        #endregion

        private static string NormalizeMockQuestionType(string? questionType)
        {
            var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SingleChoice", "MultipleChoice", "Ordering", "FillBlank", "Matching", "ShortAnswer", "Listening", "Reading"
            };
            if (string.IsNullOrWhiteSpace(questionType)) return "SingleChoice";
            if (!supported.Contains(questionType.Trim()))
                throw new InvalidOperationException($"Dạng câu MockTest không hỗ trợ: {questionType}.");
            return supported.First(x => string.Equals(x, questionType.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}