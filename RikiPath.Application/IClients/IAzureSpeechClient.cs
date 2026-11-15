
namespace RikiPath.Application.IClients
{
    public interface IAzureSpeechClient
    {

        Task<string> IssueTokenAsync(CancellationToken cancellationToken = default);
    }
}
