using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Application.Requests.Payments;
using RikiPath.Domain;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RikiPath.Infrastructure.Clients
{
    /// <summary>
    /// PayOS merchant API client (https://payos.vn/docs/api/). Every failure is raised as a classified
    /// <see cref="PaymentGatewayException"/>; logs carry order code, HTTP status, PayOS code/desc and duration only —
    /// never the client id, API key, checksum key, headers or the signed payload.
    /// </summary>
    public class PayOsClient(HttpClient httpClient, AppSettings appSettings, ILogger<PayOsClient>? logger = null) : IPaymentGatewayClient
    {
        private const string PaymentRequestsUrl = "https://api-merchant.payos.vn/v2/payment-requests";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

        /// <summary>Hosted-checkout hosts the learner may be redirected to. Anything else returned by the provider is refused.</summary>
        private static readonly HashSet<string> CheckoutHostAllowlist = new(StringComparer.OrdinalIgnoreCase) { "pay.payos.vn" };

        // PayOS "description": ≤ 25 ký tự; ≤ 9 ký tự với tài khoản ngân hàng không liên kết qua payOS.
        public const int MaxDescriptionLength = 25;

        private static PayOsSettings RequireConfigured(PayOsSettings? payOs)
        {
            if (payOs == null || string.IsNullOrWhiteSpace(payOs.ClientId)
                || string.IsNullOrWhiteSpace(payOs.ApiKey)
                || string.IsNullOrWhiteSpace(payOs.ChecksumKey))
                throw new PaymentGatewayException(PaymentGatewayFailure.NotConfigured, "PayOS is not configured.");
            return payOs;
        }

        public Task<PaymentLinkInformation> GetPaymentInformationAsync(long orderCode, CancellationToken cancellationToken)
            => GetPaymentInformationAsync(orderCode.ToString(CultureInfo.InvariantCulture), cancellationToken);

        public async Task<bool> CancelPaymentLinkAsync(long orderCode, string? cancellationReason, CancellationToken cancellationToken)
        {
            if (appSettings.PayOs == null || string.IsNullOrWhiteSpace(appSettings.PayOs.ClientId)
                || string.IsNullOrWhiteSpace(appSettings.PayOs.ApiKey)
                || string.IsNullOrWhiteSpace(appSettings.PayOs.ChecksumKey))
            {
                return false;
            }

            var payOs = appSettings.PayOs;
            var orderKey = orderCode.ToString(CultureInfo.InvariantCulture);
            var reason = string.IsNullOrWhiteSpace(cancellationReason) ? "Seat hold expired" : Truncate(cancellationReason.Trim());
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{PaymentRequestsUrl}/{orderKey}/cancel")
                {
                    Content = JsonContent.Create(new { cancellationReason = reason }),
                };
                AddAuthHeaders(request, payOs);
                var (status, root, elapsedMs) = await SendAsync(request, "cancel-payment-link", orderKey, cancellationToken);
                using (root)
                {
                    RequireSuccess(root.RootElement, status, "cancel-payment-link", orderKey);
                    logger?.LogInformation("PayOS payment link cancelled. OrderCode={OrderCode} DurationMs={DurationMs}", orderCode, elapsedMs);
                    return true;
                }
            }
            catch (PaymentGatewayException ex)
            {
                logger?.LogWarning("PayOS cancel-payment-link did not complete cleanly. OrderCode={OrderCode} Failure={Failure} ProviderCode={ProviderCode}",
                    orderCode, ex.Failure, ex.ProviderCode);
                return false;
            }
        }

        public async Task<PaymentLinkInformation> GetPaymentInformationAsync(string paymentRequestId, CancellationToken cancellationToken)
        {
            var payOs = RequireConfigured(appSettings.PayOs);
            if (string.IsNullOrWhiteSpace(paymentRequestId) || paymentRequestId.Any(c => !char.IsLetterOrDigit(c)))
                throw new PaymentGatewayException(PaymentGatewayFailure.Rejected, "Invalid payment request id.");

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{PaymentRequestsUrl}/{paymentRequestId}");
            AddAuthHeaders(request, payOs);
            var (status, root, _) = await SendAsync(request, "get-payment-info", paymentRequestId, cancellationToken);
            using (root)
            {
                var data = RequireSuccess(root.RootElement, status, "get-payment-info", paymentRequestId);
                try
                {
                    return new PaymentLinkInformation(
                        data.GetProperty("orderCode").GetInt64(),
                        data.GetProperty("amount").GetDecimal(),
                        data.GetProperty("amountPaid").GetDecimal(),
                        data.GetProperty("status").GetString() ?? string.Empty,
                        data.GetProperty("id").GetString() ?? string.Empty);
                }
                catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
                {
                    throw new PaymentGatewayException(PaymentGatewayFailure.InvalidResponse, "PayOS payment info is malformed.", status, inner: ex);
                }
            }
        }

        public Task<PaymentLinkResult> CreatePaymentLinkAsync(
            long orderCode, int amount, string description, string returnUrl, string cancelUrl,
            CancellationToken cancellationToken)
            => CreatePaymentLinkAsync(orderCode, amount, description, returnUrl, cancelUrl, null, cancellationToken);

        public async Task<PaymentLinkResult> CreatePaymentLinkAsync(
            long orderCode, int amount, string description, string returnUrl, string cancelUrl,
            DateTime? expiresAtUtc, CancellationToken cancellationToken)
        {
            var payOs = RequireConfigured(appSettings.PayOs);
            if (orderCode <= 0 || orderCode > 9_007_199_254_740_991L || amount <= 0
                || string.IsNullOrWhiteSpace(description) || description.Length > MaxDescriptionLength)
                throw new PaymentGatewayException(PaymentGatewayFailure.Rejected, "Payment request is outside PayOS limits.");

            // Ký trên đúng 5 field này, đúng thứ tự alphabet, theo doc PayOS "Create Payment Link".
            // "expiredAt" là trường tùy chọn và KHÔNG thuộc chữ ký.
            var signatureData = BuildPaymentRequestSignatureData(orderCode, amount, description, returnUrl, cancelUrl);
            var signature = ComputeHmacSha256(signatureData, payOs.ChecksumKey);

            object requestBody = expiresAtUtc.HasValue
                ? new { orderCode, amount, description, cancelUrl, returnUrl, expiredAt = ToUnixSeconds(expiresAtUtc.Value), signature }
                : new { orderCode, amount, description, cancelUrl, returnUrl, signature };

            using var request = new HttpRequestMessage(HttpMethod.Post, PaymentRequestsUrl)
            {
                Content = JsonContent.Create(requestBody, requestBody.GetType()),
            };
            AddAuthHeaders(request, payOs);

            var orderKey = orderCode.ToString(CultureInfo.InvariantCulture);
            var (status, root, elapsedMs) = await SendAsync(request, "create-payment-link", orderKey, cancellationToken);
            using (root)
            {
                var data = RequireSuccess(root.RootElement, status, "create-payment-link", orderKey);
                var checkoutUrl = data.TryGetProperty("checkoutUrl", out var urlEl) ? urlEl.GetString() : null;
                var paymentLinkId = data.TryGetProperty("paymentLinkId", out var idEl) ? idEl.GetString() : null;
                var echoedCode = data.TryGetProperty("orderCode", out var codeEl) && codeEl.TryGetInt64(out var c) ? c : (long?)null;
                var echoedAmount = data.TryGetProperty("amount", out var amountEl) && amountEl.TryGetInt64(out var a) ? a : (long?)null;

                if (!IsAllowedCheckoutUrl(checkoutUrl) || string.IsNullOrWhiteSpace(paymentLinkId)
                    || (echoedCode.HasValue && echoedCode.Value != orderCode)
                    || (echoedAmount.HasValue && echoedAmount.Value != amount))
                {
                    logger?.LogError("PayOS create-payment-link returned an unexpected link. OrderCode={OrderCode} HasLinkId={HasLinkId} EchoedOrderCodeMatches={CodeMatches} EchoedAmountMatches={AmountMatches}",
                        orderCode, !string.IsNullOrWhiteSpace(paymentLinkId), echoedCode is null || echoedCode == orderCode, echoedAmount is null || echoedAmount == amount);
                    throw new PaymentGatewayException(PaymentGatewayFailure.InvalidResponse, "PayOS returned an invalid checkout link.", status);
                }

                logger?.LogInformation("PayOS payment link created. OrderCode={OrderCode} DurationMs={DurationMs}", orderCode, elapsedMs);
                return new PaymentLinkResult(
                    CheckoutUrl: checkoutUrl!,
                    PaymentLinkId: paymentLinkId!,
                    QrCode: data.TryGetProperty("qrCode", out var qrEl) ? qrEl.GetString() ?? string.Empty : string.Empty);
            }
        }

        public static bool IsAllowedCheckoutUrl(string? url)
            => Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
               && uri.IsDefaultPort
               && string.IsNullOrEmpty(uri.UserInfo)
               && CheckoutHostAllowlist.Contains(uri.Host);

        private static void AddAuthHeaders(HttpRequestMessage request, PayOsSettings payOs)
        {
            request.Headers.Add("x-client-id", payOs.ClientId);
            request.Headers.Add("x-api-key", payOs.ApiKey);
        }

        private async Task<(int Status, JsonDocument Root, long ElapsedMs)> SendAsync(
            HttpRequestMessage request, string operation, string orderKey, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            string body;
            try
            {
                response = await httpClient.SendAsync(request, timeout.Token);
                body = await response.Content.ReadAsStringAsync(timeout.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger?.LogWarning("PayOS {Operation} timed out. OrderKey={OrderKey} DurationMs={DurationMs}", operation, orderKey, stopwatch.ElapsedMilliseconds);
                throw new PaymentGatewayException(PaymentGatewayFailure.Timeout, "PayOS did not respond in time.", inner: ex);
            }
            catch (HttpRequestException ex)
            {
                logger?.LogWarning("PayOS {Operation} network failure. OrderKey={OrderKey} DurationMs={DurationMs} Error={Error}",
                    operation, orderKey, stopwatch.ElapsedMilliseconds, ex.HttpRequestError);
                throw new PaymentGatewayException(PaymentGatewayFailure.Network, "PayOS could not be reached.", inner: ex);
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (!response.IsSuccessStatusCode)
                {
                    var failure = response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => PaymentGatewayFailure.AuthenticationFailed,
                        HttpStatusCode.TooManyRequests => PaymentGatewayFailure.RateLimited,
                        >= HttpStatusCode.InternalServerError => PaymentGatewayFailure.ProviderUnavailable,
                        _ => PaymentGatewayFailure.Rejected,
                    };
                    var (code, desc) = TryReadCodeAndDesc(body);
                    logger?.LogWarning("PayOS {Operation} HTTP failure. OrderKey={OrderKey} HttpStatus={HttpStatus} ProviderCode={ProviderCode} ProviderDesc={ProviderDesc} DurationMs={DurationMs}",
                        operation, orderKey, status, code, desc, elapsed);
                    throw new PaymentGatewayException(failure, $"PayOS {operation} failed with HTTP {status}.", status, code, desc);
                }

                try
                {
                    return (status, JsonDocument.Parse(body), elapsed);
                }
                catch (JsonException ex)
                {
                    logger?.LogWarning("PayOS {Operation} returned non-JSON. OrderKey={OrderKey} HttpStatus={HttpStatus} DurationMs={DurationMs}", operation, orderKey, status, elapsed);
                    throw new PaymentGatewayException(PaymentGatewayFailure.InvalidResponse, "PayOS response is not JSON.", status, inner: ex);
                }
            }
        }

        private JsonElement RequireSuccess(JsonElement root, int status, string operation, string orderKey)
        {
            var code = root.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String ? codeEl.GetString() : null;
            if (code == "00" && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                return data;

            var desc = root.TryGetProperty("desc", out var descEl) && descEl.ValueKind == JsonValueKind.String ? Truncate(descEl.GetString()) : null;
            var failure = code switch
            {
                // 231: "Đơn thanh toán đã tồn tại" — the order code is already used on this PayOS channel.
                "231" => PaymentGatewayFailure.DuplicateOrderCode,
                // 201/214 and similar: channel/credential problems ("Kênh thanh toán không tồn tại", "Cổng thanh toán không tồn tại").
                "201" or "214" => PaymentGatewayFailure.AuthenticationFailed,
                _ => PaymentGatewayFailure.Rejected,
            };
            logger?.LogWarning("PayOS {Operation} rejected. OrderKey={OrderKey} HttpStatus={HttpStatus} ProviderCode={ProviderCode} ProviderDesc={ProviderDesc}",
                operation, orderKey, status, code, desc);
            throw new PaymentGatewayException(failure, $"PayOS {operation} rejected with code {code}.", status, code, desc);
        }

        private static (string? Code, string? Desc) TryReadCodeAndDesc(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return (null, null);
                return (root.TryGetProperty("code", out var c) ? Truncate(c.ToString()) : null,
                    root.TryGetProperty("desc", out var d) ? Truncate(d.ToString()) : null);
            }
            catch (JsonException)
            {
                return (null, null);
            }
        }

        private static string? Truncate(string? value) => value is null ? null : value.Length <= 160 ? value : value[..160];

        public PaymentCallbackResult VerifyWebhook(PayOsWebhookRequest webhook) => VerifyWebhook(webhook, null);

        public PaymentCallbackResult VerifyWebhook(PayOsWebhookRequest webhook, string? rawDataJson)
        {
            var payOs = appSettings.PayOs;
            var d = webhook.Data ?? new PayOsWebhookData();

            if (payOs == null || string.IsNullOrWhiteSpace(payOs.ChecksumKey) || string.IsNullOrWhiteSpace(webhook.Signature))
                return new PaymentCallbackResult(false, false, d.OrderCode, d.Amount, d.Description);

            var dataStr = rawDataJson is not null && TryBuildSignatureDataFromRaw(rawDataJson, out var fromRaw)
                ? fromRaw
                : BuildTypedWebhookSignatureData(d);
            var expectedSignature = ComputeHmacSha256(dataStr, payOs.ChecksumKey);

            var isValidSignature = FixedTimeEqualsHex(expectedSignature, webhook.Signature);
            // Tuyệt đối không coi giao dịch là thành công nếu chữ ký sai, kể cả khi
            // webhook.Success/d.Code báo "thành công" — có thể là giả mạo.
            var isSuccess = isValidSignature && webhook.Success && d.Code == "00";

            return new PaymentCallbackResult(isValidSignature, isSuccess, d.OrderCode, d.Amount, d.Description);
        }

        /// <summary>
        /// PayOS webhook signature data: every key of <c>data</c> sorted alphabetically, joined as <c>key=value</c> with <c>&amp;</c>;
        /// null/undefined become an empty string (same as the official SDK's <c>convertObjToQueryStr</c>).
        /// </summary>
        internal static bool TryBuildSignatureDataFromRaw(string rawDataJson, out string signatureData)
        {
            signatureData = string.Empty;
            try
            {
                using var doc = JsonDocument.Parse(rawDataJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
                var parts = doc.RootElement.EnumerateObject()
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .Select(p => $"{p.Name}={SignatureValue(p.Value)}");
                signatureData = string.Join("&", parts);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string SignatureValue(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => value.GetString() is "null" or "undefined" ? string.Empty : value.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText(),
        };

        private static string BuildTypedWebhookSignatureData(PayOsWebhookData d)
        {
            // Thứ tự field PHẢI đúng alphabet theo tên field JSON gốc của PayOS.
            var orderedFields = new (string Key, string Value)[]
            {
                ("accountNumber", d.AccountNumber ?? ""),
                ("amount", d.Amount.ToString(CultureInfo.InvariantCulture)),
                ("code", d.Code ?? ""),
                ("counterAccountBankId", d.CounterAccountBankId ?? ""),
                ("counterAccountBankName", d.CounterAccountBankName ?? ""),
                ("counterAccountName", d.CounterAccountName ?? ""),
                ("counterAccountNumber", d.CounterAccountNumber ?? ""),
                ("currency", d.Currency ?? ""),
                ("desc", d.Desc ?? ""),
                ("description", d.Description ?? ""),
                ("orderCode", d.OrderCode.ToString(CultureInfo.InvariantCulture)),
                ("paymentLinkId", d.PaymentLinkId ?? ""),
                ("reference", d.Reference ?? ""),
                ("transactionDateTime", d.TransactionDateTime ?? ""),
                ("virtualAccountName", d.VirtualAccountName ?? ""),
                ("virtualAccountNumber", d.VirtualAccountNumber ?? ""),
            };
            return string.Join("&", orderedFields.Select(f => $"{f.Key}={f.Value}"));
        }

        private static bool FixedTimeEqualsHex(string expectedLowerHex, string provided)
        {
            var a = Encoding.ASCII.GetBytes(expectedLowerHex);
            var b = Encoding.ASCII.GetBytes(provided.Trim().ToLowerInvariant());
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }

        internal static string BuildPaymentRequestSignatureData(long orderCode, int amount, string description, string returnUrl, string cancelUrl)
            => $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";

        // PayOS: Unix timestamp (giây, Int32).
        internal static int ToUnixSeconds(DateTime value)
        {
            var utc = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
            return checked((int)new DateTimeOffset(utc).ToUnixTimeSeconds());
        }

        private static string ComputeHmacSha256(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
