using RikiPath.Domain.Entities;
using Domain.Enums;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.IClients;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Entities;
using System.Linq.Expressions;

namespace RikiPath.Application.Services
{
    public class ContentManagementService(IUnitOfWork unitOfWork, IExcelParser excelParser)
         : IContentManagementService
    {
        public async Task<ApiResponse<BulkImportResultResponse>> BulkImportAsync(
            int authorId, BulkImportRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.EntityType is ContentEntityType.Course or ContentEntityType.PracticeTest)
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
                                    JlptLevelId = request.JlptLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                });
                                break;

                            case ContentEntityType.VocabularyEntry:
                                await unitOfWork.VocabularyEntries.AddAsync(new VocabularyEntry
                                {
                                    Word = GetRequired(row, "Word"),
                                    Reading = GetRequired(row, "Reading"),
                                    Meaning = GetRequired(row, "Meaning"),
                                    ExampleSentence = GetOptional(row, "ExampleSentence"),
                                    ExampleSentenceMeaning = GetOptional(row, "ExampleSentenceMeaning"),
                                    JlptLevelId = request.JlptLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                });
                                break;

                            case ContentEntityType.GrammarPoint:
                                await unitOfWork.GrammarPoints.AddAsync(new GrammarPoint
                                {
                                    Title = GetRequired(row, "Title"),
                                    Structure = GetRequired(row, "Structure"),
                                    UsageNotes = GetOptional(row, "UsageNotes"),
                                    ExampleSentence = GetOptional(row, "ExampleSentence"),
                                    ExampleSentenceMeaning = GetOptional(row, "ExampleSentenceMeaning"),
                                    JlptLevelId = request.JlptLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                });
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
                                });
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

                await unitOfWork.SaveChangesAsync();

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

        public Task<ApiResponse<ContentReviewStatusResponse>> SubmitForReviewAsync(
            int authorId, ContentEntityType entityType, int entityId, CancellationToken cancellationToken) => entityType switch
            {
                ContentEntityType.Course => SubmitForReviewGenericAsync(unitOfWork.Courses, authorId, entityId, entityType, cancellationToken),
                ContentEntityType.KanjiEntry => SubmitForReviewGenericAsync(unitOfWork.KanjiEntries, authorId, entityId, entityType, cancellationToken),
                ContentEntityType.VocabularyEntry => SubmitForReviewGenericAsync(unitOfWork.VocabularyEntries, authorId, entityId, entityType, cancellationToken),
                ContentEntityType.GrammarPoint => SubmitForReviewGenericAsync(unitOfWork.GrammarPoints, authorId, entityId, entityType, cancellationToken),
                ContentEntityType.PracticeTest => SubmitForReviewGenericAsync(unitOfWork.PracticeTests, authorId, entityId, entityType, cancellationToken),
                _ => Task.FromResult(ApiResponse<ContentReviewStatusResponse>.Fail("Loại nội dung không hỗ trợ gửi duyệt."))
            };

        public Task<ApiResponse<List<ContentReviewStatusResponse>>> GetMyContentAsync(
            int authorId, ContentEntityType entityType, CancellationToken cancellationToken) => entityType switch
            {
                ContentEntityType.Course => GetByPredicateAsync(unitOfWork.Courses, entityType, ByAuthor<Course>(authorId)),
                ContentEntityType.KanjiEntry => GetByPredicateAsync(unitOfWork.KanjiEntries, entityType, ByAuthor<KanjiEntry>(authorId)),
                ContentEntityType.VocabularyEntry => GetByPredicateAsync(unitOfWork.VocabularyEntries, entityType, ByAuthor<VocabularyEntry>(authorId)),
                ContentEntityType.GrammarPoint => GetByPredicateAsync(unitOfWork.GrammarPoints, entityType, ByAuthor<GrammarPoint>(authorId)),
                ContentEntityType.PracticeTest => GetByPredicateAsync(unitOfWork.PracticeTests, entityType, ByAuthor<PracticeTest>(authorId)),
                _ => Task.FromResult(ApiResponse<List<ContentReviewStatusResponse>>.Fail("Loại nội dung không hợp lệ."))
            };

        public Task<ApiResponse<List<ContentReviewStatusResponse>>> GetPendingReviewAsync(
            ContentEntityType entityType, CancellationToken cancellationToken) => entityType switch
            {
                ContentEntityType.Course => GetByPredicateAsync(unitOfWork.Courses, entityType, ByStatus<Course>(ContentStatus.PendingReview)),
                ContentEntityType.KanjiEntry => GetByPredicateAsync(unitOfWork.KanjiEntries, entityType, ByStatus<KanjiEntry>(ContentStatus.PendingReview)),
                ContentEntityType.VocabularyEntry => GetByPredicateAsync(unitOfWork.VocabularyEntries, entityType, ByStatus<VocabularyEntry>(ContentStatus.PendingReview)),
                ContentEntityType.GrammarPoint => GetByPredicateAsync(unitOfWork.GrammarPoints, entityType, ByStatus<GrammarPoint>(ContentStatus.PendingReview)),
                ContentEntityType.PracticeTest => GetByPredicateAsync(unitOfWork.PracticeTests, entityType, ByStatus<PracticeTest>(ContentStatus.PendingReview)),
                _ => Task.FromResult(ApiResponse<List<ContentReviewStatusResponse>>.Fail("Loại nội dung không hợp lệ."))
            };

        public Task<ApiResponse<ContentReviewStatusResponse>> ReviewAsync(
            int adminId, ContentEntityType entityType, int entityId, ReviewContentRequest request, CancellationToken cancellationToken) => entityType switch
            {
                ContentEntityType.Course => ReviewGenericAsync(unitOfWork.Courses, adminId, entityId, entityType, request, cancellationToken),
                ContentEntityType.KanjiEntry => ReviewGenericAsync(unitOfWork.KanjiEntries, adminId, entityId, entityType, request, cancellationToken),
                ContentEntityType.VocabularyEntry => ReviewGenericAsync(unitOfWork.VocabularyEntries, adminId, entityId, entityType, request, cancellationToken),
                ContentEntityType.GrammarPoint => ReviewGenericAsync(unitOfWork.GrammarPoints, adminId, entityId, entityType, request, cancellationToken),
                ContentEntityType.PracticeTest => ReviewGenericAsync(unitOfWork.PracticeTests, adminId, entityId, entityType, request, cancellationToken),
                _ => Task.FromResult(ApiResponse<ContentReviewStatusResponse>.Fail("Loại nội dung không hỗ trợ duyệt."))
            };

        // ------------------- Generic helpers (DRY cho cả 5 loại entity) -------------------
        // Nhận thẳng repository cụ thể (unitOfWork.Courses, unitOfWork.KanjiEntries, ...) qua
        // tham số kiểu IGenericRepository<T> - compiler suy luận T từ interface base mà
        // ICourseRepository/IKanjiEntryRepository/... đều kế thừa.

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitForReviewGenericAsync<T>(
            IGenericRepository<T> repository, int authorId, int entityId, ContentEntityType entityType, CancellationToken cancellationToken)
            where T : class, IReviewableContent
        {
            var entity = await repository.GetByIdAsync(entityId);
            if (entity is null || entity.ContentAuthorId != authorId)
                return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

            if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

            entity.Status = ContentStatus.PendingReview;
            entity.ReviewNote = null;
            entity.ReviewedDate = null;

            repository.Update(entity);
            await unitOfWork.SaveChangesAsync();

            return ApiResponse<ContentReviewStatusResponse>.Success(MapToStatusResponse(entityType, entity));
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewGenericAsync<T>(
            IGenericRepository<T> repository, int adminId, int entityId, ContentEntityType entityType, ReviewContentRequest request, CancellationToken cancellationToken)
            where T : class, IReviewableContent
        {
            var entity = await repository.GetByIdAsync(entityId);
            if (entity is null || entity.Status != ContentStatus.PendingReview)
                return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

            entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
            entity.ReviewedById = adminId;
            entity.ReviewNote = request.ReviewNote;
            entity.ReviewedDate = DateTime.UtcNow;

            repository.Update(entity);
            await unitOfWork.SaveChangesAsync();

            return ApiResponse<ContentReviewStatusResponse>.Success(
                MapToStatusResponse(entityType, entity));
        }

        private static async Task<ApiResponse<List<ContentReviewStatusResponse>>> GetByPredicateAsync<T>(
            IGenericRepository<T> repository, ContentEntityType entityType, Expression<Func<T, bool>> predicate)
            where T : class, IReviewableContent
        {
            try
            {
                var items = await repository.FindAsync(predicate);
                var result = items.Select(e => MapToStatusResponse(entityType, e)).ToList();
                return ApiResponse<List<ContentReviewStatusResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ContentReviewStatusResponse>>.Fail($"Không thể tải danh sách: {ex.Message}");
            }
        }

        // Xây expression tree trỏ trực tiếp vào property CỦA CLASS CỤ THỂ (không phải qua interface),
        // vì LINQ provider của EF Core dịch MemberExpression theo PropertyInfo thực tế của entity -
        // nếu để trình biên dịch tự suy ra "e => e.ContentAuthorId == x" trong 1 method generic <T>
        // ràng buộc bởi interface, PropertyInfo sinh ra sẽ thuộc về interface chứ không phải entity,
        // dễ gây lỗi "could not be translated" tùy phiên bản EF Core. Cách dưới đây luôn an toàn.
        private static Expression<Func<T, bool>> ByAuthor<T>(int authorId) where T : class, IReviewableContent
            => PropertyEquals<T>(nameof(IReviewableContent.ContentAuthorId), authorId);

        private static Expression<Func<T, bool>> ByStatus<T>(ContentStatus status) where T : class, IReviewableContent
            => PropertyEquals<T>(nameof(IReviewableContent.Status), status);

        private static Expression<Func<T, bool>> PropertyEquals<T>(string propertyName, object value) where T : class
        {
            var param = Expression.Parameter(typeof(T), "e");
            var property = Expression.Property(param, propertyName);
            var constant = Expression.Constant(value, property.Type);
            var equal = Expression.Equal(property, constant);
            return Expression.Lambda<Func<T, bool>>(equal, param);
        }

        private static ContentReviewStatusResponse MapToStatusResponse(ContentEntityType entityType, IReviewableContent entity)
        {
            var title = entityType switch
            {
                ContentEntityType.Course => ((Course)entity).Title,
                ContentEntityType.KanjiEntry => ((KanjiEntry)entity).Character,
                ContentEntityType.VocabularyEntry => ((VocabularyEntry)entity).Word,
                ContentEntityType.GrammarPoint => ((GrammarPoint)entity).Title,
                ContentEntityType.PracticeTest => ((PracticeTest)entity).Title,
                _ => "N/A"
            };

            return new ContentReviewStatusResponse
            {
                Id = entity.Id,
                EntityType = entityType,
                Title = title,
                Status = entity.Status,
                ContentAuthorId = entity.ContentAuthorId,
                ReviewNote = entity.ReviewNote,
                ReviewedDate = entity.ReviewedDate,
                ReviewedById = entity.ReviewedById
            };
        }

        private static string GetRequired(ParsedContentRow row, string key)
            => row.Fields.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v
                : throw new InvalidOperationException($"Thiếu trường bắt buộc '{key}'.");

        private static string? GetOptional(ParsedContentRow row, string key)
            => row.Fields.TryGetValue(key, out var v) ? v : null;
    }
}