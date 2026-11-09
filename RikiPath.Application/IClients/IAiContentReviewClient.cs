namespace RikiPath.Application.IClients
{
    /// <summary>
    /// Gọi AI để kiểm tra nội dung. Trả về JSON thô (cùng pattern với IAiLearningPathClient).
    /// Implementation nên dùng temperature thấp (0–0.2) để kết quả ổn định.
    /// </summary>
    public interface IAiContentReviewClient
    {
        Task<string> ReviewContentJsonAsync(int userId, string prompt, CancellationToken cancellationToken);
    }
}