namespace RikiPath.Application.Requests.MentorScheduling;

// Mentor xem trước xung đột lịch dạy của chính mình (chỉ tham khảo; backend vẫn kiểm tra lại khi ghi lịch).
public sealed class TeachingConflictPreviewRequest
{
    public List<TeachingConflictPreviewInterval> Intervals { get; init; } = [];
    public int? ExcludeCohortId { get; init; }
}

public sealed class TeachingConflictPreviewInterval
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
}
