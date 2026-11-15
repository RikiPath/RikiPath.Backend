namespace RikiPath.Domain.Entities;

// Các bảng bổ sung không thay đổi ý nghĩa của lịch, buổi đặt và gói học đã tồn tại.
public sealed class MentorSchedule
{
    public long Id { get; set; }
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;
    public int PolicyVersion { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public bool AutoPublish { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? GeneratedThrough { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<MentorScheduleRule> Rules { get; set; } = new List<MentorScheduleRule>();
}

public sealed class MentorScheduleRule
{
    public long Id { get; set; }
    public long ScheduleId { get; set; }
    public MentorSchedule Schedule { get; set; } = null!;
    public int Weekday { get; set; }
    public string DaypartCode { get; set; } = null!;
    public TimeOnly LocalStart { get; set; }
    public TimeOnly LocalEnd { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public sealed class MentorBookingIntent
{
    public long Id { get; set; }
    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;
    public string IntentKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public int? MentorBookingId { get; set; }
    public MentorBooking? MentorBooking { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

// Phần mở rộng một-một không thêm trạng thái vào MentorStatus hiện có.
public sealed class MentorBookingTerms
{
    public int MentorBookingId { get; set; }
    public MentorBooking MentorBooking { get; set; } = null!;
    public int MentorAvailabilityId { get; set; }
    public MentorAvailability MentorAvailability { get; set; } = null!;
    public int PolicyVersion { get; set; }
    public DateTime ScheduledStartUtc { get; set; }
    public DateTime ScheduledEndUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
}

// Khoảng giữ chỗ dùng [StartUtc, EndUtc); ràng buộc exclusion chờ phê duyệt Data/DB.
public sealed class MentorBookingReservation
{
    public long Id { get; set; }
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;
    public int MentorBookingId { get; set; }
    public MentorBooking MentorBooking { get; set; } = null!;
    public int MentorAvailabilityId { get; set; }
    public MentorAvailability MentorAvailability { get; set; } = null!;
    public DateOnly LocalBusinessDate { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    // Khoảng bảo vệ gồm 15 phút nghỉ sau buổi hỗ trợ; chỉ áp dụng cho reservation theo chính sách mới.
    public DateTime RestProtectedUntilUtc { get; set; }
    public int PolicyVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
}

public sealed class MentorBookingDayGuard
{
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;
    public DateOnly LocalBusinessDate { get; set; }
    public int ActiveBookingCount { get; set; }
}

public sealed class MentorOutboxEvent
{
    public long Id { get; set; }
    public string EventKey { get; set; } = null!;
    public string AggregateType { get; set; } = null!;
    public string AggregateId { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public int EventVersion { get; set; }
    public string PayloadJson { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public int Attempts { get; set; }
}
