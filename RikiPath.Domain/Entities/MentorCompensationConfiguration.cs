namespace RikiPath.Domain.Entities;

/// <summary>Đơn giá theo giờ của cố vấn do quản trị viên cấu hình theo thời gian hiệu lực.</summary>
public sealed class MentorHourlyRate
{
    public int Id { get; set; }
    public int MentorUserId { get; set; }
    public UserAccount MentorUser { get; set; } = null!;
    public decimal AmountPerHour { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveUntilUtc { get; set; }
    public int ConfiguredByAdminId { get; set; }
    public UserAccount ConfiguredByAdmin { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Hệ số thù lao theo cấp độ JLPT, chỉ áp dụng cho các lịch đặt mới.</summary>
public sealed class JlptLevelPayCoefficient
{
    public int Id { get; set; }
    public int CertificateLevelId { get; set; }
    public CertificateLevel CertificateLevel { get; set; } = null!;
    public decimal Coefficient { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveUntilUtc { get; set; }
    public int ConfiguredByAdminId { get; set; }
    public UserAccount ConfiguredByAdmin { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Cấp độ mà quản trị viên đã xác minh cố vấn được phép hỗ trợ.</summary>
public sealed class MentorTeachingQualification
{
    public int Id { get; set; }
    public int MentorUserId { get; set; }
    public UserAccount MentorUser { get; set; } = null!;
    public int CertificateLevelId { get; set; }
    public CertificateLevel CertificateLevel { get; set; } = null!;
    public bool IsActive { get; set; }
    public int VerifiedByAdminId { get; set; }
    public UserAccount VerifiedByAdmin { get; set; } = null!;
    public DateTime VerifiedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
