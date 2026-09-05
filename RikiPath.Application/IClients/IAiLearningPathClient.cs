namespace RikiPath.Application.IClients
{
    public interface IAiLearningPathClient
    {
        Task<string> GenerateLearningPathJsonAsync(string prompt, CancellationToken cancellationToken);
    }
}
