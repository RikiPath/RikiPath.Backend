namespace RikiPath.Application.IServices;

/// <summary>Khung giờ dạy dự kiến (UTC, nửa mở [StartUtc, EndUtc)).</summary>
public sealed record ProposedTeachingInterval(DateTime StartUtc, DateTime EndUtc, int? SessionNumber = null);

/// <summary>Tùy chọn kiểm tra; giá trị null lấy theo chính sách nhóm hiệu lực.</summary>
public sealed record TeachingConflictOptions
{
    /// <summary>Bỏ qua các buổi của lớp này (lớp đang được sửa/thay Mentor).</summary>
    public int? ExcludeCohortId { get; init; }
    /// <summary>Bỏ qua các khung giờ này (ví dụ buổi đang được dời đi).</summary>
    public IReadOnlyCollection<int>? ExcludeAvailabilityIds { get; init; }
    public int? RestMinutes { get; init; }
    public int? DailyCap { get; init; }
    /// <summary>0 hoặc null (khi chính sách = 0) nghĩa là không giới hạn theo tuần.</summary>
    public int? WeeklyCap { get; init; }
    /// <summary>Kiểm tra trùng/nghỉ giữa chính các khung giờ dự kiến với nhau.</summary>
    public bool IncludeOwnIntervals { get; init; } = true;
}

/// <summary>Một xung đột lịch dạy. Không bao giờ chứa dữ liệu học viên.</summary>
public sealed record TeachingConflict(
    string Code,
    int? SessionNumber,
    DateTime ProposedStartUtc,
    DateTime ProposedEndUtc,
    string CommitmentKind,
    int? CohortId,
    string? CohortName,
    DateTime? CommitmentStartUtc,
    DateTime? CommitmentEndUtc,
    string Message);

public sealed record TeachingConflictReport(bool IsValid, IReadOnlyList<TeachingConflict> Conflicts)
{
    public TeachingConflict? First => Conflicts.Count > 0 ? Conflicts[0] : null;
    public static TeachingConflictReport Valid { get; } = new(true, []);
}

/// <summary>
/// Nguồn kiểm tra trùng lịch dạy chuẩn duy nhất của Mentor (lớp nhóm đã công bố, lịch 1-1 đã xác nhận, buổi khóa 1-1).
/// Bên gọi phải giữ <c>LockScheduleAsync(mentorId, ...)</c> trong cùng giao dịch trước khi ghi lịch.
/// </summary>
public interface IMentorTeachingConflictService
{
    Task<TeachingConflictReport> CheckAsync(int mentorId, IReadOnlyList<ProposedTeachingInterval> proposed,
        TeachingConflictOptions? options, CancellationToken ct);
}
