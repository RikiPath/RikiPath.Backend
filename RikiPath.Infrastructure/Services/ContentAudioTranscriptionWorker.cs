using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;

namespace RikiPath.Infrastructure.Services;

public sealed class ContentAudioTranscriptionWorker(
    IServiceScopeFactory scopes, ContentAudioTranscriptionWakeup wakeup,
    ILogger<ContentAudioTranscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Stay idle without querying PostgreSQL. QueueAsync/RetryAsync signals
                // this worker only when an explicit transcription request is made.
                await wakeup.WaitAsync(stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = scopes.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IContentAudioTranscriptionService>();
                    if (!await service.ProcessNextAsync(stoppingToken)) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Content audio transcription request failed; it will remain available for an explicit retry.");
            }
        }
    }
}
