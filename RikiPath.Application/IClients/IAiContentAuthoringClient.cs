namespace RikiPath.Application.IClients;

/// <summary>
/// Gọi AI để soạn nội dung hỗ trợ content author (Kanji, Vocabulary, GrammarPattern, Lesson, ...).
/// Tách riêng khỏi IAiLearningPathClient vì mục đích khác hẳn (soạn nội dung, không phải lập lộ trình học).
/// </summary>
public interface IAiContentAuthoringClient
{
    /// <summary>
    /// Gửi systemPrompt (quy tắc cố định cho loại nội dung cần soạn) + userPrompt (yêu cầu tự do của
    /// content author, đã kèm dữ liệu ngữ cảnh như bài học/cấp độ/danh sách đã có trong DB) tới AI,
    /// trả về JSON thô (string) do AI sinh ra.
    /// </summary>
    Task<string> GenerateContentJsonAsync(
        int userId, string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}