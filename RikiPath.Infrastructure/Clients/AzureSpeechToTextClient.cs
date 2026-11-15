using RikiPath.Application.IClients;
using RikiPath.Domain;

namespace RikiPath.Infrastructure.Clients
{
    public class AzureSpeechToTextClient(IHttpClientFactory httpClientFactory, AppSettings appSettings) : IAzureSpeechClient
    {
        private const string SubscriptionKeyHeader = "Ocp-Apim-Subscription-Key";
        private const string DefaultLanguage = "ja-JP";



        private (string Region, string Key, string Language) GetSettings()
        {
            var settings = appSettings.AzureSpeechSettings
                ?? throw new InvalidOperationException("AzureSpeech chưa được cấu hình trong appsettings.");

            if (string.IsNullOrWhiteSpace(settings.Region))
                throw new InvalidOperationException("AzureSpeech:Region chưa được cấu hình.");

            if (string.IsNullOrWhiteSpace(settings.SubscriptionKey))
                throw new InvalidOperationException("AzureSpeech:SubscriptionKey chưa được cấu hình.");

            var language = string.IsNullOrWhiteSpace(settings.Language) ? DefaultLanguage : settings.Language.Trim();
            return (settings.Region.Trim(), settings.SubscriptionKey.Trim(), language);
        }

        // ---------------------------------------------------------------------------------------
        // 1) Lấy token tạm thời cho FE
        // ---------------------------------------------------------------------------------------
        public async Task<string> IssueTokenAsync(CancellationToken cancellationToken = default)
        {
            var (region, key, _) = GetSettings();

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"https://{region}.api.cognitive.microsoft.com/sts/v1.0/issueToken")
            {
                // Body rỗng nhưng vẫn gửi Content-Length: 0 (Azure có thể trả 411 nếu thiếu)
                Content = new StringContent(string.Empty)
            };
            request.Headers.Add(SubscriptionKeyHeader, key);

            var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Azure Speech issueToken trả về {(int)response.StatusCode}.", null, response.StatusCode);
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

    }
}
