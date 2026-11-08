namespace RikiPath.Application.IClients;

/// <summary>Gọi AI để chấm bài luận (作文) của học viên theo rubric JLPT.</summary>
public interface IAiEssayGradingClient
{
    /// <summary>Gửi systemPrompt (rubric/quy tắc chấm cố định) + userPrompt (bài luận + ngữ cảnh
    /// cấp độ/đề bài) tới AI, trả về JSON thô (string) do AI sinh ra.</summary>
    Task<string> GenerateGradingJsonAsync(
        int userId, string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}