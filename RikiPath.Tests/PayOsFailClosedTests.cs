using RikiPath.Application.Requests.Payments;
using RikiPath.Domain;
using RikiPath.Infrastructure.Clients;

namespace RikiPath.Tests;

public sealed class PayOsFailClosedTests
{
    [Fact]
    public async Task Missing_configuration_cannot_create_or_confirm_payment()
    {
        var client = new PayOsClient(new HttpClient(), new AppSettings { PayOs = new PayOsSettings() });

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CreatePaymentLinkAsync(
            12345, 1000, "QA", "https://example.invalid/return", "https://example.invalid/cancel", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetPaymentInformationAsync(12345, CancellationToken.None));

        var callback = client.VerifyWebhook(new PayOsWebhookRequest
        {
            Success = true,
            Data = new PayOsWebhookData { OrderCode = 12345, Amount = 1000, Code = "00" }
        });
        Assert.False(callback.IsValidSignature);
    }
}
