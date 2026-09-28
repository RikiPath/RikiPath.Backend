using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
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
    // dung (Lesson/Kanji/Vocabulary/GrammarPattern/MockTest) có bộ method riêng, code
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

                if (request.EntityType is ContentEntityType.Lesson or ContentEntityType.MockTest)
                    return ApiResponse<BulkImportResultResponse>.Fail(
                        "Loại nội dung này không hỗ trợ import hàng loạt, hãy tạo thủ công.");

                if (request.EntityType == ContentEntityType.MockQuestion)
                {
                    if (request.TargetMockTestSectionId is null)
                        return ApiResponse<BulkImportResultResponse>.Fail("Thiếu TargetMockTestSectionId để import câu hỏi.");
                    var section = await unitOfWork.MockTestSections.GetByIdAsync(request.TargetMockTestSectionId.Value, cancellationToken);
                    var test = section is null ? null : await unitOfWork.MockTests.GetByIdAsync(section.MockTestId, cancellationToken);
                    if (test is null || test.IsDeleted || test.ContentAuthorId != authorId || test.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                        return ApiResponse<BulkImportResultResponse>.Fail("Bạn chỉ import câu hỏi vào bài thi Draft/Rejected do mình tạo.");
                }

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
                            case ContentEntityType.Kanji:
                                await unitOfWork.Kanjis.AddAsync(new Kanji
                                {
                                    Character = GetRequired(row, "Character"),
                                    Meaning = GetRequired(row, "Meaning"),
                                    SinoVietnamese = GetOptional(row, "SinoVietnamese"),
                                    OnYomi = GetOptional(row, "OnYomi"),
                                    KunYomi = GetOptional(row, "KunYomi"),
                                    StrokeCount = int.Parse(GetRequired(row, "StrokeCount")),
                                    CertificateLevelId = request.CertificateLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                }, cancellationToken);
                                break;

                            case ContentEntityType.Vocabulary:
                                await unitOfWork.Vocabularies.AddAsync(new Vocabulary
                                {
                                    Word = GetRequired(row, "Word"),
                                    Reading = GetRequired(row, "Reading"),
                                    Meaning = GetRequired(row, "Meaning"),
                                    ExampleSentence = GetOptional(row, "ExampleSentence"),
                                    ExampleSentenceMeaning = GetOptional(row, "ExampleSentenceMeaning"),
                                    CertificateLevelId = request.CertificateLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                }, cancellationToken);
                                break;

                            case ContentEntityType.GrammarPattern:
                                await unitOfWork.GrammarPatterns.AddAsync(new GrammarPattern
                                {
                                    Title = GetRequired(row, "Title"),
                                    Structure = GetRequired(row, "Structure"),
                                    UsageNotes = GetOptional(row, "UsageNotes"),
                                    ExampleSentence = GetOptional(row, "ExampleSentence"),
                                    ExampleSentenceMeaning = GetOptional(row, "ExampleSentenceMeaning"),
                                    CertificateLevelId = request.CertificateLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                }, cancellationToken);
                                break;

                            case ContentEntityType.MockQuestion:
                                if (request.TargetMockTestSectionId is null)
                                {
                                    errors.Add($"Dòng {row.RowNumber}: thiếu TargetMockTestSectionId để import câu hỏi.");
                                    continue;
                                }

                                await unitOfWork.MockQuestions.AddAsync(new MockQuestion
                                {
                                    QuestionText = GetRequired(row, "QuestionText"),
                                    Explanation = GetOptional(row, "Explanation"),
                                    AudioUrl = GetOptional(row, "AudioUrl"),
                                    ImageUrl = GetOptional(row, "ImageUrl"),
                                    MockTestSectionId = request.TargetMockTestSectionId.Value,
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
                ContentEntityType.Kanji => SubmitKanjiForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.Vocabulary => SubmitVocabularyForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.GrammarPattern => SubmitGrammarForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.MockTest => SubmitMockTestForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.PracticeExercise => SubmitPracticeExerciseForReviewAsync(authorId, entityId, cancellationToken),
                _ => Task.FromResult(ApiResponse<ContentReviewStatusResponse>.Fail("Loại nội dung không hỗ trợ gửi duyệt."))
            };
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitLessonForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.Lessons.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
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
                var entity = await unitOfWork.Kanjis.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.Kanjis.Update(entity);
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
                var entity = await unitOfWork.Vocabularies.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.Vocabularies.Update(entity);
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
                var entity = await unitOfWork.GrammarPatterns.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.GrammarPatterns.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapGrammarToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitMockTestForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.MockTests.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.MockTests.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapMockTestToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        // ================= GetMyContentAsync =================

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitPracticeExerciseForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.PracticeExercises.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");
                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;
                unitOfWork.PracticeExercises.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<ContentReviewStatusResponse>.Success(MapPracticeExerciseToResponse(entity));
            }
            catch (Exception ex) { return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}"); }
        }

        public async Task<ApiResponse<List<ContentReviewStatusResponse>>> GetMyContentAsync(
            ContentEntityType entityType, CancellationToken cancellationToken)
        {
            try
            {
                var authorId = claimService.GetUserClaim().Id;

                List<ContentReviewStatusResponse> result = entityType switch
                {
                    ContentEntityType.Lesson => (await unitOfWork.Lessons.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapLessonToResponse).ToList(),
                    ContentEntityType.Kanji => (await unitOfWork.Kanjis.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapKanjiToResponse).ToList(),
                    ContentEntityType.Vocabulary => (await unitOfWork.Vocabularies.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapVocabularyToResponse).ToList(),
                    ContentEntityType.GrammarPattern => (await unitOfWork.GrammarPatterns.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapGrammarToResponse).ToList(),
                    ContentEntityType.MockTest => (await unitOfWork.MockTests.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapMockTestToResponse).ToList(),
                    ContentEntityType.PracticeExercise => (await unitOfWork.PracticeExercises.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapPracticeExerciseToResponse).ToList(),
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
                    ContentEntityType.Lesson => (await unitOfWork.Lessons.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapLessonToResponse).ToList(),
                    ContentEntityType.Kanji => (await unitOfWork.Kanjis.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapKanjiToResponse).ToList(),
                    ContentEntityType.Vocabulary => (await unitOfWork.Vocabularies.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapVocabularyToResponse).ToList(),
                    ContentEntityType.GrammarPattern => (await unitOfWork.GrammarPatterns.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapGrammarToResponse).ToList(),
                    ContentEntityType.MockTest => (await unitOfWork.MockTests.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapMockTestToResponse).ToList(),
                    ContentEntityType.PracticeExercise => (await unitOfWork.PracticeExercises.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapPracticeExerciseToResponse).ToList(),
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
                ContentEntityType.Kanji => ReviewKanjiAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.Vocabulary => ReviewVocabularyAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.GrammarPattern => ReviewGrammarAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.MockTest => ReviewMockTestAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.PracticeExercise => ReviewPracticeExerciseAsync(adminId, entityId, request, cancellationToken),
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
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
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
                var entity = await unitOfWork.Kanjis.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.Kanjis.Update(entity);
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
                var entity = await unitOfWork.Vocabularies.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.Vocabularies.Update(entity);
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
                var entity = await unitOfWork.GrammarPatterns.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.GrammarPatterns.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapGrammarToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewMockTestAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.MockTests.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.MockTests.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapMockTestToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewPracticeExerciseAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.PracticeExercises.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.Approve ? null : request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;
                unitOfWork.PracticeExercises.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<ContentReviewStatusResponse>.Success(MapPracticeExerciseToResponse(entity));
            }
            catch (Exception ex) { return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}"); }
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

        private static ContentReviewStatusResponse MapKanjiToResponse(Kanji e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.Kanji,
            Title = e.Character,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapVocabularyToResponse(Vocabulary e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.Vocabulary,
            Title = e.Word,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapGrammarToResponse(GrammarPattern e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.GrammarPattern,
            Title = e.Title,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapMockTestToResponse(MockTest e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.MockTest,
            Title = e.Title,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapPracticeExerciseToResponse(PracticeExercise e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.PracticeExercise,
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
