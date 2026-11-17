using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Middleware;

/// <summary>
/// Chuyển mọi exception chưa xử lý thành <see cref="ApiResponse{T}"/> JSON chuẩn.
/// Không bao giờ trả về nội dung exception, stack trace hoặc câu lệnh SQL cho client.
/// </summary>
public sealed class GlobalApiExceptionHandler(ILogger<GlobalApiExceptionHandler> logger) : IExceptionHandler
{
    public const string CorrelationHeader = "X-Correlation-Id";
    public const string InternalErrorMessage = "Hệ thống gặp sự cố. Vui lòng thử lại sau.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;
        var clientAborted = httpContext.RequestAborted.IsCancellationRequested;
        if (exception is OperationCanceledException && clientAborted)
        {
            // Client đã ngắt kết nối: không còn ai nhận phản hồi.
            logger.LogInformation("Request {TraceId} bị client hủy ({Method} {Path}).",
                traceId, httpContext.Request.Method, httpContext.Request.Path.Value);
            return true;
        }

        var (status, code, message) = Classify(exception);
        var category = exception.GetType().Name;
        if ((int)status >= 500)
            logger.LogError("Lỗi chưa xử lý [{Category}] {Code} trace={TraceId} {Method} {Path}",
                category, code, traceId, httpContext.Request.Method, httpContext.Request.Path.Value);
        else
            logger.LogWarning("Xung đột/từ chối [{Category}] {Code} trace={TraceId} {Method} {Path}",
                category, code, traceId, httpContext.Request.Method, httpContext.Request.Path.Value);

        if (httpContext.Response.HasStarted) return false;
        httpContext.Response.Clear();
        httpContext.Response.StatusCode = (int)status;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        httpContext.Response.Headers[CorrelationHeader] = traceId;
        var body = ApiResponse<object>.Fail(message, status, code);
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions), cancellationToken);
        return true;
    }

    internal static (HttpStatusCode Status, string Code, string Message) Classify(Exception exception)
    {
        switch (exception)
        {
            case UnauthorizedAccessException:
                return (HttpStatusCode.Forbidden, "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.");
            case DbUpdateConcurrencyException:
                return (HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", "Dữ liệu vừa được thay đổi bởi thao tác khác. Vui lòng tải lại.");
            case DbUpdateException db:
                switch (SqlStateOf(db))
                {
                    case "23505":
                        return (HttpStatusCode.Conflict, "DUPLICATE_REQUEST", "Yêu cầu đã được xử lý hoặc dữ liệu bị trùng.");
                    case "23P01":
                        return (HttpStatusCode.Conflict, "MENTOR_SCHEDULE_CONFLICT", "Lịch dạy bị trùng với lịch đã xác nhận. Vui lòng chọn khung giờ khác.");
                    case "40001":
                    case "40P01":
                        return (HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", "Hệ thống đang xử lý thao tác đồng thời. Vui lòng thử lại.");
                }
                break;
            case TimeoutException:
            case TaskCanceledException:
                return (HttpStatusCode.GatewayTimeout, "UPSTREAM_TIMEOUT", "Dịch vụ phản hồi quá lâu. Vui lòng thử lại sau.");
        }
        return (HttpStatusCode.InternalServerError, "INTERNAL_ERROR", InternalErrorMessage);
    }

    internal static string? SqlStateOf(Exception exception)
    {
        for (var e = exception.InnerException; e is not null; e = e.InnerException)
            if (e is DbException db && !string.IsNullOrEmpty(db.SqlState)) return db.SqlState;
        return null;
    }
}
