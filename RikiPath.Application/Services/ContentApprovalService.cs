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
        IContentApprovalNotifierService notifier,
        IContentAudioTranscriptionService? audioTranscriptions = null) : IContentApprovalService
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
        private Dictionary<int, string>? _languageSkillNames;
        private Dictionary<int, (string Code, bool IsDeleted)>? _certificateLevels;
        private Dictionary<string, List<int>>? _certificateSectionIdsByCode;

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

        public Task<ApiResponse<int>> SubmitImportRequestsAsync(
            List<SheetImportResponse> sheetDiffs, CancellationToken cancellationToken)
        {
            return Task.FromResult(ApiResponse<int>.Fail(
                "Luồng gửi diff trực tiếp đã đóng. Hãy lưu bản nháp bằng /api/content-import/confirm rồi gửi duyệt từ bản nháp.",
                System.Net.HttpStatusCode.Gone));
        }

        public async Task<ApiResponse<List<PendingApprovalItemResponse>>> GetPendingApprovalsAsync(
            ImportEntityType? entityType, CancellationToken cancellationToken)
        {
            try
            {
                var query = unitOfWork.ContentApprovalRequests.Query()
                    .Include(r => r.SubmittedBy)
                    .Where(r => !r.IsDeleted && r.Status == ContentApprovalStatus.PendingReview);

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
                    || r.Status == ContentApprovalStatus.PendingReview)
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
            var authors = requests.ToDictionary(x => x.Id, x => x.SubmittedByUserId);
            foreach (var response in responses.Where(x => x.EntityType == ImportEntityType.Lesson))
            {
                if (!lessonRequestByKey.TryGetValue(response.RequestId, out var key)) continue;
                response.RelatedLessonContent = related.Where(x => x.LessonKey == key &&
                    (x.Request.Status == ContentApprovalStatus.Approved ||
                     x.Request.SubmittedByUserId == authors[response.RequestId])).Select(x =>
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

        private async Task<string> ResolveReviewerNameAsync(CancellationToken ct)
        {
            var claim = claimService.GetUserClaim();
            var admin = await unitOfWork.UserAccounts.GetByIdAsync(claim.Id, ct);
            return admin is null ? $"Admin #{claim.Id}" : UserNameHelper.FormatFullName(admin.FirstName, admin.LastName);
        }

        public async Task<ApiResponse<bool>> ReviewRequestAsync(
              int requestId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            if (!request.Approve && string.IsNullOrWhiteSpace(request.ReviewNote))
                return ApiResponse<bool>.Fail("Vui lòng nhập lý do từ chối.");

            try
            {
                var actor = claimService.GetUserClaim();
                var reviewerName = await ResolveReviewerNameAsync(cancellationToken);
                ApiResponse<bool>? conflictOrFail = null;
                ContentApprovalRequest? processed = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var req = await unitOfWork.ContentApprovalRequests.GetByIdForUpdateAsync(requestId, token);

                    if (req is null || req.IsDeleted)
                    {
                        conflictOrFail = ApiResponse<bool>.NotFound("Không tìm thấy yêu cầu duyệt nội dung.");
                        return;
                    }
                    if (!IsReviewable(req.Status))
                    {
                        conflictOrFail = ApiResponse<bool>.Fail("Yêu cầu đã được quản trị viên khác xử lý. Vui lòng tải lại.", System.Net.HttpStatusCode.Conflict);
                        return;
                    }

                    req.ReviewedByName = reviewerName;
                    await ApplyReviewAsync(req, request.Approve, request.ReviewNote, token);

                    if (unitOfWork.AdminAuditLogs is not null)
                    {
                        await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                        {
                            ActorUserId = actor.Id,
                            EntityType = "ContentApprovalRequest",
                            EntityId = req.Id,
                            Action = request.Approve ? "Approved" : "Rejected",
                            AfterJson = JsonSerializer.Serialize(new { req.Id, EntityType = req.EntityType.ToString(), req.RowKey, Status = req.Status.ToString() }),
                            Reason = request.ReviewNote?.Trim(),
                            CreatedAtUtc = DateTime.UtcNow,
                        }, token);
                    }

                    await unitOfWork.SaveChangesAsync(token);
                    processed = req;
                }, cancellationToken);

                if (conflictOrFail is not null) return conflictOrFail;
                if (processed is not null) await NotifyAuthorsAsync([processed], cancellationToken);

                return ApiResponse<bool>.Success(true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, System.Net.HttpStatusCode.BadRequest);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ApiResponse<bool>.Fail("Yêu cầu vừa được xử lý bởi thao tác khác. Vui lòng tải lại.", System.Net.HttpStatusCode.Conflict);
            }
            catch (Exception)
            {
                return ApiResponse<bool>.Fail("Không thể duyệt yêu cầu lúc này. Vui lòng thử lại sau.", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<BulkReviewResponse>> ReviewLessonGroupAsync(
            int lessonRequestId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            if (!request.Approve && string.IsNullOrWhiteSpace(request.ReviewNote))
                return ApiResponse<BulkReviewResponse>.Fail("Vui lòng nhập lý do từ chối.");
            try
            {
                var actor = claimService.GetUserClaim();
                var reviewerName = await ResolveReviewerNameAsync(cancellationToken);
                ApiResponse<BulkReviewResponse>? fail = null;
                var result = new BulkReviewResponse();
                List<ContentApprovalRequest> notifyTargets = [];

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var lessonRequest = await unitOfWork.ContentApprovalRequests.GetByIdForUpdateAsync(lessonRequestId, token);
                    if (lessonRequest is null || lessonRequest.IsDeleted || lessonRequest.EntityType != ImportEntityType.Lesson)
                    {
                        fail = ApiResponse<BulkReviewResponse>.NotFound("Không tìm thấy yêu cầu Lesson.");
                        return;
                    }
                    if (!IsReviewable(lessonRequest.Status))
                    {
                        fail = ApiResponse<BulkReviewResponse>.Fail("Yêu cầu Lesson đã được xử lý trước đó.", System.Net.HttpStatusCode.Conflict);
                        return;
                    }

                    var payload = JsonSerializer.Deserialize<LessonImportPayloadResponse>(lessonRequest.PayloadJson, JsonOptions)
                        ?? throw new InvalidOperationException("Payload Lesson rỗng.");
                    var requests = await unitOfWork.ContentApprovalRequests.Query()
                        .Where(r => !r.IsDeleted && r.Status == ContentApprovalStatus.PendingReview &&
                            r.SubmittedByUserId == lessonRequest.SubmittedByUserId &&
                            lessonRequest.BatchId != Guid.Empty && r.BatchId == lessonRequest.BatchId)
                        .ToListAsync(token);

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

                    foreach (var related in group.Skip(1).OrderBy(item => item.Id))
                    {
                        var expectedPayload = related.PayloadJson;
                        var locked = await unitOfWork.ContentApprovalRequests.GetByIdForUpdateAsync(related.Id, token);
                        if (locked is null || locked.IsDeleted || !IsReviewable(locked.Status) ||
                            locked.PayloadJson != expectedPayload)
                            throw new InvalidOperationException("Một nội dung trong lô vừa được xử lý. Vui lòng tải lại.");
                    }

                    if (request.Approve)
                    {
                        lessonRequest.ReviewedByName = reviewerName;
                        await ApplyReviewAsync(lessonRequest, true, request.ReviewNote, token);
                        await unitOfWork.SaveChangesAsync(token);
                    }
                    else
                    {
                        foreach (var item in group)
                        {
                            item.ReviewedByName = reviewerName;
                            await ApplyReviewAsync(item, false, request.ReviewNote, token);
                        }
                    }

                    if (unitOfWork.AdminAuditLogs is not null)
                    {
                        await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                        {
                            ActorUserId = actor.Id,
                            EntityType = "ContentApprovalLessonGroup",
                            EntityId = lessonRequest.Id,
                            Action = request.Approve ? "Approved" : "Rejected",
                            AfterJson = JsonSerializer.Serialize(new { LessonRequestId = lessonRequest.Id, Count = group.Count }),
                            Reason = request.ReviewNote?.Trim(),
                            CreatedAtUtc = DateTime.UtcNow,
                        }, token);
                    }

                    var completedStatus = request.Approve
                        ? ContentApprovalStatus.Approved : ContentApprovalStatus.Rejected;
                    if (group.Any(item => item.Status != completedStatus))
                        throw new InvalidOperationException("Một số nội dung trong lô chưa được xử lý. Vui lòng tải lại.");
                    result.SuccessCount = group.Count;
                    await unitOfWork.SaveChangesAsync(token);
                    notifyTargets = group;
                }, cancellationToken);

                if (fail is not null) return fail;
                if (notifyTargets.Count > 0) await NotifyAuthorsAsync(notifyTargets, cancellationToken);
                return ApiResponse<BulkReviewResponse>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return ApiResponse<BulkReviewResponse>.Fail(ex.Message, System.Net.HttpStatusCode.BadRequest);
            }
            catch (Exception)
            {
                return ApiResponse<BulkReviewResponse>.Fail("Không thể duyệt nhóm Lesson lúc này. Vui lòng thử lại.", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<BulkReviewResponse>> ReviewBatchAsync(
               BulkReviewRequest request, CancellationToken cancellationToken)
        {
            if (request.Items is not { Count: > 0 })
                return ApiResponse<BulkReviewResponse>.Fail("Chưa chọn yêu cầu nào để duyệt.");

            var actor = claimService.GetUserClaim();
            var reviewerName = await ResolveReviewerNameAsync(cancellationToken);
            var result = new BulkReviewResponse();
            var seen = new HashSet<int>();
            var reviewed = new List<ContentApprovalRequest>();
            var approvedWithLesson = new Dictionary<int, ContentApprovalRequest>();
            var requestedIds = request.Items.Select(item => item.RequestId).Where(id => id > 0).Distinct().ToArray();
            var requestTypes = requestedIds.Length == 0
                ? new Dictionary<int, ImportEntityType>()
                : await unitOfWork.ContentApprovalRequests.Query()
                    .AsNoTracking()
                    .Where(r => requestedIds.Contains(r.Id))
                    .ToDictionaryAsync(r => r.Id, r => r.EntityType, cancellationToken);
            var orderedItems = request.Items
                .OrderBy(i => requestTypes.GetValueOrDefault(i.RequestId) == ImportEntityType.Lesson ? 0 : 1)
                .ThenBy(i => i.RequestId)
                .ToList();

            foreach (var item in orderedItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seen.Add(item.RequestId))
                {
                    result.Failures.Add(new BulkReviewItemResult
                    {
                        RequestId = item.RequestId,
                        Reason = "Yêu cầu bị lặp trong danh sách duyệt."
                    });
                    continue;
                }

                // Approving a Lesson can approve its related rows in the same import batch.
                if (approvedWithLesson.TryGetValue(item.RequestId, out var previouslyApproved))
                {
                    if (item.Approve)
                    {
                        reviewed.Add(previouslyApproved);
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.Failures.Add(new BulkReviewItemResult
                        {
                            RequestId = item.RequestId,
                            Reason = "Nội dung đã được duyệt cùng bài học trong lô này."
                        });
                    }
                    continue;
                }

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
                    _stagedLessonLinks.Clear();
                    ContentApprovalRequest? processed = null;
                    List<ContentApprovalRequest> cascaded = [];
                    await unitOfWork.ExecuteInTransactionAsync(async token =>
                    {
                        var approval = await unitOfWork.ContentApprovalRequests
                            .GetByIdForUpdateAsync(item.RequestId, token);
                        if (approval is null || approval.IsDeleted || !IsReviewable(approval.Status))
                            throw new InvalidOperationException("Yêu cầu không tồn tại hoặc đã được xử lý.");

                        List<ContentApprovalRequest> relatedCandidates = [];
                        if (item.Approve && approval.EntityType == ImportEntityType.Lesson &&
                            approval.BatchId != Guid.Empty)
                        {
                            relatedCandidates = await unitOfWork.ContentApprovalRequests.Query()
                                .Where(row => requestedIds.Contains(row.Id) && row.Id != approval.Id &&
                                    row.BatchId == approval.BatchId &&
                                    row.SubmittedByUserId == approval.SubmittedByUserId &&
                                    !row.IsDeleted && row.Status == ContentApprovalStatus.PendingReview)
                                .ToListAsync(token);
                        }

                        approval.ReviewedByName = reviewerName;
                        await ApplyReviewAsync(approval, item.Approve, item.ReviewNote, token);
                        cascaded = relatedCandidates
                            .Where(row => row.Status == ContentApprovalStatus.Approved)
                            .ToList();

                        if (unitOfWork.AdminAuditLogs is not null)
                        {
                            foreach (var changed in cascaded.Prepend(approval))
                            {
                                await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                                {
                                    ActorUserId = actor.Id,
                                    EntityType = "ContentApprovalRequest",
                                    EntityId = changed.Id,
                                    Action = "BatchReviewed",
                                    AfterJson = JsonSerializer.Serialize(new
                                    {
                                        changed.Id,
                                        Status = changed.Status.ToString()
                                    }),
                                    CreatedAtUtc = DateTime.UtcNow,
                                }, token);
                            }
                        }

                        await unitOfWork.SaveChangesAsync(token);
                        processed = approval;
                    }, cancellationToken);

                    reviewed.Add(processed!);
                    result.SuccessCount++;
                    foreach (var related in cascaded)
                        approvedWithLesson.TryAdd(related.Id, related);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (InvalidOperationException ex)
                {
                    result.Failures.Add(new BulkReviewItemResult { RequestId = item.RequestId, Reason = ex.Message });
                }
                catch (DbUpdateException)
                {
                    result.Failures.Add(new BulkReviewItemResult
                    {
                        RequestId = item.RequestId,
                        Reason = "Dữ liệu mục này vừa thay đổi hoặc bị xung đột. Vui lòng tải lại."
                    });
                }
                catch (Exception)
                {
                    result.Failures.Add(new BulkReviewItemResult
                    {
                        RequestId = item.RequestId,
                        Reason = "Không thể xử lý nội dung này. Vui lòng thử lại."
                    });
                }
            }

            if (reviewed.Count > 0)
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
                .AsNoTracking()
                .Where(request => !request.IsDeleted && request.SubmittedByUserId == userId)
                .OrderByDescending(request => request.ModifiedDate ?? request.SubmittedAt)
                .Take(200)
                .ToListAsync(cancellationToken);

            var levelCodes = await unitOfWork.CertificateLevels.Query()
                .AsNoTracking()
                .Where(l => !l.IsDeleted)
                .ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);

            return ApiResponse<List<ContentApprovalListItemResponse>>.Success(rows.Select(request =>
            {
                var info = ManualContentService.ReadListInfo(request.PayloadJson);
                var levelId = info?.CertificateLevelId;
                var levelCode = levelId is int lid && levelCodes.TryGetValue(lid, out var code) ? code : null;
                return new ContentApprovalListItemResponse
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
                    UpdatedAt = request.ModifiedDate ?? request.CreatedDate ?? request.SubmittedAt,
                    ReviewedByName = request.ReviewedByName,
                    ReviewedAt = request.ReviewedAt,
                    ReviewNote = request.ReviewNote,
                    CertificateLevelId = levelId,
                    CertificateLevelCode = levelCode,
                };
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
            status == ContentApprovalStatus.PendingReview;

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
                    var lesson = ReadLessonPayload(req);
                    if (lesson.TopicId.HasValue || lesson.TopicTitle is not null)
                    {
                        await ApplyLessonTopicApprovalAsync(req, reviewNote, cancellationToken);
                        return;
                    }
                    await ApplyLessonApprovalAsync(req, lesson, cancellationToken);
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

            MarkApproved(req, reviewNote);
        }

        private static void MarkApproved(ContentApprovalRequest request, string? reviewNote)
        {
            request.Status = ContentApprovalStatus.Approved;
            request.ReviewNote = reviewNote;
            request.ReviewedAt = DateTime.UtcNow;
        }

        #region Helper Methods Thực thi lưu Entity + ContentLevelMapping
        private async Task ApplyVocabularyApprovalAsync(ContentApprovalRequest req, CancellationToken cancellationToken)
        {
            var p = JsonSerializer.Deserialize<VocabularyImportPayloadResponse>(req.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Payload Từ vựng rỗng.");
            if (string.IsNullOrWhiteSpace(p.Word) || string.IsNullOrWhiteSpace(p.Meaning))
                throw new InvalidOperationException("Từ vựng thiếu từ hoặc nghĩa bắt buộc.");

            Vocabulary vocab;
            if (req.Action == ImportRowAction.New)
            {
                vocab = new Vocabulary
                {
                    Word = p.Word,
                    ImageUrl = p.ImageUrl,
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

                vocab.Word = p.Word;
                if (p.ImageUrl is not null) vocab.ImageUrl = string.IsNullOrWhiteSpace(p.ImageUrl) ? null : p.ImageUrl;
                vocab.Reading = p.Reading;
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
                ?? throw new InvalidOperationException("Payload Hán tự rỗng.");
            if (string.IsNullOrWhiteSpace(p.Character) || string.IsNullOrWhiteSpace(p.Meaning) || p.StrokeCount <= 0)
                throw new InvalidOperationException("Hán tự thiếu ký tự, nghĩa hoặc số nét hợp lệ.");

            Kanji kanji;
            if (req.Action == ImportRowAction.New)
            {
                kanji = new Kanji
                {
                    Character = p.Character,
                    ImageUrl = p.ImageUrl,
                    Meaning = p.Meaning,
                    SinoVietnamese = p.SinoVietnamese,
                    OnYomi = p.OnYomi,
                    KunYomi = p.KunYomi,
                    StrokeCount = p.StrokeCount,
                    StrokeOrderImageUrl = p.StrokeOrderImageUrl,
                    AudioUrl = p.AudioUrl
                };
                await unitOfWork.Kanjis.AddAsync(kanji, cancellationToken);
            }
            else
            {
                kanji = await unitOfWork.Kanjis.GetByIdAsync(req.TargetEntityId!.Value, cancellationToken)
                    ?? throw new InvalidOperationException("Không tìm thấy Kanji gốc.");

                kanji.Character = p.Character;
                if (p.ImageUrl is not null) kanji.ImageUrl = string.IsNullOrWhiteSpace(p.ImageUrl) ? null : p.ImageUrl;
                kanji.Meaning = p.Meaning;
                kanji.SinoVietnamese = p.SinoVietnamese;
                kanji.OnYomi = p.OnYomi;
                kanji.KunYomi = p.KunYomi;
                kanji.StrokeCount = p.StrokeCount;
                if (p.StrokeOrderImageUrl is not null) kanji.StrokeOrderImageUrl = p.StrokeOrderImageUrl;
                if (p.AudioUrl is not null) kanji.AudioUrl = p.AudioUrl;

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

        private static LessonImportPayloadResponse ReadLessonPayload(ContentApprovalRequest request) =>
            JsonSerializer.Deserialize<LessonImportPayloadResponse>(request.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Payload Lesson rỗng.");

        private async Task ApplyLessonTopicApprovalAsync(ContentApprovalRequest request, string? reviewNote, CancellationToken ct)
        {
            // In batch review, earlier items may have staged changes before this separate transaction.
            await unitOfWork.SaveChangesAsync(ct);
            var id = request.Id;
            var status = request.Status;
            var payloadJson = request.PayloadJson;
            await unitOfWork.ExecuteLessonTopicApprovalAsync(async token =>
            {
                if (!await unitOfWork.ContentApprovalRequests.Query().AnyAsync(x => x.Id == id
                    && !x.IsDeleted && x.Status == status && x.PayloadJson == payloadJson, token))
                    throw new InvalidOperationException("Yêu cầu bài học đã thay đổi hoặc đã được xử lý. Hãy tải lại.");

                var current = await unitOfWork.ContentApprovalRequests.GetByIdAsync(id, token)
                    ?? throw new InvalidOperationException("Không tìm thấy yêu cầu bài học.");
                await ApplyLessonApprovalAsync(current, ReadLessonPayload(current), token);
                MarkApproved(current, reviewNote);
                await unitOfWork.SaveChangesAsync(token);
            }, ct);
        }

        private async Task ApplyLessonApprovalAsync(ContentApprovalRequest req, LessonImportPayloadResponse p, CancellationToken cancellationToken)
        {
            var isNew = req.Action == ImportRowAction.New;
            var lesson = isNew ? new Lesson() : await unitOfWork.Lessons.GetByIdAsync(req.TargetEntityId!.Value, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy Lesson gốc.");
            var hasTopicUpdate = p.TopicId.HasValue || p.TopicTitle is not null;
            if (hasTopicUpdate)
            {
                var topic = await ResolveLessonTopicAsync(p, cancellationToken);
                lesson.Topic = topic;
                if (topic is null) lesson.TopicId = null;
            }
            lesson.Title = p.Title;
            lesson.Description = p.Description;
            if (p.ImageUrl is not null) lesson.ImageUrl = string.IsNullOrWhiteSpace(p.ImageUrl) ? null : p.ImageUrl;
            if (p.AudioUrl is not null)
                lesson.AudioUrl = string.IsNullOrWhiteSpace(p.AudioUrl) ? null : p.AudioUrl;
            lesson.SortOrder = p.SortOrder;
            lesson.EstimatedMinutes = p.EstimatedMinutes > 0 ? p.EstimatedMinutes : 15;
            if (p.LearningObjectives is not null) lesson.LearningObjectives = p.LearningObjectives;
            if (p.SourceProvenance is not null) lesson.SourceProvenance = p.SourceProvenance;
            if (!string.IsNullOrWhiteSpace(p.PrerequisiteLessonTitle))
            {
                var prereqTitle = p.PrerequisiteLessonTitle.Trim();
                var prereq = await FindLessonAsync(prereqTitle, p.CertificateLevelId, cancellationToken)
                    ?? await unitOfWork.Lessons.FirstOrDefaultAsync(
                        l => !l.IsDeleted && l.Title == prereqTitle,
                        cancellationToken);
                if (prereq is not null && prereq.Id != lesson.Id)
                    lesson.PrerequisiteLessonId = prereq.Id;
            }
            lesson.CertificateLevelId = p.CertificateLevelId;
            lesson.LanguageSkillId = p.LanguageSkillId;
            if (isNew) await unitOfWork.Lessons.AddAsync(lesson, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            req.TargetEntityId = lesson.Id;
            if (hasTopicUpdate)
            {
                p.TopicId = lesson.TopicId;
                p.TopicTitle = lesson.Topic?.Title ?? string.Empty;
                req.PayloadJson = JsonSerializer.Serialize(p);
            }
            await LinkPendingLessonContentAsync(lesson, req, p, cancellationToken);
        }

        private async Task<Topic?> ResolveLessonTopicAsync(LessonImportPayloadResponse payload, CancellationToken ct)
        {
            var topics = unitOfWork.Topics.Query().Where(x => !x.IsDeleted);
            if (payload.TopicId is int id)
                return await topics.SingleOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new InvalidOperationException("Chủ đề đã chọn không còn tồn tại.");

            var title = ManualContentService.NormalizeTopicTitle(payload.TopicTitle);
            if (string.IsNullOrEmpty(title)) return null;

            var candidates = await topics
                .Where(x => x.CertificateLevelId == payload.CertificateLevelId || x.CertificateLevelId == null)
                .OrderByDescending(x => x.CertificateLevelId == payload.CertificateLevelId)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

            var match = candidates.Find(x => string.Equals(x.Title.Trim().Normalize(), title, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                if (!match.CertificateLevelId.HasValue) match.CertificateLevelId = payload.CertificateLevelId;
                if (!string.IsNullOrWhiteSpace(payload.StageCode)) match.StageCode = payload.StageCode;
                if (!string.IsNullOrWhiteSpace(payload.StageObjectives)) match.LearningObjectives = payload.StageObjectives;
                if (payload.StageSortOrder.HasValue) match.SortOrder = payload.StageSortOrder.Value;
                unitOfWork.Topics.Update(match);
                return match;
            }

            var nextSort = payload.StageSortOrder
                ?? ((candidates.Where(x => x.CertificateLevelId == payload.CertificateLevelId).Select(x => x.SortOrder).DefaultIfEmpty(0).Max()) + 1);
            return await unitOfWork.Topics.AddAsync(new Topic
            {
                Title = title,
                CertificateLevelId = payload.CertificateLevelId,
                StageCode = payload.StageCode,
                LearningObjectives = payload.StageObjectives,
                SortOrder = nextSort,
            }, ct);
        }

        private async Task ApplyPracticeExerciseApprovalAsync(ContentApprovalRequest req, CancellationToken cancellationToken)
        {
            var p = JsonSerializer.Deserialize<PracticeExerciseImportPayloadResponse>(req.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Payload Practice Exercise rỗng.");
            var scope = PracticeExerciseScopes.Normalize(p.Scope);
            var passingScore = p.PassingScorePercent > 0 ? p.PassingScorePercent : 70;

            Lesson? lesson = null;
            int? resolvedTopicId = p.TopicId;
            int resolvedLevelId = p.CertificateLevelId;

            if (scope == PracticeExerciseScopes.LessonQuickCheck)
            {
                lesson = await FindLessonAsync(p.LessonTitle, p.CertificateLevelId, cancellationToken)
                    ?? throw new InvalidOperationException($"Không tìm thấy Lesson '{p.LessonTitle}' ({p.CertificateLevelId}) để lưu Practice Exercise.");
                resolvedTopicId = lesson.TopicId;
                resolvedLevelId = lesson.CertificateLevelId;
            }
            else if (scope == PracticeExerciseScopes.GroupQuiz)
            {
                if (!resolvedTopicId.HasValue && !string.IsNullOrWhiteSpace(p.TopicTitle))
                {
                    var normalizedTopic = ManualContentService.NormalizeTopicTitle(p.TopicTitle);
                    var topicRow = await unitOfWork.Topics.Query()
                        .Where(t => !t.IsDeleted && (t.CertificateLevelId == resolvedLevelId || resolvedLevelId == 0 || t.CertificateLevelId == null))
                        .OrderByDescending(t => t.CertificateLevelId == resolvedLevelId)
                        .ToListAsync(cancellationToken);
                    var matchedTopic = topicRow.FirstOrDefault(t => string.Equals(t.Title.Trim().Normalize(), normalizedTopic, StringComparison.OrdinalIgnoreCase));
                    if (matchedTopic is null)
                        throw new InvalidOperationException($"Không tìm thấy Topic/Chặng '{p.TopicTitle}' để lưu GroupQuiz.");
                    resolvedTopicId = matchedTopic.Id;
                    resolvedLevelId = matchedTopic.CertificateLevelId ?? resolvedLevelId;
                }
                if (!resolvedTopicId.HasValue)
                    throw new InvalidOperationException("GroupQuiz yêu cầu TopicId hoặc TopicTitle.");
            }
            else if (resolvedLevelId <= 0)
            {
                throw new InvalidOperationException("StageAssessment yêu cầu CertificateLevelId.");
            }

            static List<PracticeQuestion> MapQuestions(IEnumerable<PracticeQuestionPayloadResponse> questions) => questions
                .OrderBy(q => q.SortOrder).Select(q => new PracticeQuestion
                {
                    QuestionText = q.QuestionText,
                    ImageUrl = q.ImageUrl,
                    Explanation = q.Explanation,
                    SortOrder = q.SortOrder,
                    Options = q.Options.OrderBy(o => o.SortOrder).Select(o => new PracticeQuestionOption
                    {
                        OptionText = o.OptionText,
                        ImageUrl = o.ImageUrl,
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
                    ImageUrl = p.ImageUrl,
                    SortOrder = p.SortOrder,
                    Scope = scope,
                    PassingScorePercent = passingScore,
                    LessonId = lesson?.Id,
                    TopicId = resolvedTopicId,
                    CertificateLevelId = resolvedLevelId > 0 ? resolvedLevelId : null,
                    LanguageSkillId = p.LanguageSkillId,
                    Questions = MapQuestions(p.Questions)
                }, cancellationToken);
                return;
            }

            var exercise = await unitOfWork.PracticeExercises.GetWithQuestionsAndOptionsAsync(req.TargetEntityId!.Value, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy Practice Exercise gốc.");
            exercise.Title = p.Title;
            exercise.Description = p.Description;
            if (p.ImageUrl is not null) exercise.ImageUrl = string.IsNullOrWhiteSpace(p.ImageUrl) ? null : p.ImageUrl;
            exercise.SortOrder = p.SortOrder;
            exercise.Scope = scope;
            exercise.PassingScorePercent = passingScore;
            exercise.LessonId = lesson?.Id;
            exercise.TopicId = resolvedTopicId;
            exercise.CertificateLevelId = resolvedLevelId > 0 ? resolvedLevelId : null;
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
                    && lessonRequest.BatchId != Guid.Empty && r.BatchId == lessonRequest.BatchId
                    && (r.Status == ContentApprovalStatus.Approved ||
                        (r.Status == ContentApprovalStatus.PendingReview && r.SubmittedByUserId == lessonRequest.SubmittedByUserId))
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

            foreach (var related in matching.Where(item => item.Status == ContentApprovalStatus.PendingReview)
                .OrderBy(item => item.Id))
            {
                var expectedPayload = related.PayloadJson;
                var locked = await unitOfWork.ContentApprovalRequests.GetByIdForUpdateAsync(related.Id, cancellationToken);
                if (locked is null || locked.IsDeleted || !IsReviewable(locked.Status) ||
                    locked.PayloadJson != expectedPayload)
                    throw new InvalidOperationException("Nội dung liên quan vừa thay đổi. Vui lòng tải lại lô duyệt.");
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
                    ImageUrl = p.ImageUrl,
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

                grammar.Title = p.Title;
                grammar.Structure = p.Structure;
                if (p.ImageUrl is not null) grammar.ImageUrl = string.IsNullOrWhiteSpace(p.ImageUrl) ? null : p.ImageUrl;
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

        private async Task<Dictionary<int, string>> GetLanguageSkillNamesAsync(CancellationToken ct)
        {
            if (_languageSkillNames is not null) return _languageSkillNames;

            _languageSkillNames = (await unitOfWork.LanguageSkills.Query()
                    .AsNoTracking()
                    .Where(skill => !skill.IsDeleted)
                    .Select(skill => new { skill.Id, skill.Name })
                    .ToListAsync(ct))
                .ToDictionary(skill => skill.Id, skill => skill.Name);
            return _languageSkillNames;
        }

        private async Task<Dictionary<int, (string Code, bool IsDeleted)>> GetCertificateLevelsAsync(CancellationToken ct)
        {
            if (_certificateLevels is not null) return _certificateLevels;

            _certificateLevels = (await unitOfWork.CertificateLevels.Query()
                    .AsNoTracking()
                    .Select(level => new { level.Id, level.Code, level.IsDeleted })
                    .ToListAsync(ct))
                .ToDictionary(level => level.Id, level => (level.Code, level.IsDeleted));
            return _certificateLevels;
        }

        private async Task<Dictionary<string, List<int>>> GetCertificateSectionIdsByCodeAsync(CancellationToken ct)
        {
            if (_certificateSectionIdsByCode is not null) return _certificateSectionIdsByCode;

            var sections = await unitOfWork.CertificateSections.Query()
                .AsNoTracking()
                .Where(section => !section.IsDeleted)
                .Select(section => new { section.Id, section.Code })
                .ToListAsync(ct);
            _certificateSectionIdsByCode = sections
                .GroupBy(section => section.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(section => section.Id).ToList(),
                    StringComparer.OrdinalIgnoreCase);
            return _certificateSectionIdsByCode;
        }

        private async Task ApplyMockTestApprovalAsync(ContentApprovalRequest req, CancellationToken cancellationToken)
        {
            _ = audioTranscriptions;
            var p = JsonSerializer.Deserialize<MockTestImportPayloadResponse>(req.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Payload MockTest rỗng.");

            // Validate hết TRƯỚC khi add/sửa entity nào: trong ReviewBatchAsync, entity đã add mà sau đó throw
            // vẫn nằm trong ChangeTracker và bị SaveChanges cuối lô ghi xuống.
            if (req.Action == ImportRowAction.New || p.Sections is { Count: > 0 })
            {
                if (p.Sections is not { Count: > 0 })
                    throw new InvalidOperationException("Đề thi chưa có phần (section) nào.");

                var skillNames = await GetLanguageSkillNamesAsync(cancellationToken);
                var levels = await GetCertificateLevelsAsync(cancellationToken);
                if (!levels.TryGetValue(p.CertificateLevelId, out var level) || level.IsDeleted)
                    throw new InvalidOperationException("Cấp độ của đề thi không tồn tại hoặc đã bị xóa.");
                foreach (var s in p.Sections)
                {
                    if (req.Action == ImportRowAction.New)
                    {
                        if (s.LanguageSkillId <= 0)
                            throw new InvalidOperationException($"Phần '{s.Title}' thiếu LanguageSkillId.");
                        if (string.IsNullOrWhiteSpace(s.CertificateSectionCode))
                            throw new InvalidOperationException($"Phần '{s.Title}' thiếu CertificateSectionCode.");
                    }

                }
                var validationError = ExcelContentImportService.ValidateMockTestPayload(req.PayloadJson!, skillNames,
                    new Dictionary<int, string> { [p.CertificateLevelId] = level.Code });
                if (validationError is not null) throw new InvalidOperationException(validationError);
            }

            if (req.Action == ImportRowAction.New)
            {
                // Tra FK từ Code (admin tạo sẵn CertificateSection) TRƯỚC khi tạo entity,
                // để lỗi thì throw sớm, không để entity dở dang trong ChangeTracker.
                var codes = p.Sections!
                    .Select(s => s.CertificateSectionCode!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var sectionIdsByCode = await GetCertificateSectionIdsByCodeAsync(cancellationToken);
                var duplicated = codes
                    .Where(code => sectionIdsByCode.TryGetValue(code, out var ids) && ids.Count > 1)
                    .ToList();
                if (duplicated.Count > 0)
                    throw new InvalidOperationException(
                        $"Code CertificateSection bị trùng trong DB: {string.Join(", ", duplicated)}.");

                var missing = codes
                    .Where(code => !sectionIdsByCode.ContainsKey(code))
                    .ToList();
                if (missing.Count > 0)
                    throw new InvalidOperationException(
                        $"Không tìm thấy CertificateSection có Code: {string.Join(", ", missing)}. Admin cần tạo sẵn trước khi duyệt.");

                var sectionIdByCode = codes.ToDictionary(
                    code => code,
                    code => sectionIdsByCode[code][0],
                    StringComparer.OrdinalIgnoreCase);

                var test = new MockTest
                {
                    Title = p.Title,
                    Description = p.Description,
                    TimeLimitMinutes = p.TimeLimitMinutes,
                    CertificateLevelId = p.CertificateLevelId,
                    PublishStatus = ContentStatus.Published,
                    PublishedAt = DateTime.UtcNow,
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
                                ImageUrl = o.ImageUrl,
                                IsCorrect = o.IsCorrect,
                                SortOrder = o.SortOrder
                            }).ToList()
                        }).ToList()
                    }).ToList()
                };

                // Transcription is asynchronous; do not enqueue/write ContentAudioTranscriptions inside approval tx.
                await unitOfWork.MockTests.AddAsync(test, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                req.TargetEntityId = test.Id;
            }
            else
            {
                var test = await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(req.TargetEntityId!.Value, cancellationToken)
                    ?? throw new InvalidOperationException("Không tìm thấy đề thi gốc.");

                test.Title = p.Title;
                test.Description = p.Description;
                test.TimeLimitMinutes = p.TimeLimitMinutes;
                test.CertificateLevelId = p.CertificateLevelId;
                test.PublishStatus = ContentStatus.Published;
                test.PublishedAt ??= DateTime.UtcNow;

                if (p.Sections is { Count: > 0 })
                {
                    var sectionCodes = p.Sections.Select(s => s.CertificateSectionCode?.Trim())
                        .Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var sectionIdsByCode = await GetCertificateSectionIdsByCodeAsync(cancellationToken);
                    var missingCodes = sectionCodes.Where(code => !sectionIdsByCode.ContainsKey(code)).ToList();
                    if (missingCodes.Count > 0)
                        throw new InvalidOperationException($"Không tìm thấy CertificateSection: {string.Join(", ", missingCodes)}.");
                    var sectionIdByCode = sectionCodes.ToDictionary(
                        code => code,
                        code => sectionIdsByCode[code][0],
                        StringComparer.OrdinalIgnoreCase);

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
                                ImageUrl = option.ImageUrl,
                                IsCorrect = option.IsCorrect,
                                SortOrder = option.SortOrder
                            }).ToList()
                        }).ToList()
                    }).ToList();
                    (test.Sections ??= []).AddRange(replacementSections);
                }

                // Transcription is asynchronous; do not enqueue/write ContentAudioTranscriptions inside approval tx.
                unitOfWork.MockTests.Update(test);
            }
        }
        #endregion

        private static string NormalizeMockQuestionType(string? questionType) =>
            JlptMockQuestionFormats.NormalizeQuestionType(questionType)
                ?? throw new InvalidOperationException($"Dạng câu MockTest không hỗ trợ: {questionType}.");
    }
}
