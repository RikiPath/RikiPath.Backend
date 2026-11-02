using RikiPath.Application.IServices;
using RikiPath.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RikiPath.Application.Services
{
    public class SupabaseFileStorageService(HttpClient httpClient, AppSettings appSettings) : IFileStorageService
    {
        private readonly string _supabaseUrl = ConfigureHttpClient(httpClient, appSettings);
        private readonly string _bucket = appSettings.Supabase?.Bucket
            ?? throw new InvalidOperationException("Supabase:Bucket chưa được cấu hình trong appsettings.");

        private string VerificationBucket => !string.IsNullOrWhiteSpace(appSettings.Supabase?.VerificationBucket)
            ? appSettings.Supabase.VerificationBucket
            : throw new InvalidOperationException("Supabase:VerificationBucket chưa được cấu hình.");

        private async Task<string> RequirePrivateVerificationBucketAsync(CancellationToken cancellationToken)
        {
            var bucket = VerificationBucket;
            using var response = await httpClient.GetAsync(
                $"/storage/v1/bucket/{Uri.EscapeDataString(bucket)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Không thể xác minh bucket tài liệu riêng tư.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!document.RootElement.TryGetProperty("id", out var id) || id.GetString() != bucket ||
                !document.RootElement.TryGetProperty("public", out var isPublic) ||
                isPublic.ValueKind != JsonValueKind.False)
                throw new InvalidOperationException("Bucket tài liệu xác minh phải ở chế độ private.");
            return bucket;
        }

        public async Task<UploadedFileResult> UploadPrivateVerificationAsync(
            Stream fileStream, string contentType, int ownerId, CancellationToken cancellationToken)
        {
            if (ownerId <= 0) throw new ArgumentOutOfRangeException(nameof(ownerId));
            var extension = contentType switch
            {
                "application/pdf" => ".pdf",
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                _ => throw new ArgumentException("Loại tài liệu không được hỗ trợ.", nameof(contentType))
            };
            var bucket = await RequirePrivateVerificationBucketAsync(cancellationToken);
            var objectPath = $"mentor/{ownerId}/{Guid.NewGuid():N}{extension}";
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            using var response = await httpClient.PostAsync(
                $"/storage/v1/object/{Uri.EscapeDataString(bucket)}/{objectPath}", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Không thể tải tài liệu xác minh lên kho riêng tư.");
            var reference = $"{_supabaseUrl}/storage/v1/object/authenticated/{Uri.EscapeDataString(bucket)}/{objectPath}";
            return new UploadedFileResult(reference, objectPath, fileStream.CanSeek ? fileStream.Length : 0);
        }

        private static string ConfigureHttpClient(HttpClient httpClient, AppSettings appSettings)
        {
            var supabase = appSettings.Supabase
                ?? throw new InvalidOperationException("Supabase chưa được cấu hình trong appsettings.");

            var url = supabase.Url?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Supabase:Url chưa được cấu hình trong appsettings.");

            var storageKey = supabase.ServiceRoleKey?.Trim().Trim('<', '>');
            if (string.IsNullOrWhiteSpace(storageKey))
                throw new InvalidOperationException("Supabase:ServiceRoleKey chưa được cấu hình trong appsettings.");

            httpClient.BaseAddress ??= new Uri(url);
            httpClient.DefaultRequestHeaders.Remove("apikey");
            httpClient.DefaultRequestHeaders.Add("apikey", storageKey);
            httpClient.DefaultRequestHeaders.Remove("Authorization");

            // Legacy service_role keys are JWTs and must be sent as Bearer.
            // New Supabase secret keys (sb_secret_...) are accepted through apikey,
            // but are not JWTs and must not be sent as Bearer.
            if (storageKey.StartsWith("eyJ", StringComparison.Ordinal))
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", storageKey);

            return url;
        }

        public async Task<UploadedFileResult> UploadAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            string folder,
            CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(fileName);
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var objectPath = string.IsNullOrWhiteSpace(folder)
                ? storedFileName
                : $"{folder.Trim('/')}/{storedFileName}";

            long sizeBytes = fileStream.CanSeek ? fileStream.Length : 0;

            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            // POST /storage/v1/object/{bucket}/{path} -> tạo object mới trên Supabase Storage
            var requestUrl = $"/storage/v1/object/{_bucket}/{objectPath}";
            using var response = await httpClient.PostAsync(requestUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Upload lên Supabase thất bại ({(int)response.StatusCode}): {error}");
            }

            if (sizeBytes == 0)
            {
                sizeBytes = content.Headers.ContentLength ?? 0;
            }

            // URL public (chỉ dùng được nếu bucket được set Public trên Supabase Dashboard)
            var publicUrl = $"{_supabaseUrl}/storage/v1/object/public/{_bucket}/{objectPath}";

            return new UploadedFileResult(publicUrl, objectPath, sizeBytes);
        }

        public async Task DeleteAsync(string storedFileName, string folder, CancellationToken cancellationToken)
        {
            // storedFileName chính là objectPath đã trả về lúc Upload (UploadedFileResult.StoredFileName),
            // thường đã bao gồm folder bên trong.
            var objectPath = storedFileName.Contains('/')
                ? storedFileName
                : (string.IsNullOrWhiteSpace(folder) ? storedFileName : $"{folder.Trim('/')}/{storedFileName}");

            var payload = new { prefixes = new[] { objectPath } };
            var json = JsonSerializer.Serialize(payload);

            // DELETE /storage/v1/object/{bucket} với body { "prefixes": [...] } -> API xóa hàng loạt của Supabase
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/storage/v1/object/{_bucket}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Xóa file trên Supabase thất bại ({(int)response.StatusCode}): {error}");
            }
        }

        // Chỉ ký URL trỏ tới object của chính bucket đã cấu hình; không gửi yêu cầu tới host do client cung cấp.
        public async Task<string> CreateSignedReadUrlAsync(string storedUrl, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(storedUrl, UriKind.Absolute, out var uri) ||
                !Uri.TryCreate(_supabaseUrl, UriKind.Absolute, out var configuredUri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.GetLeftPart(UriPartial.Authority) != configuredUri.GetLeftPart(UriPartial.Authority) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Tài liệu xác minh không thuộc kho lưu trữ được cấu hình.", nameof(storedUrl));

            var bucket = await RequirePrivateVerificationBucketAsync(cancellationToken);
            var prefix = $"/storage/v1/object/authenticated/{Uri.EscapeDataString(bucket)}/";
            if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
                throw new ArgumentException("Tài liệu xác minh không thuộc bucket được cấu hình.", nameof(storedUrl));
            var objectPath = Uri.UnescapeDataString(uri.AbsolutePath[prefix.Length..]);
            if (string.IsNullOrWhiteSpace(objectPath) || objectPath.Split('/').Any(segment => segment is "." or ".." or ""))
                throw new ArgumentException("Đường dẫn tài liệu xác minh không hợp lệ.", nameof(storedUrl));

            var escapedPath = string.Join('/', objectPath.Split('/').Select(Uri.EscapeDataString));
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/storage/v1/object/sign/{Uri.EscapeDataString(bucket)}/{escapedPath}")
            {
                Content = new StringContent("{\"expiresIn\":60}", Encoding.UTF8, "application/json")
            };
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Không thể tạo liên kết bảo vệ cho tài liệu xác minh.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var signedPath = document.RootElement.GetProperty("signedURL").GetString();
            if (string.IsNullOrWhiteSpace(signedPath) || !signedPath.StartsWith("/object/sign/", StringComparison.Ordinal))
                throw new InvalidOperationException("Kho lưu trữ trả về liên kết tài liệu không hợp lệ.");
            return $"{_supabaseUrl}/storage/v1{signedPath}";
        }

        public async Task VerifyPrivateVerificationAsync(string storedUrl, CancellationToken cancellationToken)
        {
            _ = await CreateSignedReadUrlAsync(storedUrl, cancellationToken);
        }

        public async Task<PrivateVerificationReadResult> OpenPrivateVerificationAsync(
            string storedUrl, CancellationToken cancellationToken)
        {
            await VerifyPrivateVerificationAsync(storedUrl, cancellationToken);
            var uri = new Uri(storedUrl);
            using var response = await httpClient.GetAsync(uri.AbsolutePath, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new FileNotFoundException("Không tìm thấy chứng chỉ trong kho riêng tư.");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Không thể đọc chứng chỉ từ kho riêng tư.");
            const int maxBytes = 5 * 1024 * 1024;
            if (response.Content.Headers.ContentLength > maxBytes)
                throw new InvalidOperationException("Chứng chỉ vượt quá giới hạn dung lượng.");
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is not ("application/pdf" or "image/jpeg" or "image/png"))
                throw new InvalidOperationException("Loại chứng chỉ không hợp lệ.");
            var result = new MemoryStream();
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[81920];
            int count;
            while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (result.Length + count > maxBytes)
                {
                    await result.DisposeAsync();
                    throw new InvalidOperationException("Chứng chỉ vượt quá giới hạn dung lượng.");
                }
                await result.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            result.Position = 0;
            return new PrivateVerificationReadResult(result, contentType);
        }
    }
}
