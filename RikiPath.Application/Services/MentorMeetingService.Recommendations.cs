using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed partial class MentorMeetingService
{
    private const double HighRatingThreshold = 4;

    public async Task<ApiResponse<List<MentorRecommendationResponse>>> GetRecommendationsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (to <= from || to - from > TimeSpan.FromDays(90) || to.UtcDateTime <= now)
            return ApiResponse<List<MentorRecommendationResponse>>.Fail(
                "Hãy chọn khoảng thời gian trong tương lai, tối đa 90 ngày.");

        await ExpirePendingPaymentsAsync(ct);
        var slots = await unitOfWork.MentorAvailabilities.GetAvailableSlotsAsync(null, from.UtcDateTime, to.UtcDateTime, ct);
        var slotsByMentor = slots.Where(slot => slot.CancelRequestedAt == null).ToLookup(slot => slot.MentorId);
        var mentorIds = slotsByMentor.Select(group => group.Key).ToArray();
        if (mentorIds.Length == 0)
            return ApiResponse<List<MentorRecommendationResponse>>.Success([]);

        var reviews = await unitOfWork.MentorReviews.Query().AsNoTracking()
            .Where(review => mentorIds.Contains(review.MentorId) && review.IsPublic &&
                review.ModerationStatus == MentorReviewStatus.Approved)
            .GroupBy(review => review.MentorId)
            .Select(group => new { MentorId = group.Key, Count = group.Count(), Average = group.Average(review => review.Rating) })
            .ToDictionaryAsync(item => item.MentorId, ct);

        var recommendations = slotsByMentor.Select(group =>
        {
            var mentor = group.First().Mentor;
            var rating = reviews.GetValueOrDefault(group.Key);
            return new MentorRecommendationResponse(mentor.Id, $"{mentor.FirstName} {mentor.LastName}".Trim(),
                mentor.AvatarUrl, rating?.Average, rating?.Count ?? 0, rating?.Average >= HighRatingThreshold,
                group.OrderBy(slot => slot.StartTime).ThenBy(slot => slot.Id).Select(MapAvailability).ToList());
        }).OrderByDescending(item => item.IsRecommended)
            .ThenByDescending(item => item.AverageRating).ThenByDescending(item => item.ReviewCount)
            .ThenBy(item => item.Slots[0].StartTime).ThenBy(item => item.Id).ToList();

        return ApiResponse<List<MentorRecommendationResponse>>.Success(recommendations);
    }

}
