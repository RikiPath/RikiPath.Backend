using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RikiPath.Application;
using RikiPath.Application.EmailTemplates;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Services;

/// <summary>Periodically dispatches per-learner SRS study reminders once per local day when due cards exist.</summary>
public sealed class SrsReminderWorker(
    IServiceScopeFactory scopes,
    ILogger<SrsReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial short delay so migrations/schema readiness complete on cold startup
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        await RunPassAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunPassAsync(stoppingToken);
        }
    }

    private async Task RunPassAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var emailService = scope.ServiceProvider.GetService<IEmailService>();
            var emailTemplates = scope.ServiceProvider.GetService<IEmailTemplateService>();

            var candidates = await unitOfWork.UserSrsPreferences.GetDueReminderCandidatesAsync(stoppingToken);
            if (candidates.Count == 0)
                return;

            var utcNow = DateTime.UtcNow;
            foreach (var pref in candidates)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    await ProcessCandidateAsync(
                        unitOfWork,
                        notificationService,
                        emailService,
                        emailTemplates,
                        pref,
                        utcNow,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "SRS reminder check failed for user {UserId}; will retry next tick.", pref.UserId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SRS reminder worker pass encountered an unexpected error.");
        }
    }

    private async Task ProcessCandidateAsync(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IEmailService? emailService,
        IEmailTemplateService? emailTemplates,
        UserSrsPreference pref,
        DateTime utcNow,
        CancellationToken stoppingToken)
    {
        var user = pref.UserAccount;
        if (user is null || user.IsDeleted)
            return;

        var tz = SrsSchedulingHelper.ResolveTimeZoneOrDefault(pref.TimezoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
        var localToday = DateOnly.FromDateTime(localNow.Date);

        if (pref.LastReminderSentLocalDate == localToday)
            return;

        var targetTime = new TimeSpan(Math.Clamp(pref.ReminderHour, 0, 23), Math.Clamp(pref.ReminderMinute, 0, 59), 0);
        if (localNow.TimeOfDay < targetTime)
            return;

        if (!ShouldRunOnDay(pref, localNow.DayOfWeek))
            return;

        var dueCards = await unitOfWork.ReviewCards.GetDueQueueAsync(
            pref.UserId,
            utcNow,
            null,
            null,
            null,
            stoppingToken);
        if (dueCards.Count == 0)
            return;

        var studyToday = SrsSchedulingHelper.GetStudyDateInZone(utcNow, pref.TimezoneId);
        var utcWindowStart = studyToday.AddDays(-1);
        var todayLogs = await unitOfWork.ReviewHistories.Query()
            .AsNoTracking()
            .Where(h => h.ReviewCard.UserId == pref.UserId
                && !h.IsDeleted
                && h.ReviewedAt >= utcWindowStart)
            .Select(h => new { h.ReviewedAt, h.PreviousState })
            .ToListAsync(stoppingToken);

        var todayInZone = todayLogs
            .Where(h => SrsSchedulingHelper.GetStudyDateInZone(h.ReviewedAt, pref.TimezoneId) == studyToday)
            .ToList();

        var actionableDueCards = SrsSchedulingHelper.SelectActionableDueCards(
            dueCards,
            pref.DailyNewLimit,
            pref.DailyReviewLimit,
            todayInZone.Count(h => h.PreviousState == RikiPath.Domain.Enums.SrsLearningState.New),
            todayInZone.Count);

        var dueCount = actionableDueCards.Count;
        if (dueCount <= 0)
            return;

        // Strict one-per-local-day reminder idempotency key regardless of later time/locale/frequency edits
        var idempotencyKey = $"srs-reminder:{pref.UserId}:{localToday.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";

        var marked = false;
        UserSrsPreference? lockedPref = null;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.UserAccounts.GetByIdForUpdateAsync(pref.UserId, token);
            var fresh = await unitOfWork.UserSrsPreferences.GetByUserIdAsync(pref.UserId, token);
            if (fresh is null || !fresh.DailyReminderEnabled || fresh.LastReminderSentLocalDate == localToday)
                return;

            var existingNotif = await unitOfWork.Notifications.FirstOrDefaultAsync(
                n => n.IdempotencyKey == idempotencyKey,
                token);
            if (existingNotif is not null)
            {
                // Heal marker if a previous run persisted the notification right before a crash/restart
                fresh.LastReminderSentAtUtc ??= existingNotif.CreatedDate ?? utcNow;
                fresh.LastReminderSentLocalDate = localToday;
                fresh.ModifiedDate = utcNow;
                unitOfWork.UserSrsPreferences.Update(fresh);
                await unitOfWork.SaveChangesAsync(token);
                return;
            }

            var freshTz = SrsSchedulingHelper.ResolveTimeZoneOrDefault(fresh.TimezoneId);
            var freshLocalNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), freshTz);
            var freshTargetTime = new TimeSpan(Math.Clamp(fresh.ReminderHour, 0, 23), Math.Clamp(fresh.ReminderMinute, 0, 59), 0);
            if (freshLocalNow.TimeOfDay < freshTargetTime || !ShouldRunOnDay(fresh, freshLocalNow.DayOfWeek))
                return;

            fresh.LastReminderSentAtUtc = utcNow;
            fresh.LastReminderSentLocalDate = localToday;
            fresh.ModifiedDate = utcNow;
            unitOfWork.UserSrsPreferences.Update(fresh);
            await unitOfWork.SaveChangesAsync(token);
            lockedPref = fresh;
            marked = true;
        }, stoppingToken);

        if (!marked || lockedPref is null)
            return;

        var isJa = string.Equals(lockedPref.PreferredLocale, "ja", StringComparison.OrdinalIgnoreCase);
        var title = isJa
            ? $"本日のSRS復習カードが {dueCount} 枚あります"
            : $"Bạn có {dueCount} thẻ SRS đến hạn ôn hôm nay";
        var message = isJa
            ? $"忘れずに今すぐ {dueCount} 枚のフラッシュカードを復習して、学習ストリークを維持しましょう。"
            : $"Hãy dành vài phút ôn tập {dueCount} thẻ đến hạn hôm nay để giữ vững chuỗi ghi nhớ của bạn trên RikiPath.";

        try
        {
            var notifRes = await notificationService.SendNotificationToUserAsync(
                pref.UserId,
                title,
                message,
                "SrsReminder",
                "/daily-srs/",
                stoppingToken,
                idempotencyKey);

            if (!notifRes.IsSuccess)
                throw new InvalidOperationException(notifRes.ErrorMessage ?? "Failed to dispatch in-app SRS reminder.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var persisted = await unitOfWork.Notifications.FirstOrDefaultAsync(
                n => n.IdempotencyKey == idempotencyKey,
                stoppingToken);
            if (persisted is null)
            {
                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(pref.UserId, token);
                    var fresh = await unitOfWork.UserSrsPreferences.GetByUserIdAsync(pref.UserId, token);
                    if (fresh is not null && fresh.LastReminderSentLocalDate == localToday)
                    {
                        fresh.LastReminderSentLocalDate = null;
                        fresh.ModifiedDate = DateTime.UtcNow;
                        unitOfWork.UserSrsPreferences.Update(fresh);
                        await unitOfWork.SaveChangesAsync(token);
                    }
                }, stoppingToken);
            }
            logger.LogWarning(ex, "SRS in-app reminder failed for user {UserId}; armed for safe retry.", pref.UserId);
            return;
        }

        if (lockedPref.EmailReminderEnabled
            && user.IsEmailVerified
            && emailService is not null
            && emailTemplates is not null
            && !string.IsNullOrWhiteSpace(user.Email)
            && !user.IsDeleted)
        {
            try
            {
                var learnerName = UserNameHelper.FormatFullName(user.FirstName, user.LastName);
                var rendered = emailTemplates.BuildStudyNotification(new StudyNotificationEmailData(
                    learnerName,
                    title,
                    isJa ? "SRS学習リマインダー" : "Nhắc ôn tập SRS",
                    title,
                    message,
                    isJa ? "復習待ち" : "Thẻ đến hạn",
                    $"{dueCount}",
                    [
                        new EmailDetailItem(isJa ? "対象日" : "Ngày học", localToday.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                        new EmailDetailItem(isJa ? "現在のストリーク" : "Chuỗi học", $"{user.CurrentStreak}"),
                    ],
                    SakuraEmailTone.Sakura,
                    null,
                    null,
                    isJa ? "SRS復習を始める" : "Bắt đầu ôn tập ngay",
                    "/daily-srs/"));

                var sendRes = await emailService.SendEmailAsync(user.Email, rendered, stoppingToken);
                if (!sendRes.IsSuccess)
                {
                    logger.LogInformation("SRS reminder email skipped or provider unavailable for user {UserId}: {Reason}", pref.UserId, sendRes.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SRS email reminder delivery failed for user {UserId}.", pref.UserId);
            }
        }
    }

    private static bool ShouldRunOnDay(UserSrsPreference pref, DayOfWeek day)
    {
        if (string.Equals(pref.ReminderFrequency, "Weekdays", StringComparison.OrdinalIgnoreCase))
            return day is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

        if (string.Equals(pref.ReminderFrequency, "CustomDays", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(pref.ReminderDaysJson))
                return false;

            try
            {
                var days = JsonSerializer.Deserialize<List<int>>(pref.ReminderDaysJson);
                return days is { Count: > 0 } && days.Contains((int)day);
            }
            catch
            {
                return false;
            }
        }

        return true;
    }
}
