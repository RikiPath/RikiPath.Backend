namespace RikiPath.Application.IClients;

public interface IKanjiOcrClient
{
    Task<string?> RecognizeAsync(string base64Png, CancellationToken cancellationToken);
}
