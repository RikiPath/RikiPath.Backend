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

        private static string ConfigureHttpClient(HttpClient httpClient, AppSettings appSettings)
        {
            var supabase = appSettings.Supabase
                ?? throw new InvalidOperationException("Supabase chưa được cấu hình trong appsettings.");

            var url = supabase.Url?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Supabase:Url chưa được cấu hình trong appsettings.");

            if (string.IsNullOrWhiteSpace(supabase.ServiceRoleKey))
                throw new InvalidOperationException("Supabase:ServiceRoleKey chưa được cấu hình trong appsettings.");

            httpClient.BaseAddress ??= new Uri(url);
            httpClient.DefaultRequestHeaders.Remove("apikey");
            httpClient.DefaultRequestHeaders.Add("apikey", supabase.ServiceRoleKey);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supabase.ServiceRoleKey);

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
    }
}
