using RikiPath.Application.IClients;
using RikiPath.Application.IServices;

namespace RikiPath.Application.Services;

public class OcrService(IAiOcrClient client) : IOcrService
{
    public async Task<string> RecognizeJapaneseAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default)
    {
        var mimeType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => throw new ArgumentException("Ảnh OCR phải là PNG, JPEG, WEBP hoặc GIF.")
        };
        using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer, cancellationToken);
        return await client.ScanHandwritingAsync(
            "Trích xuất chính xác văn bản tiếng Nhật trong ảnh. Chỉ trả văn bản, không Markdown hoặc lời giải thích.",
            Convert.ToBase64String(buffer.ToArray()), mimeType, cancellationToken) ?? string.Empty;
    }
}
