using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
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
                var dueItems = await unitOfWork.ReviewCards.GetDueForReviewAsync(userId, DateTime.UtcNow);
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
                var reviewItem = await unitOfWork.ReviewCards.GetByIdAsync(request.ReviewCardId);
                if (reviewItem is null)
                    return ApiResponse<SubmitReviewResultResponse>.NotFound(
                        $"Không tìm thấy ReviewCard có Id = {request.ReviewCardId}.");

                if (reviewItem.UserId != userId)
                    return ApiResponse<SubmitReviewResultResponse>.Fail(
                        "Bạn không có quyền nộp kết quả ôn tập cho mục này.",
                        HttpStatusCode.Forbidden);

                ApplySm2Algorithm(reviewItem, request.Quality);
                unitOfWork.ReviewCards.Update(reviewItem);

                var log = new ReviewHistory
                {
                    ReviewCardId = reviewItem.Id,
                    Quality = request.Quality,
                    Rating = MapQualityToRating(request.Quality),
                    ReviewedAt = DateTime.UtcNow,
                };
                await unitOfWork.ReviewHistories.AddAsync(log);

                await unitOfWork.SaveChangesAsync();

                var result = new SubmitReviewResultResponse
                {
                    ReviewCardId = reviewItem.Id,
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

        private static void ApplySm2Algorithm(ReviewCard item, int quality)
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

        private static ReviewQueueItemResponse MapToQueueItem(ReviewCard item)
        {
            if (item.LearnerNoteEntry is { } note)
            {
                var isFromBank = note.Vocabulary is not null;
                var isKanjiNote = note.Kanji is not null;

                return new ReviewQueueItemResponse
                {
                    ReviewCardId = item.Id,
                    ContentType = isKanjiNote ? ReviewContentType.Kanji : ReviewContentType.Vocabulary,
                    Term = isFromBank ? note.Vocabulary!.Word
                         : isKanjiNote ? note.Kanji!.Character
                         : note.ManualWord ?? string.Empty,
                    Reading = isFromBank ? note.Vocabulary!.Reading
                            : isKanjiNote ? note.Kanji!.OnYomi
                            : note.ManualReading,
                    Meaning = isFromBank ? note.Vocabulary!.Meaning
                            : isKanjiNote ? note.Kanji!.Meaning
                            : note.ManualMeaning ?? string.Empty,
                    Note = note.Note,
                    Repetitions = item.Repetitions,
                    NextReviewDate = item.NextReviewDate,
                };
            }

            if (item.Kanji is { } kanji)
            {
                return new ReviewQueueItemResponse
                {
                    ReviewCardId = item.Id,
                    ContentType = ReviewContentType.Kanji,
                    Term = kanji.Character,
                    Reading = kanji.OnYomi,
                    Meaning = kanji.Meaning,
                    Repetitions = item.Repetitions,
                    NextReviewDate = item.NextReviewDate,
                };
            }

            var grammar = item.GrammarPattern!; // ràng buộc: 1 trong 3 nguồn luôn có giá trị
            return new ReviewQueueItemResponse
            {
                ReviewCardId = item.Id,
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
