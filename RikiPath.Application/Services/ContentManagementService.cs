using RikiPath.Domain.Entities;
using Domain.Enums;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.IClients;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;

namespace RikiPath.Application.Services
{
    // Đã bỏ IReviewableContent + generic dispatch qua Expression.Property. Đổi lại: mỗi loại nội
    // dung (Lesson/KanjiEntry/VocabularyEntry/GrammarPoint/PracticeTest) có bộ method riêng, code
    // dài hơn nhưng thẳng, không còn "ảo thuật" generic/reflection nào - dễ đọc, dễ debug độc lập
    // từng loại, không có rủi ro EF Core dịch sai LINQ expression qua interface.
    //
    // GHI CHÚ: entity không còn field "ReviewedById" (int FK) - đã đổi sang "ReviewedByName"
    // (string, lưu tên hiển thị của admin đã duyệt ngay tại thời điểm duyệt) - áp dụng cho cả 5 loại.
    public class ContentManagementService(IUnitOfWork unitOfWork, IExcelParser excelParser, IClaimService claimService)
        : IContentManagementService
    {
        public async Task<ApiResponse<BulkImportResultResponse>> BulkImportAsync(
            BulkImportRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var authorId = claimService.GetUserClaim().Id;

                if (request.EntityType is ContentEntityType.Lesson or ContentEntityType.PracticeTest)
                    return ApiResponse<BulkImportResultResponse>.Fail(
                        "Loại nội dung này không hỗ trợ import hàng loạt, hãy tạo thủ công.");

                var parseResult = await excelParser.ParseAsync(request.FileStream, request.EntityType, cancellationToken);
                if (!parseResult.Success && parseResult.Rows.Count == 0)
                    return ApiResponse<BulkImportResultResponse>.Fail(
                        "Không đọc được file, vui lòng kiểm tra định dạng.", errors: parseResult.Errors);

                var errors = new List<string>(parseResult.Errors);
                var successCount = 0;

                foreach (var row in parseResult.Rows)
                {
                    try
                    {
                        switch (request.EntityType)
                        {
                            case ContentEntityType.KanjiEntry:
                                await unitOfWork.KanjiEntries.AddAsync(new KanjiEntry
                                {
                                    Character = GetRequired(row, "Character"),
                                    Meaning = GetRequired(row, "Meaning"),
                                    SinoVietnamese = GetOptional(row, "SinoVietnamese"),
                                    OnYomi = GetOptional(row, "OnYomi"),
                                    KunYomi = GetOptional(row, "KunYomi"),
                                    StrokeCount = int.Parse(GetRequired(row, "StrokeCount")),
                                    CertificationLevelId = request.CertificationLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                }, cancellationToken);
                                break;

                            case ContentEntityType.VocabularyEntry:
                                await unitOfWork.VocabularyEntries.AddAsync(new VocabularyEntry
                                {
                                    Word = GetRequired(row, "Word"),
                                    Reading = GetRequired(row, "Reading"),
                                    Meaning = GetRequired(row, "Meaning"),
                                    ExampleSentence = GetOptional(row, "ExampleSentence"),
                                    ExampleSentenceMeaning = GetOptional(row, "ExampleSentenceMeaning"),
                                    CertificationLevelId = request.CertificationLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                }, cancellationToken);
                                break;

                            case ContentEntityType.GrammarPoint:
                                await unitOfWork.GrammarPoints.AddAsync(new GrammarPoint
                                {
                                    Title = GetRequired(row, "Title"),
                                    Structure = GetRequired(row, "Structure"),
                                    UsageNotes = GetOptional(row, "UsageNotes"),
                                    ExampleSentence = GetOptional(row, "ExampleSentence"),
                                    ExampleSentenceMeaning = GetOptional(row, "ExampleSentenceMeaning"),
                                    CertificationLevelId = request.CertificationLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                }, cancellationToken);
                                break;

                            case ContentEntityType.PracticeQuestion:
                                if (request.TargetPracticeTestSectionId is null)
                                {
                                    errors.Add($"Dòng {row.RowNumber}: thiếu TargetPracticeTestSectionId để import câu hỏi.");
                                    continue;
                                }

                                await unitOfWork.PracticeQuestions.AddAsync(new PracticeQuestion
                                {
                                    QuestionText = GetRequired(row, "QuestionText"),
                                    Explanation = GetOptional(row, "Explanation"),
                                    AudioUrl = GetOptional(row, "AudioUrl"),
                                    ImageUrl = GetOptional(row, "ImageUrl"),
                                    PracticeTestSectionId = request.TargetPracticeTestSectionId.Value,
                                    SortOrder = row.RowNumber
                                }, cancellationToken);
                                break;

                            default:
                                errors.Add($"Dòng {row.RowNumber}: loại nội dung không được hỗ trợ import hàng loạt.");
                                continue;
                        }

                        successCount++;
                    }
                    catch (Exception rowEx)
                    {
                        errors.Add($"Dòng {row.RowNumber}: {rowEx.Message}");
                    }
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<BulkImportResultResponse>.Success(
                    new BulkImportResultResponse
                    {
                        TotalRows = parseResult.Rows.Count,
                        SuccessCount = successCount,
                        FailedCount = parseResult.Rows.Count - successCount,
                        Errors = errors
                    });
            }
            catch (Exception ex)
            {
                return ApiResponse<BulkImportResultResponse>.Fail($"Import thất bại: {ex.Message}");
            }
        }

        // ================= SubmitForReviewAsync =================

        public Task<ApiResponse<ContentReviewStatusResponse>> SubmitForReviewAsync(
            ContentEntityType entityType, int entityId, CancellationToken cancellationToken)
        {
            var authorId = claimService.GetUserClaim().Id;
            return entityType switch
            {
                ContentEntityType.Lesson => SubmitLessonForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.KanjiEntry => SubmitKanjiForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.VocabularyEntry => SubmitVocabularyForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.GrammarPoint => SubmitGrammarForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.PracticeTest => SubmitPracticeTestForReviewAsync(authorId, entityId, cancellationToken),
                _ => Task.FromResult(ApiResponse<ContentReviewStatusResponse>.Fail("Loại nội dung không hỗ trợ gửi duyệt."))
            };
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitLessonForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.Lessons.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.Lessons.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapLessonToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitKanjiForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.KanjiEntries.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.KanjiEntries.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapKanjiToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitVocabularyForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.VocabularyEntries.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.VocabularyEntries.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapVocabularyToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitGrammarForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.GrammarPoints.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.GrammarPoints.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapGrammarToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitPracticeTestForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.PracticeTests.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.PracticeTests.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapPracticeTestToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        // ================= GetMyContentAsync =================

        public async Task<ApiResponse<List<ContentReviewStatusResponse>>> GetMyContentAsync(
            ContentEntityType entityType, CancellationToken cancellationToken)
        {
            try
            {
                var authorId = claimService.GetUserClaim().Id;

                List<ContentReviewStatusResponse> result = entityType switch
                {
                    ContentEntityType.Lesson => (await unitOfWork.Lessons.FindAsync(e => e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapLessonToResponse).ToList(),
                    ContentEntityType.KanjiEntry => (await unitOfWork.KanjiEntries.FindAsync(e => e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapKanjiToResponse).ToList(),
                    ContentEntityType.VocabularyEntry => (await unitOfWork.VocabularyEntries.FindAsync(e => e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapVocabularyToResponse).ToList(),
                    ContentEntityType.GrammarPoint => (await unitOfWork.GrammarPoints.FindAsync(e => e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapGrammarToResponse).ToList(),
                    ContentEntityType.PracticeTest => (await unitOfWork.PracticeTests.FindAsync(e => e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapPracticeTestToResponse).ToList(),
                    _ => throw new InvalidOperationException("Loại nội dung không hợp lệ.")
                };

                return ApiResponse<List<ContentReviewStatusResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ContentReviewStatusResponse>>.Fail($"Không thể tải danh sách: {ex.Message}");
            }
        }

        // ================= GetPendingReviewAsync =================

        public async Task<ApiResponse<List<ContentReviewStatusResponse>>> GetPendingReviewAsync(
            ContentEntityType entityType, CancellationToken cancellationToken)
        {
            try
            {
                List<ContentReviewStatusResponse> result = entityType switch
                {
                    ContentEntityType.Lesson => (await unitOfWork.Lessons.FindAsync(e => e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapLessonToResponse).ToList(),
                    ContentEntityType.KanjiEntry => (await unitOfWork.KanjiEntries.FindAsync(e => e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapKanjiToResponse).ToList(),
                    ContentEntityType.VocabularyEntry => (await unitOfWork.VocabularyEntries.FindAsync(e => e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapVocabularyToResponse).ToList(),
                    ContentEntityType.GrammarPoint => (await unitOfWork.GrammarPoints.FindAsync(e => e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapGrammarToResponse).ToList(),
                    ContentEntityType.PracticeTest => (await unitOfWork.PracticeTests.FindAsync(e => e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapPracticeTestToResponse).ToList(),
                    _ => throw new InvalidOperationException("Loại nội dung không hợp lệ.")
                };

                return ApiResponse<List<ContentReviewStatusResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ContentReviewStatusResponse>>.Fail($"Không thể tải danh sách chờ duyệt: {ex.Message}");
            }
        }

        // ================= ReviewAsync =================

        public Task<ApiResponse<ContentReviewStatusResponse>> ReviewAsync(
            ContentEntityType entityType, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            var adminId = claimService.GetUserClaim().Id;
            return entityType switch
            {
                ContentEntityType.Lesson => ReviewLessonAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.KanjiEntry => ReviewKanjiAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.VocabularyEntry => ReviewVocabularyAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.GrammarPoint => ReviewGrammarAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.PracticeTest => ReviewPracticeTestAsync(adminId, entityId, request, cancellationToken),
                _ => Task.FromResult(ApiResponse<ContentReviewStatusResponse>.Fail("Loại nội dung không hỗ trợ duyệt."))
            };
        }

        // Lấy tên hiển thị của admin đã duyệt - dùng chung cho cả 5 loại (private helper, không
        // phải interface nên không vi phạm yêu cầu "bỏ IReviewableContent").
        // Sửa lại lỗi precedence: "a + " " + b ?? fallback" KHÔNG hoạt động như mong đợi vì
        // string + null vẫn ra chuỗi non-null, nên "?? fallback" không bao giờ được kích hoạt.
        private async Task<string> GetReviewerNameAsync(int adminId, CancellationToken cancellationToken)
        {
            var admin = await unitOfWork.UserAccounts.GetByIdAsync(adminId, cancellationToken);
            return admin is null ? $"Admin #{adminId}" : $"{admin.FirstName} {admin.LastName}".Trim();
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewLessonAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.Lessons.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.Lessons.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapLessonToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewKanjiAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.KanjiEntries.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.KanjiEntries.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapKanjiToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewVocabularyAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.VocabularyEntries.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.VocabularyEntries.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapVocabularyToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewGrammarAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.GrammarPoints.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.GrammarPoints.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapGrammarToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewPracticeTestAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.PracticeTests.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.PracticeTests.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapPracticeTestToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        // ================= Mapping (1 method riêng / loại, không qua interface) =================

        private static ContentReviewStatusResponse MapLessonToResponse(Lesson e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.Lesson,
            Title = e.Title,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapKanjiToResponse(KanjiEntry e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.KanjiEntry,
            Title = e.Character,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapVocabularyToResponse(VocabularyEntry e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.VocabularyEntry,
            Title = e.Word,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapGrammarToResponse(GrammarPoint e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.GrammarPoint,
            Title = e.Title,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapPracticeTestToResponse(PracticeTest e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.PracticeTest,
            Title = e.Title,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static string GetRequired(ParsedContentRow row, string key)
            => row.Fields.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v
                : throw new InvalidOperationException($"Thiếu trường bắt buộc '{key}'.");

        private static string? GetOptional(ParsedContentRow row, string key)
            => row.Fields.TryGetValue(key, out var v) ? v : null;
    }
}