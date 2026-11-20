using System.Diagnostics;

namespace RikiPath.WebApi.Middleware;

/// <summary>
/// Attaches correlation ID, hardens HTTP security headers, monitors request duration,
/// and logs security/error status codes (401/403/429/5xx) without ever logging tokens, query secrets, or passwords.
/// </summary>
public sealed class RequestObservabilityAndSecurityMiddleware(
    RequestDelegate next,
    ILogger<RequestObservabilityAndSecurityMiddleware> logger)
{
    public const string CorrelationHeader = GlobalApiExceptionHandler.CorrelationHeader;
    private const long SlowRequestThresholdMs = 2000;

    public async Task InvokeAsync(HttpContext context)
    {
        var rawCorrelation = context.Request.Headers[CorrelationHeader].FirstOrDefault();
        var correlationId = IsSafeCorrelationId(rawCorrelation) ? rawCorrelation!.Trim() : context.TraceIdentifier;
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers[CorrelationHeader] = correlationId;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.TryAdd("Permissions-Policy", "camera=(self), microphone=(self), geolocation=()");
            headers.Remove("X-Powered-By");
            headers.Remove("Server");
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
        });

        var sw = Stopwatch.StartNew();
        await next(context);
        sw.Stop();

        var statusCode = context.Response.StatusCode;
        var elapsedMs = sw.ElapsedMilliseconds;
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";

        if (elapsedMs >= SlowRequestThresholdMs)
        {
            logger.LogWarning(
                "Slow HTTP request {Method} {Path} responded {StatusCode} in {ElapsedMs}ms correlation={CorrelationId}",
                method, path, statusCode, elapsedMs, correlationId);
        }

        if (statusCode is 401 or 403 or 429)
        {
            logger.LogWarning(
                "Security/throttle response {StatusCode} on {Method} {Path} in {ElapsedMs}ms correlation={CorrelationId}",
                statusCode, method, path, elapsedMs, correlationId);
        }
    }

    private static bool IsSafeCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64) return false;
        foreach (var ch in value)
        {
            if (!(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')) return false;
        }
        return true;
    }
}
