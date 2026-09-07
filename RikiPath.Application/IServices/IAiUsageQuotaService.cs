namespace RikiPath.Application.IServices
{
    public enum AiFeature
    {
        Grading,
        LearningPath
    }

    // Kiểm tra & ghi nhận quota sử dụng AI theo từng user/theo ngày (giờ Việt Nam), dùng chung
    // cho AiGradingClient và AiLearningPathClient để chống spam và khống chế chi phí Groq.
    // Giới hạn (số request/ngày, số token/ngày) đọc từ AppSettings.Ai.RateLimit.
    public interface IAiUsageQuotaService
    {
        // Ném AiQuotaExceededException nếu user đã vượt giới hạn request hoặc token trong ngày
        // cho feature tương ứng. PHẢI gọi TRƯỚC khi gọi provider AI.
        Task EnsureWithinQuotaAsync(int userId, AiFeature feature, CancellationToken cancellationToken);

        // Cộng dồn số token thực tế đã dùng (lấy từ usage.total_tokens trong response của provider)
        // và tăng số lượt gọi lên 1. PHẢI gọi SAU khi gọi provider AI thành công (kể cả lần retry).
        Task RecordUsageAsync(int userId, AiFeature feature, int tokensUsed, CancellationToken cancellationToken);
    }
}