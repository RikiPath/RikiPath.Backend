namespace RikiPath.Application.IClients;

public interface IAiMockTestExplanationClient
{
    Task<string?> ExplainJsonAsync(int userId, string prompt, IReadOnlyList<string> images,
        string? transcript, CancellationToken cancellationToken);
}
