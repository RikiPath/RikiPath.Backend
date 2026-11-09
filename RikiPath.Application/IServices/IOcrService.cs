namespace RikiPath.Application.IServices;

public interface IOcrService
{
    Task<string> RecognizeJapaneseAsync(
        Stream imageStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
