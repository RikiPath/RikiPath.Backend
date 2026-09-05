namespace RikiPath.Application.IClients
{
    public interface IAiGradingClient
    {
        /// <summary>
        /// Sends the full grading prompt (system + user content already merged by the caller)
        /// and returns the raw JSON string the model replied with. Must NOT swallow provider
        /// errors — let them bubble up so the caller can map to 502 Bad Gateway.
        /// </summary>
        Task<string> GradeSubmissionJsonAsync(string promptText, CancellationToken cancellationToken);
    }
}
