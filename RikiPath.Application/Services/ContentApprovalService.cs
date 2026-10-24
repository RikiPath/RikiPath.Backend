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
        IEnumerable<IApprovalDiffBuilder> diffBuilders) : IContentApprovalService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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

                return ApiResponse<List<PendingApprovalItemResponse>>.Success(responseList);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<PendingApprovalItemResponse>>.Fail($"Không thể lấy danh sách chờ duyệt: {ex.Message}");
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

                return ApiResponse<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail($"Duyệt yêu cầu thất bại: {ex.Message}");
            }
        }

        public async Task<ApiResponse<BulkReviewResponse>> ReviewBatchAsync(
               BulkReviewRequest request, CancellationToken cancellationToken)
        {
            if (request.Items is not { Count: > 0 })
                return ApiResponse<BulkReviewResponse>.Fail("Chưa chọn yêu cầu nào để duyệt.");

            var result = new BulkReviewResponse();
            var seen = new HashSet<int>();

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

            if (result.SuccessCount == 0 && result.Failures.Count > 0)
                return ApiResponse<BulkReviewResponse>.Fail(
                    string.Join("; ", result.Failures.Select(f => $"#{f.RequestId}: {f.Reason}")));

            return ApiResponse<BulkReviewResponse>.Success(result);
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
                        if (q.Options.Count < 2)
                            throw new InvalidOperationException($"Câu hỏi '{q.QuestionText}' phải có ít nhất 2 đáp án.");
                        if (q.Options.Count(o => o.IsCorrect) != 1)
                            throw new InvalidOperationException($"Câu hỏi '{q.QuestionText}' phải có đúng 1 đáp án đúng.");
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
                var test = await unitOfWork.MockTests.GetByIdAsync(req.TargetEntityId!.Value, cancellationToken)
                    ?? throw new InvalidOperationException("Không tìm thấy đề thi gốc.");

                test.Title = p.Title;
                test.Description = p.Description;
                test.TimeLimitMinutes = p.TimeLimitMinutes;
                test.CertificateLevelId = p.CertificateLevelId;

                unitOfWork.MockTests.Update(test);
            }
        }
        #endregion
    }
}