using RikiPath.Application.IServices;
using RikiPath.Application.Requests.KanjiWriting;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.KanjiWriting;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services
{
    public class KanjiWritingPracticeService(IUnitOfWork unitOfWork, IClaimService claimService)
        : IKanjiWritingPracticeService
    {
        public async Task<ApiResponse<List<KanjiWritingQueueItem>>> GetDueForPracticeAsync(
            int count, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var now = DateTime.UtcNow;

                // 1. Chữ đã đến hạn ôn lại (đã luyện ít nhất 1 lần trước đó).
                var dueItems = (await unitOfWork.ReviewCards.FindAsync(
                    r => r.UserId == userId
                        && r.Mode == ReviewMode.Writing
                        && r.KanjiId != null
                        && r.NextReviewDate <= now,
                    cancellationToken))
                    .OrderBy(r => r.NextReviewDate)
                    .Take(count)
                    .ToList();

                var result = new List<KanjiWritingQueueItem>();
                foreach (var item in dueItems)
                {
                    var kanji = await unitOfWork.Kanjis.GetByIdAsync(item.KanjiId!.Value, cancellationToken);
                    if (kanji is not null)
                        result.Add(MapToQueueItem(kanji, isNew: false));
                }

                // 2. Nếu chưa đủ count, bổ sung chữ MỚI (chưa từng có ReviewCard Writing) theo cấp độ
                // learner đang nhắm tới.
                var remaining = count - result.Count;
                if (remaining > 0)
                {
                    var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                    var targetLevelId = user?.TargetCertificateLevelId;

                    if (targetLevelId.HasValue)
                    {
                        var existingKanjiIds = (await unitOfWork.ReviewCards.FindAsync(
                            r => r.UserId == userId && r.Mode == ReviewMode.Writing && r.KanjiId != null,
                            cancellationToken))
                            .Select(r => r.KanjiId!.Value)
                            .ToHashSet();

                        var candidates = (await unitOfWork.Kanjis.FindAsync(
                            k => k.CertificateLevelId == targetLevelId.Value && k.Status == ContentStatus.Published,
                            cancellationToken))
                            .Where(k => !existingKanjiIds.Contains(k.Id))
                            .Take(remaining)
                            .ToList();

                        foreach (var kanji in candidates)
                        {
                            // Tạo sẵn ReviewCard cho chữ mới với lịch mặc định - lần luyện đầu tiên coi
                            // như "review" đầu tiên trong SM-2, không phải thao tác riêng biệt.
                            await unitOfWork.ReviewCards.AddAsync(new ReviewCard
                            {
                                UserId = userId,
                                KanjiId = kanji.Id,
                                Mode = ReviewMode.Writing,
                                EaseFactor = 2.5,
                                IntervalDays = 0,
                                Repetitions = 0,
                                NextReviewDate = now,
                            }, cancellationToken);

                            result.Add(MapToQueueItem(kanji, isNew: true));
                        }

                        await unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                }

                return ApiResponse<List<KanjiWritingQueueItem>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<KanjiWritingQueueItem>>.Fail($"Không thể tải danh sách chữ cần luyện viết: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<KanjiWritingQueueItem>>> GetAllForPracticeAsync(
            int count, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                if (user?.TargetCertificateLevelId is not int targetLevelId)
                    return ApiResponse<List<KanjiWritingQueueItem>>.Success([]);

                var kanjis = (await unitOfWork.Kanjis.FindAsync(
                    k => k.CertificateLevelId == targetLevelId && k.Status == ContentStatus.Published,
                    cancellationToken))
                    .OrderBy(k => k.Id)
                    .Take(Math.Clamp(count, 1, 200))
                    .ToList();

                var cards = await unitOfWork.ReviewCards.FindAsync(
                    r => r.UserId == userId && r.Mode == ReviewMode.Writing && r.KanjiId != null,
                    cancellationToken);
                var cardByKanji = cards.ToDictionary(card => card.KanjiId!.Value);

                return ApiResponse<List<KanjiWritingQueueItem>>.Success(kanjis
                    .Select(kanji => MapToQueueItem(
                        kanji,
                        isNew: !cardByKanji.ContainsKey(kanji.Id),
                        cardByKanji.TryGetValue(kanji.Id, out var card) ? card : null))
                    .ToList());
            }
            catch (Exception ex)
            {
                return ApiResponse<List<KanjiWritingQueueItem>>.Fail($"Không thể tải danh sách Kanji luyện viết: {ex.Message}");
            }
        }

        public async Task<ApiResponse<SubmitKanjiWritingResultResponse>> SubmitResultAsync(
            SubmitKanjiWritingResultRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;

                var reviewItem = await unitOfWork.ReviewCards.FirstOrDefaultAsync(
                    r => r.UserId == userId && r.Mode == ReviewMode.Writing && r.KanjiId == request.KanjiId,
                    cancellationToken);

                // Phòng trường hợp FE gọi submit cho 1 chữ chưa từng được GetDueForPracticeAsync tạo sẵn.
                reviewItem ??= new ReviewCard
                {
                    UserId = userId,
                    KanjiId = request.KanjiId,
                    Mode = ReviewMode.Writing,
                    EaseFactor = 2.5,
                    IntervalDays = 0,
                    Repetitions = 0,
                    NextReviewDate = DateTime.UtcNow,
                };

                var isNewItem = reviewItem.Id == 0;

                var quality = MapMistakesToQuality(request.TotalMistakes);
                ApplySm2(reviewItem, quality);
                reviewItem.LastReviewedAt = DateTime.UtcNow;
                reviewItem.WritingScore = Math.Clamp(request.Score, 0, 100);
                reviewItem.WritingCorrectStrokeCount = Math.Max(0, request.CorrectStrokeCount);
                reviewItem.WritingTotalStrokeCount = Math.Max(0, request.TotalStrokeCount);
                reviewItem.WritingPracticeMode = request.PracticeMode is "free" ? "free" : "guided";
                reviewItem.WritingScoreUpdatedAt = DateTime.UtcNow;

                if (isNewItem)
                    await unitOfWork.ReviewCards.AddAsync(reviewItem, cancellationToken);
                else
                    unitOfWork.ReviewCards.Update(reviewItem);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                // SaveChangesAsync ở trên đảm bảo reviewItem.Id đã được DB sinh ra (identity) dù là
                // ReviewCard mới hay cũ, nên log dưới đây luôn dùng đúng Id thật.
                await unitOfWork.ReviewHistories.AddAsync(new ReviewHistory
                {
                    ReviewCardId = reviewItem.Id,
                    Rating = MapQualityToRating(quality),
                    Quality = quality,
                    ReviewedAt = DateTime.UtcNow,
                }, cancellationToken);

                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<SubmitKanjiWritingResultResponse>.Success(new SubmitKanjiWritingResultResponse
                {
                    ReviewCardId = reviewItem.Id,
                    TotalMistakes = request.TotalMistakes,
                    QualityScore = quality,
                    Repetitions = reviewItem.Repetitions,
                    IntervalDays = reviewItem.IntervalDays,
                    NextReviewDate = reviewItem.NextReviewDate,
                    Score = reviewItem.WritingScore.Value,
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<SubmitKanjiWritingResultResponse>.Fail($"Không thể ghi nhận kết quả luyện viết: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<KanjiWritingScoreResponse>>> GetScoresAsync(
                CancellationToken cancellationToken)
            {
                try
                {
                    var userId = claimService.GetUserClaim().Id;
                    var cards = await unitOfWork.ReviewCards.FindAsync(
                        r => r.UserId == userId && r.Mode == ReviewMode.Writing && r.KanjiId != null && r.WritingScore != null,
                        cancellationToken);

                    return ApiResponse<List<KanjiWritingScoreResponse>>.Success(cards.Select(card => new KanjiWritingScoreResponse
                    {
                        KanjiId = card.KanjiId!.Value,
                        Score = card.WritingScore!.Value,
                        CorrectStrokeCount = card.WritingCorrectStrokeCount ?? 0,
                        TotalStrokeCount = card.WritingTotalStrokeCount ?? 0,
                        PracticeMode = card.WritingPracticeMode ?? "guided",
                        CompletedAt = card.WritingScoreUpdatedAt ?? card.LastReviewedAt ?? DateTime.UtcNow,
                    }).ToList());
                }
                catch (Exception ex)
                {
                    return ApiResponse<List<KanjiWritingScoreResponse>>.Fail($"Không thể tải điểm luyện viết: {ex.Message}");
                }
            }

            private static KanjiWritingQueueItem MapToQueueItem(Kanji kanji, bool isNew, ReviewCard? card = null) => new()
        {
            KanjiId = kanji.Id,
            Character = kanji.Character,
            Meaning = kanji.Meaning,
            OnYomi = kanji.OnYomi,
            KunYomi = kanji.KunYomi,
            StrokeCount = kanji.StrokeCount,
            IsNew = isNew,
            WritingScore = card?.WritingScore,
            WritingPracticeMode = card?.WritingPracticeMode,
        };

        // 0 lỗi -> nhớ hoàn hảo (5); mỗi lỗi thêm trừ dần; >=4 lỗi coi như chưa nhớ cách viết (1).
        private static int MapMistakesToQuality(int totalMistakes) => totalMistakes switch
        {
            0 => 5,
            1 => 4,
            2 => 3,
            3 => 2,
            _ => 1,
        };

        private static ReviewRating MapQualityToRating(int quality) => quality switch
        {
            >= 4 => ReviewRating.Easy,
            3 => ReviewRating.Good,
            2 => ReviewRating.Hard,
            _ => ReviewRating.Again,
        };

        // Công thức SM-2 chuẩn (SuperMemo 2).
        private static void ApplySm2(ReviewCard item, int quality)
        {
            if (quality >= 3)
            {
                item.IntervalDays = item.Repetitions switch
                {
                    0 => 1,
                    1 => 6,
                    _ => (int)Math.Round(item.IntervalDays * item.EaseFactor),
                };
                item.Repetitions += 1;
            }
            else
            {
                item.Repetitions = 0;
                item.IntervalDays = 1;
            }

            item.EaseFactor += 0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02);
            if (item.EaseFactor < 1.3) item.EaseFactor = 1.3;

            item.NextReviewDate = DateTime.UtcNow.AddDays(item.IntervalDays);
        }
    }
}