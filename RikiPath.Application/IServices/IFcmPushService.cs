using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RikiPath.Application.IServices
{
    public class FcmPushPayload
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public Dictionary<string, string> Data { get; set; } = new();
    }

    public class FcmSendResult
    {
        public bool Success { get; set; }
        public int TotalTokens { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> SuccessfulTokens { get; set; } = new();
        public List<string> FailedTokens { get; set; } = new();
        public List<string> DeactivatedTokens { get; set; } = new();
        public string? Note { get; set; }
        public bool HasDeferredRetry { get; set; }
        public int? RecommendedBackoffSeconds { get; set; }
    }

    public interface IFcmPushService
    {
        Task<FcmSendResult> SendPushToUserAsync(int userId, FcmPushPayload payload, CancellationToken cancellationToken = default);
        Task<FcmSendResult> SendPushToTokenAsync(string token, FcmPushPayload payload, CancellationToken cancellationToken = default);
        Task<FcmSendResult> SendPushToTokensAsync(IEnumerable<string> tokens, FcmPushPayload payload, CancellationToken cancellationToken = default);
    }
}
