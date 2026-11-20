using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RikiPath.WebApi.Infrastructure;

/// <summary>
/// Logs slow EF Core SQL queries (> 500 ms) safely without logging parameter values or sensitive data.
/// </summary>
public sealed class SlowDbCommandInterceptor(ILogger<SlowDbCommandInterceptor> logger) : DbCommandInterceptor
{
    private const long SlowCommandThresholdMs = 500;

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        LogIfSlow(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void LogIfSlow(DbCommand command, CommandExecutedEventData eventData)
    {
        var elapsedMs = (long)eventData.Duration.TotalMilliseconds;
        if (elapsedMs < SlowCommandThresholdMs) return;

        var raw = (command.CommandText ?? string.Empty).ReplaceLineEndings(" ").Trim();
        var summary = raw.Length > 120 ? raw[..120] + "..." : raw;
        logger.LogWarning(
            "Slow DB query detected duration={ElapsedMs}ms paramCount={ParamCount} sqlPreview={SqlPreview}",
            elapsedMs,
            command.Parameters.Count,
            summary);
    }
}
