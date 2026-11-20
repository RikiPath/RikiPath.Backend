//using System.Text.Json;
//using Microsoft.AspNetCore.Http;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging.Abstractions;
//using RikiPath.WebApi.Middleware;

//namespace RikiPath.Tests;

//public sealed class GlobalApiExceptionHandlerTests
//{
//    private static async Task<(DefaultHttpContext Context, JsonElement Body)> HandleAsync(Exception exception)
//    {
//        var context = new DefaultHttpContext { TraceIdentifier = "trace-0001" };
//        context.Response.Body = new MemoryStream();
//        var handled = await new GlobalApiExceptionHandler(NullLogger<GlobalApiExceptionHandler>.Instance)
//            .TryHandleAsync(context, exception, default);
//        Assert.True(handled);
//        context.Response.Body.Position = 0;
//        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
//        return (context, JsonDocument.Parse(text).RootElement.Clone());
//    }

//    [Fact]
//    public async Task Unhandled_exception_returns_generic_500_without_leaking_details_and_sets_correlation_id()
//    {
//        var secret = new InvalidOperationException("SELECT * FROM \"Users\" WHERE password='hunter2'");
//        var (context, body) = await HandleAsync(secret);

//        Assert.Equal(500, context.Response.StatusCode);
//        Assert.Equal("trace-0001", context.Response.Headers["X-Correlation-Id"].ToString());
//        Assert.Equal("INTERNAL_ERROR", body.GetProperty("errorCode").GetString());
//        Assert.False(body.GetProperty("isSuccess").GetBoolean());
//        Assert.Equal("Hệ thống gặp sự cố. Vui lòng thử lại sau.", body.GetProperty("errorMessage").GetString());
//        var raw = body.GetRawText();
//        Assert.DoesNotContain("SELECT", raw);
//        Assert.DoesNotContain("hunter2", raw);
//        Assert.DoesNotContain(nameof(InvalidOperationException), raw);
//        Assert.DoesNotContain("   at ", raw);
//    }

//    [Fact]
//    public async Task Known_exceptions_map_to_stable_codes()
//    {
//        var (forbidden, forbiddenBody) = await HandleAsync(new UnauthorizedAccessException());
//        Assert.Equal(403, forbidden.Response.StatusCode);
//        Assert.Equal("FORBIDDEN", forbiddenBody.GetProperty("errorCode").GetString());

//        var (concurrency, concurrencyBody) = await HandleAsync(new DbUpdateConcurrencyException("row changed"));
//        Assert.Equal(409, concurrency.Response.StatusCode);
//        Assert.Equal("CONCURRENCY_CONFLICT", concurrencyBody.GetProperty("errorCode").GetString());

//        var (timeout, timeoutBody) = await HandleAsync(new TimeoutException("upstream"));
//        Assert.Equal(504, timeout.Response.StatusCode);
//        Assert.Equal("UPSTREAM_TIMEOUT", timeoutBody.GetProperty("errorCode").GetString());
//    }
//}
