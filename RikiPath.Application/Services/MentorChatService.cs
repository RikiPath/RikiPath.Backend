using System.Net;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class MentorChatService(IUnitOfWork unitOfWork, IClaimService claimService) : IMentorChatService
{
    private const int MaxTextLength = 2000;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    public async Task<ApiResponse<List<MentorChatThreadResponse>>> GetThreadsAsync(CancellationToken cancellationToken)
    {
        var user = claimService.GetUserClaim();
        List<MentorBooking> bookings = user.Role switch
        {
            Role.Learner => await unitOfWork.MentorBookings.GetByLearnerAsync(user.Id, cancellationToken),
            Role.Mentor => await unitOfWork.MentorBookings.GetChatThreadsForMentorAsync(user.Id, cancellationToken),
            _ => []
        };

        if (user.Role is not (Role.Learner or Role.Mentor))
            return ApiResponse<List<MentorChatThreadResponse>>.Fail("Chỉ Learner và Mentor được sử dụng chat.", HttpStatusCode.Forbidden);

        var paidBookings = bookings.Where(x => x.UserSubscription?.PaymentStatus == PaymentStatus.Paid).ToList();
        var result = new List<MentorChatThreadResponse>(paidBookings.Count);
        foreach (var booking in paidBookings)
        {
            var latest = await unitOfWork.MentorChatMessages.GetLatestAsync(booking.Id, cancellationToken);
            var unread = await unitOfWork.MentorChatMessages.GetUnreadCountAsync(booking.Id, user.Id, cancellationToken);
            result.Add(MapThread(booking, user.Id, unread, latest));
        }

        return ApiResponse<List<MentorChatThreadResponse>>.Success(
            result.OrderByDescending(x => x.LastMessage?.SentAt ?? x.ScheduledAt ?? DateTime.MinValue).ToList());
    }

    public async Task<ApiResponse<List<MentorChatMessageResponse>>> GetMessagesAsync(
        int bookingId, int? beforeMessageId, int pageSize, CancellationToken cancellationToken)
    {
        var user = claimService.GetUserClaim();
        if (beforeMessageId is <= 0)
            return ApiResponse<List<MentorChatMessageResponse>>.Fail("beforeMessageId phải lớn hơn 0.");

        var booking = await GetAccessibleBookingAsync(bookingId, user, cancellationToken);
        if (booking is null)
            return ApiResponse<List<MentorChatMessageResponse>>.Fail("Không tìm thấy cuộc trò chuyện.", HttpStatusCode.NotFound);

        var size = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var messages = await unitOfWork.MentorChatMessages.GetPageAsync(
            bookingId, beforeMessageId, size, cancellationToken);
        messages.Reverse();
        return ApiResponse<List<MentorChatMessageResponse>>.Success(messages.Select(MapMessage).ToList());
    }

    public async Task<ApiResponse<MentorChatThreadResponse>> GetThreadAsync(int bookingId, CancellationToken cancellationToken)
    {
        var user = claimService.GetUserClaim();
        var booking = await GetAccessibleBookingAsync(bookingId, user, cancellationToken);
        if (booking is null)
            return ApiResponse<MentorChatThreadResponse>.Fail("Không tìm thấy cuộc trò chuyện.", HttpStatusCode.NotFound);

        var latest = await unitOfWork.MentorChatMessages.GetLatestAsync(bookingId, cancellationToken);
        var unread = await unitOfWork.MentorChatMessages.GetUnreadCountAsync(bookingId, user.Id, cancellationToken);
        return ApiResponse<MentorChatThreadResponse>.Success(MapThread(booking, user.Id, unread, latest));
    }

    public async Task<ApiResponse<MentorChatMessageResponse>> SendAsync(
        int bookingId, string? text, CancellationToken cancellationToken)
    {
        var user = claimService.GetUserClaim();
        var normalized = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return ApiResponse<MentorChatMessageResponse>.Fail("Tin nhắn không được để trống.");
        if (normalized.Length > MaxTextLength)
            return ApiResponse<MentorChatMessageResponse>.Fail($"Tin nhắn tối đa {MaxTextLength} ký tự.");

        var booking = await GetAccessibleBookingAsync(bookingId, user, cancellationToken);
        if (booking is null)
            return ApiResponse<MentorChatMessageResponse>.Fail("Không tìm thấy cuộc trò chuyện.", HttpStatusCode.NotFound);
        if (!CanSend(booking, DateTime.UtcNow))
            return ApiResponse<MentorChatMessageResponse>.Fail(
                "Cuộc trò chuyện đã đóng; bạn vẫn có thể xem lại lịch sử.", HttpStatusCode.Conflict);

        var message = new MentorChatMessage
        {
            MentorBookingId = bookingId,
            SenderUserId = user.Id,
            Text = normalized,
            SentAt = DateTime.UtcNow,
        };
        await unitOfWork.MentorChatMessages.AddAsync(message, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Load sender profile after save so SignalR and REST return identical server-authored metadata.
        var saved = (await unitOfWork.MentorChatMessages.GetPageAsync(
            bookingId, null, 1, cancellationToken)).Single();
        return ApiResponse<MentorChatMessageResponse>.Success(MapMessage(saved), HttpStatusCode.Created);
    }

    public async Task<ApiResponse<int>> MarkReadAsync(int bookingId, CancellationToken cancellationToken)
    {
        var user = claimService.GetUserClaim();
        var booking = await GetAccessibleBookingAsync(bookingId, user, cancellationToken);
        if (booking is null)
            return ApiResponse<int>.Fail("Không tìm thấy cuộc trò chuyện.", HttpStatusCode.NotFound);

        var updated = await unitOfWork.MentorChatMessages.MarkIncomingAsReadAsync(
            bookingId, user.Id, DateTime.UtcNow, cancellationToken);
        return ApiResponse<int>.Success(updated);
    }

    private async Task<MentorBooking?> GetAccessibleBookingAsync(int bookingId, ClaimDTO user, CancellationToken cancellationToken)
    {
        var booking = await unitOfWork.MentorBookings.GetForChatAsync(bookingId, cancellationToken);
        if (booking is null || booking.UserSubscription?.PaymentStatus != PaymentStatus.Paid)
            return null;

        var learnerId = booking.UserSubscription.UserId;
        var mentorId = booking.MentorAvailability?.MentorId ?? booking.UserSubscription.MentorId;
        var isAllowed = user.Role == Role.Learner && user.Id == learnerId
            || user.Role == Role.Mentor && mentorId == user.Id;
        return isAllowed ? booking : null;
    }

    private static bool CanSend(MentorBooking booking, DateTime now)
        => (booking.Status is MentorStatus.Assigned or MentorStatus.Accepted)
            && booking.MentorAvailability is { IsDeleted: false } slot
            && slot.EndTime > now;

    private static MentorChatThreadResponse MapThread(
        MentorBooking booking, int userId, int unreadCount, MentorChatMessage? latest)
    {
        var subscription = booking.UserSubscription;
        var slot = booking.MentorAvailability;
        var learner = subscription?.UserAccount;
        var mentor = slot?.Mentor ?? subscription?.Mentor;
        return new MentorChatThreadResponse
        {
            BookingId = booking.Id,
            LearnerId = subscription?.UserId ?? 0,
            LearnerName = learner is null ? string.Empty : $"{learner.FirstName} {learner.LastName}".Trim(),
            MentorId = slot?.MentorId ?? subscription?.MentorId ?? 0,
            MentorName = mentor is null ? string.Empty : $"{mentor.FirstName} {mentor.LastName}".Trim(),
            ScheduledAt = booking.ScheduledAt,
            EndsAt = slot?.EndTime,
            BookingStatus = booking.Status.ToString(),
            CanSendMessages = CanSend(booking, DateTime.UtcNow),
            UnreadCount = unreadCount,
            LastMessage = latest is null ? null : MapMessage(latest),
        };
    }

    private static MentorChatMessageResponse MapMessage(MentorChatMessage message) => new()
    {
        Id = message.Id,
        BookingId = message.MentorBookingId,
        SenderUserId = message.SenderUserId,
        SenderName = $"{message.Sender?.FirstName} {message.Sender?.LastName}".Trim(),
        SenderRole = message.Sender?.Role.ToString() ?? string.Empty,
        Text = message.Text,
        SentAt = message.SentAt,
        ReadAt = message.ReadAt,
    };
}
