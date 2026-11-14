namespace RikiPath.Application.IClients;

public interface IAiOcrClient
{
    Task<string?> ScanHandwritingAsync(
        string prompt,
        string base64Image,
        string mimeType,
        CancellationToken cancellationToken = default);
}