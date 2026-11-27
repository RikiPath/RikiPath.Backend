namespace RikiPath.Application.IServices;

/// <summary>Khung giờ dạy dự kiến (UTC, nửa mở [StartUtc, EndUtc)).</summary>
public sealed record ProposedTeachingInterval(DateTime StartUtc, DateTime EndUtc, int? SessionNumber = null);

/// <summary>Tùy chọn kiểm tra; giá trị null lấy theo chính sách nhóm hiệu lực.</summary>
public sealed record TeachingConflictOptions
{
    /// <summary>Bỏ qua các buổi của lớp này (lớp đang được sửa/thay Mentor).</summary>
    public int? ExcludeCohortId { get; init; }
    /// <summary>Bỏ qua đề xuất mở lớp này khi kiểm tra (tránh tự xung đột khi chỉnh sửa/gửi lại).</summary>
    public int? ExcludeProposalId { get; init; }
    /// <summary>Bỏ qua các khung giờ này (ví dụ buổi đang được dời đi).</summary>
    public IReadOnlyCollection<int>? ExcludeAvailabilityIds { get; init; }
    public int? RestMinutes { get; init; }
    /// <summary>Kiểm tra trùng/nghỉ giữa chính các khung giờ dự kiến với nhau.</summary>
    public bool IncludeOwnIntervals { get; init; } = true;
    /// <summary>Cam kết tạm thời bổ sung trong bộ nhớ (ví dụ các đề xuất cùng Giáo viên đã xếp lịch trước trong cùng đợt duyệt hàng loạt).</summary>
    public IReadOnlyCollection<ProposedTeachingInterval>? AdditionalCommittedIntervals { get; init; }
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
    string Message,
    string? JlptLevelCode = null,
    string? CommitmentStatus = null,
    int? ProposalId = null);

public sealed record TeachingConflictReport(bool IsValid, IReadOnlyList<TeachingConflict> Conflicts)
{
    public TeachingConflict? First => Conflicts.Count > 0 ? Conflicts[0] : null;
    public static TeachingConflictReport Valid { get; } = new(true, []);
}

/// <summary>
/// Nguồn kiểm tra trùng lịch dạy chuẩn duy nhất của Mentor (chỉ kiểm tra lịch giảng dạy chính thức của cùng một Giáo viên;
/// không khóa slot theo đề xuất chưa duyệt và không kiểm tra chéo giữa nhiều Giáo viên).
/// Bên gọi phải giữ <c>LockScheduleAsync(mentorId, ...)</c> trong cùng giao dịch trước khi ghi lịch.
/// </summary>
public interface IMentorTeachingConflictService
{
    Task<TeachingConflictReport> CheckAsync(int mentorId, IReadOnlyList<ProposedTeachingInterval> proposed,
        TeachingConflictOptions? options, CancellationToken ct);
}
