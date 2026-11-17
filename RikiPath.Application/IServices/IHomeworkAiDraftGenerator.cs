namespace RikiPath.Application.IServices;

public sealed record HomeworkAiDraftResult(
    bool IsAvailable,
    string? Feedback = null,
    double? SuggestedScore = null,
    string? UnavailableReason = null);

public interface IHomeworkAiDraftGenerator
{
    Task<HomeworkAiDraftResult> GenerateDraftAsync(
        int assignmentId,
        string title,
        string? instructions,
        string? submissionText,
        string? submissionFileUrl,
        CancellationToken cancellationToken = default);
}

public sealed class HomeworkAiDraftGenerator : IHomeworkAiDraftGenerator
{
    public Task<HomeworkAiDraftResult> GenerateDraftAsync(
        int assignmentId,
        string title,
        string? instructions,
        string? submissionText,
        string? submissionFileUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(submissionText) && string.IsNullOrWhiteSpace(submissionFileUrl))
            return Task.FromResult(new HomeworkAiDraftResult(false, UnavailableReason: "Chưa có nội dung bài nộp để tạo gợi ý AI."));

        return Task.FromResult(new HomeworkAiDraftResult(
            IsAvailable: true,
            Feedback: $"[Nháp AI cho Cố vấn] Bài làm \"{title}\" đã có nội dung nộp. Vui lòng rà soát lỗi ngữ pháp và phát âm trước khi công bố cho học viên.",
            SuggestedScore: null));
    }
}
