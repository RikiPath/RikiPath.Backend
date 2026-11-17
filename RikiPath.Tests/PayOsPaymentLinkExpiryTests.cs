using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RikiPath.Application.IClients;
using RikiPath.Domain;
using RikiPath.Infrastructure.Clients;

namespace RikiPath.Tests;

// PayOS "Create payment link": trường tùy chọn "expiredAt" (Unix giây, Int32) KHÔNG thuộc chữ ký;
// chữ ký chỉ gồm amount, cancelUrl, description, orderCode, returnUrl (thứ tự alphabet).
public sealed class PayOsPaymentLinkExpiryTests
{
    private const string ChecksumKey = "qa-checksum-key";

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public HttpRequestMessage? Request { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"code":"00","desc":"success","data":{"checkoutUrl":"https://pay.payos.vn/web/qa","paymentLinkId":"plink-qa","qrCode":"qr"}}""",
                    Encoding.UTF8, "application/json")
            };
        }
    }

    private static (PayOsClient Client, CapturingHandler Handler) Create()
    {
        var handler = new CapturingHandler();
        var client = new PayOsClient(new HttpClient(handler), new AppSettings
        {
            PayOs = new PayOsSettings { ClientId = "client-qa", ApiKey = "api-qa", ChecksumKey = ChecksumKey }
        });
        return (client, handler);
    }

    private static string ExpectedSignature(long orderCode, int amount, string description, string returnUrl, string cancelUrl)
    {
        var data = $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ChecksumKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    [Fact]
    public async Task Expiry_is_sent_as_unix_seconds_and_is_not_part_of_the_signature()
    {
        var (client, handler) = Create();
        var expiresAt = new DateTime(2026, 12, 1, 3, 4, 5, DateTimeKind.Utc);

        var result = await client.CreatePaymentLinkAsync(700_000_123L, 1_500_000, "COURSE 123",
            "https://rikipath.test/my-courses", "https://rikipath.test/my-courses", expiresAt, CancellationToken.None);

        Assert.Equal("https://pay.payos.vn/web/qa", result.CheckoutUrl);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        using var body = JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;
        Assert.Equal(JsonValueKind.Number, root.GetProperty("expiredAt").ValueKind);
        Assert.Equal((int)new DateTimeOffset(expiresAt).ToUnixTimeSeconds(), root.GetProperty("expiredAt").GetInt32());
        Assert.Equal(1796094245, root.GetProperty("expiredAt").GetInt32());
        Assert.Equal(700_000_123L, root.GetProperty("orderCode").GetInt64());
        Assert.Equal(1_500_000, root.GetProperty("amount").GetInt32());
        Assert.Equal(ExpectedSignature(700_000_123L, 1_500_000, "COURSE 123", "https://rikipath.test/my-courses", "https://rikipath.test/my-courses"),
            root.GetProperty("signature").GetString());
    }

    [Fact]
    public async Task Legacy_overload_omits_expiry_and_keeps_the_same_signature()
    {
        var (client, handler) = Create();

        await client.CreatePaymentLinkAsync(700_000_124L, 200_000, "COURSE 124",
            "https://rikipath.test/r", "https://rikipath.test/c", CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Body!);
        Assert.False(body.RootElement.TryGetProperty("expiredAt", out _));
        Assert.Equal(ExpectedSignature(700_000_124L, 200_000, "COURSE 124", "https://rikipath.test/r", "https://rikipath.test/c"),
            body.RootElement.GetProperty("signature").GetString());
    }

    private sealed class LegacyGateway : IPaymentGatewayClient
    {
        public int Calls;
        public Task<PaymentLinkResult> CreatePaymentLinkAsync(long orderCode, int amount, string description, string returnUrl,
            string cancelUrl, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new PaymentLinkResult("https://pay.invalid/legacy", "legacy", ""));
        }
        public PaymentCallbackResult VerifyWebhook(RikiPath.Application.Requests.Payments.PayOsWebhookRequest webhook) => throw new NotSupportedException();
        public Task<PaymentLinkInformation> GetPaymentInformationAsync(long orderCode, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Gateways_without_expiry_support_fall_back_to_the_legacy_method()
    {
        var legacy = new LegacyGateway();
        IPaymentGatewayClient gateway = legacy;
        var link = await gateway.CreatePaymentLinkAsync(1, 1000, "QA", "https://r", "https://c", DateTime.UtcNow.AddMinutes(10), CancellationToken.None);
        Assert.Equal("https://pay.invalid/legacy", link.CheckoutUrl);
        Assert.Equal(1, legacy.Calls);
    }
}
