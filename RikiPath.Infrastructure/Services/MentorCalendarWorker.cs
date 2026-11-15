using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Services;

public sealed class MentorCalendarWorker(IServiceScopeFactory scopes, ILogger<MentorCalendarWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IMentorCalendarService>();
            if (!service.IsConfigured) continue;
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ids = await db.Set<MentorCalendarConnection>().Where(x => x.ProtectedRefreshToken != null)
                    .Select(x => x.MentorId).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    try { await service.SyncAsync(id, stoppingToken); }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("Calendar sync failed for mentor {MentorId}; will retry.", id); }
                }
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("Calendar storage is not ready; sync will retry."); }
        }
    }
}
