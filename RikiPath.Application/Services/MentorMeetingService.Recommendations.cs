using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
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
            .Where(review => mentorIds.Contains(review.MentorId) &&
                review.ModerationStatus == MentorReviewStatus.Approved)
            .GroupBy(review => review.MentorId)
            .Select(group => new { MentorId = group.Key, Count = group.Count(), Average = group.Average(review => review.Rating) })
            .ToDictionaryAsync(item => item.MentorId, ct);

        var recommendations = slotsByMentor.Select(group =>
        {
            var mentor = group.First().Mentor;
            var rating = reviews.GetValueOrDefault(group.Key);
            return new MentorRecommendationResponse(mentor.Id, $"{mentor.FirstName} {mentor.LastName}".Trim(),
                mentor.AvatarUrl, rating is null ? null : Math.Round(rating.Average, 1), rating?.Count ?? 0, rating?.Average >= HighRatingThreshold,
                group.OrderBy(slot => slot.StartTime).ThenBy(slot => slot.Id).Select(MapAvailability).ToList());
        }).OrderByDescending(item => item.IsRecommended)
            .ThenByDescending(item => item.AverageRating).ThenByDescending(item => item.ReviewCount)
            .ThenBy(item => item.Slots[0].StartTime).ThenBy(item => item.Id).ToList();

        return ApiResponse<List<MentorRecommendationResponse>>.Success(recommendations);
    }

    public async Task<ApiResponse<int>> GetMyRatingAsync(int bookingId, CancellationToken ct)
    {
        var userId = claimService.GetUserClaim().Id;
        if (await FindEligibleRatingBookingAsync(bookingId, userId, ct) is null)
            return ApiResponse<int>.NotFound("Buổi tư vấn không đủ điều kiện đánh giá.");

        var review = await unitOfWork.MentorReviews.FirstOrDefaultAsync(
            item => item.MentorBookingId == bookingId && item.LearnerId == userId, ct);
        return ApiResponse<int>.Success(review?.Rating ?? 0);
    }

    public async Task<ApiResponse<int>> RateMentorAsync(int bookingId, int rating, CancellationToken ct)
    {
        if (rating is < 1 or > 5)
            return ApiResponse<int>.Fail("Hãy chọn từ 1 đến 5 sao.");

        var userId = claimService.GetUserClaim().Id;
        var response = ApiResponse<int>.NotFound("Buổi tư vấn không đủ điều kiện đánh giá.");
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
            var booking = await FindEligibleRatingBookingAsync(bookingId, userId, token);
            if (booking is null) return;

            var review = await unitOfWork.MentorReviews.FirstOrDefaultAsync(
                item => item.MentorBookingId == bookingId, token);
            if (review is not null)
            {
                response = ApiResponse<int>.Fail("Bạn đã đánh giá buổi tư vấn này.", HttpStatusCode.Conflict);
                return;
            }

            await unitOfWork.MentorReviews.AddAsync(new MentorReview
            {
                MentorBookingId = booking.Id,
                MentorId = booking.MentorAvailability!.MentorId,
                LearnerId = userId,
                Rating = rating,
            }, token);
            await unitOfWork.SaveChangesAsync(token);
            response = ApiResponse<int>.Created(rating);
        }, ct);
        return response;
    }

    private Task<MentorBooking?> FindEligibleRatingBookingAsync(int bookingId, int learnerId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return unitOfWork.MentorBookings.Query().AsNoTracking().Include(booking => booking.MentorAvailability)
            .FirstOrDefaultAsync(booking => booking.Id == bookingId && !booking.IsDeleted &&
                booking.UserSubscription.UserId == learnerId && !booking.UserSubscription.IsDeleted &&
                booking.UserSubscription.PaymentStatus == PaymentStatus.Paid &&
                booking.MentorAvailability != null && booking.MentorAvailability.EndTime <= now &&
                (booking.Status == MentorStatus.Completed ||
                    (booking.AttendedAt.HasValue && (booking.Status == MentorStatus.Assigned || booking.Status == MentorStatus.Accepted))), ct);
    }
}
