using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;

namespace RikiPath.Infrastructure.Services;

public sealed class ContentAudioTranscriptionWorker(
    IServiceScopeFactory scopes, ILogger<ContentAudioTranscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IContentAudioTranscriptionService>();
                if (!await service.ProcessNextAsync(stoppingToken))
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch
            {
                logger.LogWarning("Content audio transcription storage is unavailable; processing will retry.");
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
