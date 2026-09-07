namespace RikiPath.Application.IClients
{
    public interface IAiLearningPathClient
    {
        Task<string> GenerateLearningPathJsonAsync(
            int userId, string prompt, CancellationToken cancellationToken);
    }
}
