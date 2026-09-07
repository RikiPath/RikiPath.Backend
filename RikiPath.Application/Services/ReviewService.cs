using Domain.Entities;
using Domain.Enums;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Reviews;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Reviews;
using System.Net;

namespace RikiPath.Application.Services
{
    public class ReviewService(IUnitOfWork unitOfWork) : IReviewService
    {
        private const double MinEaseFactor = 1.3;

        public async Task<ApiResponse<DailyReviewQueueResponse>> GetDailyReviewQueueAsync(int userId, CancellationToken cancellationToken)
        {
            try
            {
                var dueItems = await unitOfWork.ReviewItems.GetDueForReviewAsync(userId, DateTime.UtcNow);
                var items = dueItems.Select(MapToQueueItem).ToList();

                var result = new DailyReviewQueueResponse
                {
                    TotalDue = items.Count,
                    Items = items,
                };

                return ApiResponse<DailyReviewQueueResponse>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<DailyReviewQueueResponse>.Fail(
                    "Không thể tải hàng chờ ôn tập hôm nay.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<SubmitReviewResultResponse>> SubmitReviewResultAsync(
            int userId, SubmitReviewRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var reviewItem = await unitOfWork.ReviewItems.GetByIdAsync(request.ReviewItemId);
                if (reviewItem is null)
                    return ApiResponse<SubmitReviewResultResponse>.NotFound(
                        $"Không tìm thấy ReviewItem có Id = {request.ReviewItemId}.");

                if (reviewItem.UserId != userId)
                    return ApiResponse<SubmitReviewResultResponse>.Fail(
                        "Bạn không có quyền nộp kết quả ôn tập cho mục này.",
                        HttpStatusCode.Forbidden);

                ApplySm2Algorithm(reviewItem, request.Quality);
                unitOfWork.ReviewItems.Update(reviewItem);

                var log = new ReviewLog
                {
                    ReviewItemId = reviewItem.Id,
                    Quality = request.Quality,
                    Rating = MapQualityToRating(request.Quality),
                    ReviewedAt = DateTime.UtcNow,
                };
                await unitOfWork.ReviewLogs.AddAsync(log);

                await unitOfWork.SaveChangesAsync();

                var result = new SubmitReviewResultResponse
                {
                    ReviewItemId = reviewItem.Id,
                    QualitySubmitted = request.Quality,
                    NewEaseFactor = reviewItem.EaseFactor,
                    NewIntervalDays = reviewItem.IntervalDays,
                    Repetitions = reviewItem.Repetitions,
                    NextReviewDate = reviewItem.NextReviewDate,
                };

                return ApiResponse<SubmitReviewResultResponse>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<SubmitReviewResultResponse>.Fail(
                    "Không thể ghi nhận kết quả ôn tập.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        private static void ApplySm2Algorithm(ReviewItem item, int quality)
        {
            if (quality < 3)
            {
                item.Repetitions = 0;
                item.IntervalDays = 1;
            }
            else
            {
                item.IntervalDays = item.Repetitions switch
                {
                    0 => 1,
                    1 => 6,
                    _ => (int)Math.Round(item.IntervalDays * item.EaseFactor),
                };
                item.Repetitions += 1;
            }

            var newEaseFactor = item.EaseFactor + (0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02));
            item.EaseFactor = Math.Max(newEaseFactor, MinEaseFactor);

            item.NextReviewDate = DateTime.UtcNow.Date.AddDays(item.IntervalDays);
            item.LastReviewedAt = DateTime.UtcNow;
        }

        private static ReviewRating MapQualityToRating(int quality) => quality switch
        {
            <= 2 => ReviewRating.Again,
            3 => ReviewRating.Hard,
            4 => ReviewRating.Good,
            _ => ReviewRating.Easy,
        };

        private static ReviewQueueItemResponse MapToQueueItem(ReviewItem item)
        {
            if (item.VocabularyNoteEntry is { } note)
            {
                var isFromBank = note.VocabularyEntry is not null;
                var isKanjiNote = note.KanjiEntry is not null;

                return new ReviewQueueItemResponse
                {
                    ReviewItemId = item.Id,
                    ContentType = isKanjiNote ? ReviewContentType.Kanji : ReviewContentType.Vocabulary,
                    Term = isFromBank ? note.VocabularyEntry!.Word
                         : isKanjiNote ? note.KanjiEntry!.Character
                         : note.ManualWord ?? string.Empty,
                    Reading = isFromBank ? note.VocabularyEntry!.Reading
                            : isKanjiNote ? note.KanjiEntry!.OnYomi
                            : note.ManualReading,
                    Meaning = isFromBank ? note.VocabularyEntry!.Meaning
                            : isKanjiNote ? note.KanjiEntry!.Meaning
                            : note.ManualMeaning ?? string.Empty,
                    Note = note.Note,
                    Repetitions = item.Repetitions,
                    NextReviewDate = item.NextReviewDate,
                };
            }

            if (item.KanjiEntry is { } kanji)
            {
                return new ReviewQueueItemResponse
                {
                    ReviewItemId = item.Id,
                    ContentType = ReviewContentType.Kanji,
                    Term = kanji.Character,
                    Reading = kanji.OnYomi,
                    Meaning = kanji.Meaning,
                    Repetitions = item.Repetitions,
                    NextReviewDate = item.NextReviewDate,
                };
            }

            var grammar = item.GrammarPoint!; // ràng buộc: 1 trong 3 nguồn luôn có giá trị
            return new ReviewQueueItemResponse
            {
                ReviewItemId = item.Id,
                ContentType = ReviewContentType.Grammar,
                Term = grammar.Title,
                Meaning = grammar.Structure,
                Repetitions = item.Repetitions,
                NextReviewDate = item.NextReviewDate,
            };
        }

        /// <summary>Gói thông tin exception vào Errors để frontend/dev thấy đủ khi debug — stack trace
        /// chỉ kèm theo ở build Debug, không lộ ra bản Release/Production.</summary>
        private static List<string> BuildDebugErrors(Exception ex)
        {
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
#if DEBUG
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
#endif
            return errors;
        }
    }
}
