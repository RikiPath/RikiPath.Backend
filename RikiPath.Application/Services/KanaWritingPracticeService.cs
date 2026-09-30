using RikiPath.Application.IServices;
using RikiPath.Application.Requests.KanaWriting;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.KanaWriting;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services
{
    public class KanaWritingPracticeService(IUnitOfWork unitOfWork, IClaimService claimService) : IKanaWritingPracticeService
    {
        public async Task<ApiResponse<List<KanaWritingQueueItem>>> GetDueForPracticeAsync(int count, KanaCharacterType? type, CancellationToken cancellationToken)
        {
            if (count is < 1 or > 100) return ApiResponse<List<KanaWritingQueueItem>>.Fail("count phải nằm trong khoảng 1 đến 100.");
            if (type.HasValue && !Enum.IsDefined(type.Value)) return ApiResponse<List<KanaWritingQueueItem>>.Fail("type chỉ nhận Hiragana hoặc Katakana.");
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var now = DateTime.UtcNow;
                var cards = await unitOfWork.KanaWritingPracticeCards.FindAsync(
                    x => x.UserId == userId && x.NextReviewDate <= now && !x.KanaCharacter.IsDeleted && x.KanaCharacter.Status == ContentStatus.Published
                        && (!type.HasValue || x.KanaCharacter.Type == type.Value),
                    cancellationToken);
                var result = new List<KanaWritingQueueItem>();
                foreach (var card in cards.OrderBy(x => x.NextReviewDate).Take(count))
                {
                    var kana = await unitOfWork.KanaCharacters.GetByIdAsync(card.KanaCharacterId, cancellationToken);
                    if (kana is not null && kana.Status == ContentStatus.Published)
                        result.Add(Map(kana, false));
                }

                var remaining = count - result.Count;
                if (remaining > 0)
                {
                    var existingIds = cards.Select(x => x.KanaCharacterId).ToHashSet();
                    var allCards = await unitOfWork.KanaWritingPracticeCards.FindAsync(x => x.UserId == userId, cancellationToken);
                    existingIds.UnionWith(allCards.Select(x => x.KanaCharacterId));
                    var candidates = (await unitOfWork.KanaCharacters.FindAsync(
                        x => x.Status == ContentStatus.Published && !x.IsDeleted && (!type.HasValue || x.Type == type.Value), cancellationToken))
                        .Where(x => !existingIds.Contains(x.Id)).OrderBy(x => x.Type).ThenBy(x => x.Character).Take(remaining).ToList();

                    foreach (var kana in candidates)
                    {
                        await unitOfWork.KanaWritingPracticeCards.AddAsync(new KanaWritingPracticeCard
                        {
                            UserId = userId, KanaCharacterId = kana.Id, NextReviewDate = now
                        }, cancellationToken);
                        result.Add(Map(kana, true));
                    }
                    if (candidates.Count > 0) await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                return ApiResponse<List<KanaWritingQueueItem>>.Success(result);
            }
            catch (Exception ex) { return ApiResponse<List<KanaWritingQueueItem>>.Fail($"Không thể tải danh sách Kana cần luyện viết: {ex.Message}"); }
        }

        public async Task<ApiResponse<SubmitKanaWritingResultResponse>> SubmitResultAsync(SubmitKanaWritingResultRequest request, CancellationToken cancellationToken)
        {
            if (request.KanaCharacterId <= 0 || request.TotalMistakes < 0)
                return ApiResponse<SubmitKanaWritingResultResponse>.Fail("KanaCharacterId phải lớn hơn 0 và TotalMistakes không được âm.");
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var kana = await unitOfWork.KanaCharacters.GetByIdAsync(request.KanaCharacterId, cancellationToken);
                if (kana is null || kana.Status != ContentStatus.Published)
                    return ApiResponse<SubmitKanaWritingResultResponse>.NotFound("Không tìm thấy ký tự Kana đã được duyệt.");

                var card = await unitOfWork.KanaWritingPracticeCards.FirstOrDefaultAsync(
                    x => x.UserId == userId && x.KanaCharacterId == request.KanaCharacterId, cancellationToken);
                var isNew = card is null;
                card ??= new KanaWritingPracticeCard { UserId = userId, KanaCharacterId = kana.Id, NextReviewDate = DateTime.UtcNow };

                var quality = request.TotalMistakes switch { 0 => 5, 1 => 4, 2 => 3, 3 => 2, _ => 1 };
                ApplySm2(card, quality);
                card.LastReviewedAt = DateTime.UtcNow;
                if (isNew) await unitOfWork.KanaWritingPracticeCards.AddAsync(card, cancellationToken);
                else unitOfWork.KanaWritingPracticeCards.Update(card);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                await unitOfWork.KanaWritingPracticeHistories.AddAsync(new KanaWritingPracticeHistory
                {
                    KanaWritingPracticeCardId = card.Id,
                    TotalMistakes = request.TotalMistakes,
                    Quality = quality,
                    Rating = quality switch { >= 4 => ReviewRating.Easy, 3 => ReviewRating.Good, 2 => ReviewRating.Hard, _ => ReviewRating.Again },
                    ReviewedAt = DateTime.UtcNow
                }, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<SubmitKanaWritingResultResponse>.Success(new()
                {
                    PracticeCardId = card.Id, KanaCharacterId = kana.Id, TotalMistakes = request.TotalMistakes,
                    QualityScore = quality, Repetitions = card.Repetitions, IntervalDays = card.IntervalDays,
                    NextReviewDate = card.NextReviewDate
                });
            }
            catch (Exception ex) { return ApiResponse<SubmitKanaWritingResultResponse>.Fail($"Không thể ghi nhận kết quả luyện viết Kana: {ex.Message}"); }
        }

        private static KanaWritingQueueItem Map(KanaCharacter x, bool isNew) => new()
        {
            KanaCharacterId = x.Id, Character = x.Character, Type = x.Type, Romaji = x.Romaji,
            StrokeCount = x.StrokeCount, StrokeOrderImageUrl = x.StrokeOrderImageUrl, AudioUrl = x.AudioUrl, IsNew = isNew
        };

        private static void ApplySm2(KanaWritingPracticeCard card, int quality)
        {
            if (quality >= 3)
            {
                card.IntervalDays = card.Repetitions switch { 0 => 1, 1 => 6, _ => (int)Math.Round(card.IntervalDays * card.EaseFactor) };
                card.Repetitions++;
            }
            else { card.Repetitions = 0; card.IntervalDays = 1; }
            card.EaseFactor = Math.Max(1.3, card.EaseFactor + 0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02));
            card.NextReviewDate = DateTime.UtcNow.AddDays(card.IntervalDays);
        }
    }
}
