using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Services;

/// <summary>
/// Đọc, kiểm tra nội dung thực tế, tạo bản xem trước chỉ đọc và stream tài liệu đính kèm có phân quyền,
/// tuyệt đối bảo toàn tệp gốc và tên tệp Unicode (tiếng Việt, tiếng Nhật...).
/// </summary>
public static class ProposalAttachmentStreamHelper
{
    private const string LocalProposalPrefix = "/api/FileStorage/local/mentor-proposals/";
    private const long MaxAttachmentStreamBytes = 25L * 1024 * 1024;

    private static readonly HashSet<string> InlineViewableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".txt"
    };

    public static async Task<IActionResult> StreamAttachmentAsync(
        ControllerBase controller,
        IFileStorageService fileStorageService,
        IHttpClientFactory httpClientFactory,
        ProposalAttachmentDto attachment,
        bool download,
        CancellationToken ct)
    {
        var loaded = await LoadAttachmentAsync(controller, fileStorageService, httpClientFactory, attachment, ct);
        if (loaded.ErrorResult is not null)
            return loaded.ErrorResult;

        var buffer = loaded.Buffer!;
        var rawDisplayName = Path.GetFileName(string.IsNullOrWhiteSpace(attachment.Name) ? "tai-lieu-dinh-kem" : attachment.Name.Trim());
        if (string.IsNullOrWhiteSpace(rawDisplayName))
            rawDisplayName = "tai-lieu-dinh-kem";

        var declaredExt = Path.GetExtension(rawDisplayName);
        if (string.IsNullOrWhiteSpace(declaredExt) && !string.IsNullOrWhiteSpace(attachment.StoredFileName))
            declaredExt = Path.GetExtension(attachment.StoredFileName);

        var actualExt = AttachmentContentInspector.DetectActualExtension(buffer, declaredExt);
        if (string.IsNullOrWhiteSpace(actualExt))
            actualExt = declaredExt;

        var alignedName = AttachmentContentInspector.EnsureFilenameMatchesExtension(rawDisplayName, actualExt);
        var effectiveMime = ResolveMime(attachment.ContentType, loaded.UpstreamMime, actualExt);

        controller.Response.Headers.CacheControl = "no-store, private";
        controller.Response.Headers.XContentTypeOptions = "nosniff";
        ApplyContentDisposition(controller.Response, alignedName, actualExt, download);

        buffer.Position = 0;
        return controller.File(buffer, effectiveMime);
    }

    public static async Task<IActionResult> PreviewAttachmentAsync(
        ControllerBase controller,
        IFileStorageService fileStorageService,
        IHttpClientFactory httpClientFactory,
        ProposalAttachmentDto attachment,
        CancellationToken ct)
    {
        var loaded = await LoadAttachmentAsync(controller, fileStorageService, httpClientFactory, attachment, ct);
        if (loaded.ErrorResult is not null)
            return loaded.ErrorResult;

        await using var buffer = loaded.Buffer!;
        var rawDisplayName = Path.GetFileName(string.IsNullOrWhiteSpace(attachment.Name) ? "tai-lieu-dinh-kem" : attachment.Name.Trim());
        if (string.IsNullOrWhiteSpace(rawDisplayName))
            rawDisplayName = "tai-lieu-dinh-kem";

        var declaredExt = Path.GetExtension(rawDisplayName);
        if (string.IsNullOrWhiteSpace(declaredExt) && !string.IsNullOrWhiteSpace(attachment.StoredFileName))
            declaredExt = Path.GetExtension(attachment.StoredFileName);

        var actualExt = AttachmentContentInspector.DetectActualExtension(buffer, declaredExt);
        if (string.IsNullOrWhiteSpace(actualExt))
            actualExt = declaredExt;

        var effectiveMime = ResolveMime(attachment.ContentType, loaded.UpstreamMime, actualExt);
        var sizeBytes = buffer.Length > 0 ? buffer.Length : (attachment.SizeBytes ?? 0);
        var payload = AttachmentContentInspector.BuildPreviewPayload(buffer, rawDisplayName, actualExt, effectiveMime, sizeBytes);

        controller.Response.Headers.CacheControl = "no-store, private";
        controller.Response.Headers.XContentTypeOptions = "nosniff";
        return controller.Ok(ApiResponse<AttachmentPreviewPayload>.Success(payload));
    }

    public static IActionResult StreamPrivateVerificationResult(
        ControllerBase controller,
        PrivateVerificationReadResult? result,
        string baseFileName,
        bool download)
    {
        if (result is null)
            return controller.NotFound(ApiResponse<object>.Fail("Không tìm thấy tài liệu đính kèm.", HttpStatusCode.NotFound));

        controller.Response.Headers.CacheControl = "no-store, private";
        controller.Response.Headers.XContentTypeOptions = "nosniff";

        var mime = string.IsNullOrWhiteSpace(result.ContentType) ? "application/octet-stream" : result.ContentType;
        var ext = ExtensionFromMime(mime);
        var cleanBase = string.IsNullOrWhiteSpace(baseFileName) ? "tai-lieu" : baseFileName.Trim();
        var fileName = string.IsNullOrWhiteSpace(Path.GetExtension(cleanBase)) && !string.IsNullOrWhiteSpace(ext)
            ? $"{cleanBase}{ext}"
            : cleanBase;

        ApplyContentDisposition(controller.Response, fileName, ext, download);
        return controller.File(result.Content, mime);
    }

    private static async Task<(MemoryStream? Buffer, string? UpstreamMime, IActionResult? ErrorResult)> LoadAttachmentAsync(
        ControllerBase controller,
        IFileStorageService fileStorageService,
        IHttpClientFactory httpClientFactory,
        ProposalAttachmentDto attachment,
        CancellationToken ct)
    {
        var url = attachment.Url?.Trim() ?? string.Empty;
        if (url.StartsWith(LocalProposalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var fileName = url[LocalProposalPrefix.Length..].Trim();
            if (fileStorageService is not LocalDevMediaStorageService localDev ||
                !localDev.TryOpenPublicMedia("mentor-proposals", fileName, out var localStream, out var detectedMime) ||
                localStream is null)
            {
                return (null, null, controller.NotFound(ApiResponse<object>.Fail(
                    "Tài liệu gốc không còn tồn tại trên hệ thống lưu trữ.",
                    HttpStatusCode.NotFound,
                    "ATTACHMENT_MISSING")));
            }

            await using (localStream)
            {
                var buffer = new MemoryStream();
                var copyError = await CopyBoundedAsync(controller, localStream, buffer, ct);
                if (copyError is not null)
                {
                    await buffer.DisposeAsync();
                    return (null, null, copyError);
                }
                buffer.Position = 0;
                return (buffer, detectedMime, null);
            }
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            return (null, null, controller.NotFound(ApiResponse<object>.Fail(
                "Không tìm thấy tệp đính kèm hợp lệ.",
                HttpStatusCode.NotFound,
                "ATTACHMENT_MISSING")));
        }

        if (fileStorageService is LocalDevMediaStorageService)
        {
            return (null, null, controller.NotFound(ApiResponse<object>.Fail(
                "Tài liệu gốc không còn tồn tại trên kho lưu trữ hiện tại.",
                HttpStatusCode.NotFound,
                "ATTACHMENT_MISSING")));
        }

        string fetchUrl;
        try
        {
            fetchUrl = await fileStorageService.CreateSignedReadUrlAsync(url, ct);
        }
        catch (ArgumentException)
        {
            return (null, null, controller.NotFound(ApiResponse<object>.Fail(
                "Không tìm thấy đường dẫn tệp đính kèm hợp lệ.",
                HttpStatusCode.NotFound,
                "ATTACHMENT_MISSING")));
        }
        catch (InvalidOperationException)
        {
            return (null, null, controller.StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail(
                    "Không thể truy cập kho lưu trữ tài liệu lúc này. Vui lòng thử lại sau.",
                    HttpStatusCode.ServiceUnavailable,
                    "ATTACHMENT_STORAGE_UNAVAILABLE")));
        }

        using var client = httpClientFactory.CreateClient();
        using var upstream = await client.GetAsync(fetchUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!upstream.IsSuccessStatusCode)
        {
            return (null, null, controller.NotFound(ApiResponse<object>.Fail(
                "Không thể tải nội dung tệp đính kèm từ kho lưu trữ.",
                HttpStatusCode.NotFound,
                "ATTACHMENT_MISSING")));
        }

        if (upstream.Content.Headers.ContentLength is > MaxAttachmentStreamBytes)
        {
            return (null, null, controller.StatusCode(StatusCodes.Status413PayloadTooLarge,
                ApiResponse<object>.Fail("Tệp đính kèm vượt quá kích thước cho phép (25 MB).", HttpStatusCode.RequestEntityTooLarge)));
        }

        var remoteBuffer = new MemoryStream();
        await using (var upstreamStream = await upstream.Content.ReadAsStreamAsync(ct))
        {
            var copyError = await CopyBoundedAsync(controller, upstreamStream, remoteBuffer, ct);
            if (copyError is not null)
            {
                await remoteBuffer.DisposeAsync();
                return (null, null, copyError);
            }
        }

        remoteBuffer.Position = 0;
        var upstreamMime = upstream.Content.Headers.ContentType?.MediaType;
        return (remoteBuffer, upstreamMime, null);
    }

    private static async Task<IActionResult?> CopyBoundedAsync(
        ControllerBase controller,
        Stream source,
        MemoryStream destination,
        CancellationToken ct)
    {
        var chunk = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await source.ReadAsync(chunk.AsMemory(0, chunk.Length), ct)) > 0)
        {
            totalRead += read;
            if (totalRead > MaxAttachmentStreamBytes)
            {
                return controller.StatusCode(StatusCodes.Status413PayloadTooLarge,
                    ApiResponse<object>.Fail("Tệp đính kèm vượt quá kích thước cho phép (25 MB).", HttpStatusCode.RequestEntityTooLarge));
            }
            await destination.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        return null;
    }

    private static void ApplyContentDisposition(HttpResponse response, string fileName, string ext, bool download)
    {
        var inline = !download && !string.IsNullOrWhiteSpace(ext) && InlineViewableExtensions.Contains(ext);
        var cleanName = new string(fileName.Where(c => !char.IsControl(c) && c != '"' && c != '/' && c != '\\').ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(cleanName))
            cleanName = string.IsNullOrWhiteSpace(ext) ? "tai-lieu-dinh-kem" : $"tai-lieu-dinh-kem{ext}";

        var header = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        // SetHttpFileName tạo đồng thời fallback ASCII và filename* UTF-8 theo RFC 5987 cho tên tiếng Việt / tiếng Nhật.
        header.SetHttpFileName(cleanName);
        response.Headers.ContentDisposition = header.ToString();
    }

    private static string ResolveMime(string? recorded, string? upstreamMime, string actualExt)
    {
        var fromExt = AttachmentContentInspector.ResolveCanonicalContentType(actualExt);
        if (!string.Equals(fromExt, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return fromExt;
        if (!string.IsNullOrWhiteSpace(upstreamMime) && !string.Equals(upstreamMime, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return upstreamMime;
        if (!string.IsNullOrWhiteSpace(recorded) && !string.Equals(recorded, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return recorded;
        return "application/octet-stream";
    }

    private static string ExtensionFromMime(string mime)
    {
        var baseMime = mime.Split(';')[0].Trim().ToLowerInvariant();
        return baseMime switch
        {
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "text/plain" => ".txt",
            "text/csv" => ".csv",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.ms-excel" => ".xls",
            _ => string.Empty,
        };
    }
}
