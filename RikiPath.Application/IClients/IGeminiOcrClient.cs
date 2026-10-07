namespace RikiPath.Application.IClients;

public interface IGeminiOcrClient
{
    Task<string?> ScanHandwritingAsync(
        string prompt,
        string base64Image,
        string mimeType,
        CancellationToken cancellationToken = default);
}