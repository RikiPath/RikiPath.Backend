using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.MentorMeetings;

public sealed class MentorMeetingFeatureResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class MentorMeetingPlanResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationDays { get; set; }
    public decimal Price { get; set; }
    public int MeetingSessionCount { get; set; }
    public List<MentorMeetingFeatureResponse> Features { get; set; } = [];
}

public sealed class MentorAvailabilityResponse
{
    public int Id { get; set; }
    public int MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsBooked { get; set; }
}

public sealed class MentorBookingResponse
{
    public int Id { get; set; }
    public int UserSubscriptionId { get; set; }
    public int? MentorAvailabilityId { get; set; }
    public int? MentorId { get; set; }
    public string? MentorName { get; set; }
    public int? LearnerId { get; set; }
    public string? LearnerName { get; set; }
    public ConsultationStatus Status { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? MeetingLink { get; set; }
    public Guid? RoomId { get; set; }
}

public sealed class MentorMeetingPurchaseResponse
{
    public int SubscriptionId { get; set; }
    public int BookingId { get; set; }
    public long OrderCode { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int MeetingSessionCount { get; set; }
    public List<MentorMeetingFeatureResponse> Features { get; set; } = [];
    public decimal Amount { get; set; }
    public string CheckoutUrl { get; set; } = string.Empty;
    public string PaymentLinkId { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public DateTime? PaymentExpiresAt { get; set; }
}

public sealed class MentorMeetingPurchaseStatusResponse
{
    public int SubscriptionId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public PaymentStatus PaymentStatus { get; set; }
    public bool IsCheckoutExpired { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime? PaymentExpiresAt { get; set; }
    public int MeetingSessionsIncluded { get; set; }
    public int MeetingSessionsUsed { get; set; }
    public List<MentorBookingResponse> Bookings { get; set; } = [];
}
