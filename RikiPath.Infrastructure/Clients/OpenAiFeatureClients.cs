using RikiPath.Application.IClients;
using RikiPath.Application.IServices;

namespace RikiPath.Infrastructure.Clients;

public sealed class OpenAiLearningPathClient(OpenAiClient client, IAiUsageQuotaService quota) : IAiLearningPathClient
{
    public async Task<string> GenerateLearningPathJsonAsync(int userId, string prompt, CancellationToken cancellationToken)
    {
        await quota.EnsureWithinQuotaAsync(userId, AiFeature.LearningPath, cancellationToken);
        var result = await client.CompleteAsync(client.Settings.LearningPathModel,
            "Bạn là cố vấn ôn luyện JLPT cho người Việt. Phân tích riêng từ vựng, ngữ pháp, đọc hiểu, nghe hiểu theo bằng chứng từng cấp độ được cung cấp. " +
            "Kanji là nội dung hỗ trợ từ vựng, không phải kỹ năng đánh giá thứ năm. Không tự nâng cấp độ hoặc suy từ điểm tổng. " +
            "Chỉ dùng dữ liệu học viên và danh mục lesson được cung cấp. Không bịa lesson ID. " +
            "Sắp xếp lộ trình từ nền tảng hiện tại đến mục tiêu, đánh giá thực tế thời gian ôn thi. " +
            "So sánh thời gian học đã chọn và các kịch bản tăng thời gian; chỉ nêu khả thi có điều kiện, không cam kết chắc chắn đỗ. " +
            "Giữ ngân sách bắt buộc mỗi ngày, chia nhỏ nội dung Lesson khi cần và dành ba ngày trước thi để nghỉ. " +
            "Chỉ trả về JSON object đúng schema trong yêu cầu, bằng tiếng Việt, không Markdown.", prompt, true, cancellationToken,
            reasoningEffort: SupportsReasoning(client.Settings.LearningPathModel) ? client.Settings.LearningPathReasoningEffort : null,
            maxOutputTokens: client.Settings.LearningPathMaxOutputTokens);
        await quota.RecordUsageAsync(userId, AiFeature.LearningPath, result.TotalTokens, cancellationToken);
        return result.Text;
    }

    private static bool SupportsReasoning(string model) => model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase)
        || model.StartsWith("gpt-6", StringComparison.OrdinalIgnoreCase) || model.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
        || model.StartsWith("o4", StringComparison.OrdinalIgnoreCase);
}

public sealed class OpenAiGradingClient(OpenAiClient client, IAiUsageQuotaService quota) : IAiGradingClient
{
    public async Task<string> GradeSubmissionJsonAsync(int userId, string promptText, CancellationToken cancellationToken)
    {
        await quota.EnsureWithinQuotaAsync(userId, AiFeature.Grading, cancellationToken);
        var result = await client.CompleteAsync(client.Settings.GradingModel,
            "Bạn là giám khảo tiếng Nhật theo rubric JLPT. Chỉ trả JSON object hợp lệ đúng schema được yêu cầu.",
            promptText, true, cancellationToken);
        await quota.RecordUsageAsync(userId, AiFeature.Grading, result.TotalTokens, cancellationToken);
        return result.Text;
    }
}

public sealed class OpenAiContentAuthoringClient(OpenAiClient client) : IAiContentAuthoringClient
{
    public async Task<string> GenerateContentJsonAsync(int userId, string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        => (await client.CompleteAsync(client.Settings.ContentModel, systemPrompt + "\nChỉ trả JSON object hợp lệ.", userPrompt, true, cancellationToken)).Text;
}

public sealed class OpenAiEssayGradingClient(OpenAiClient client, IAiUsageQuotaService quota) : IAiEssayGradingClient
{
    public async Task<string> GenerateGradingJsonAsync(int userId, string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        await quota.EnsureWithinQuotaAsync(userId, AiFeature.Grading, cancellationToken);
        var result = await client.CompleteAsync(client.Settings.GradingModel,
            systemPrompt + "\nChỉ trả JSON object hợp lệ.", userPrompt, true, cancellationToken);
        await quota.RecordUsageAsync(userId, AiFeature.Grading, result.TotalTokens, cancellationToken);
        return result.Text;
    }

    public async Task<string> GenerateGradingJsonAsync(int userId, string systemPrompt, string userPrompt,
        CancellationToken cancellationToken, int maxOutputTokens)
    {
        await quota.EnsureWithinQuotaAsync(userId, AiFeature.Grading, cancellationToken);
        var result = await client.CompleteAsync(client.Settings.GradingModel,
            systemPrompt + "\nChỉ trả JSON object hợp lệ.", userPrompt, true,
            cancellationToken, maxOutputTokens: Math.Clamp(maxOutputTokens, 1, 4000));
        await quota.RecordUsageAsync(userId, AiFeature.Grading, result.TotalTokens, cancellationToken);
        return result.Text;
    }
}

public sealed class OpenAiContentReviewClient(OpenAiClient client) : IAiContentReviewClient
{
    public async Task<string> ReviewContentJsonAsync(int userId, string prompt, CancellationToken cancellationToken)
        => (await client.CompleteAsync(client.Settings.ReviewModel,
            "Kiểm tra nội dung học tiếng Nhật theo yêu cầu. Chỉ trả JSON object hợp lệ đúng schema được cung cấp.", prompt, true, cancellationToken)).Text;
}

// Retain the existing application interface so essay upload callers stay compatible.
public sealed class OpenAiOcrClient(OpenAiClient client) : IAiOcrClient
{
    public async Task<string?> ScanHandwritingAsync(string prompt, string base64Image, string mimeType, CancellationToken cancellationToken = default)
    {
        if (mimeType is not ("image/png" or "image/jpeg" or "image/webp" or "image/gif"))
            throw new ArgumentException("Ảnh OCR phải là PNG, JPEG, WEBP hoặc GIF.", nameof(mimeType));
        return (await client.CompleteAsync(client.Settings.OcrModel,
            "Bạn đọc chữ viết tay tiếng Nhật. Thực hiện đúng định dạng đầu ra được yêu cầu. Không tự sửa hoặc bịa văn bản trong ảnh.",
            new object[]
            {
                new { type = "text", text = prompt },
                new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
            }, false, cancellationToken)).Text;
    }
}
