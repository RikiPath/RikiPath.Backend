using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;

namespace RikiPath.WebApi.Services;

/// <summary>Streams authorized proposal reference attachments without exposing raw storage URLs or public paths.</summary>
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
        controller.Response.Headers.CacheControl = "no-store, private";
        controller.Response.Headers.XContentTypeOptions = "nosniff";

        var displayName = Path.GetFileName(string.IsNullOrWhiteSpace(attachment.Name) ? "tai-lieu-dinh-kem" : attachment.Name.Trim());
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "tai-lieu-dinh-kem";

        var ext = Path.GetExtension(displayName);
        if (string.IsNullOrWhiteSpace(ext) && !string.IsNullOrWhiteSpace(attachment.StoredFileName))
            ext = Path.GetExtension(attachment.StoredFileName);

        var url = attachment.Url?.Trim() ?? string.Empty;
        if (url.StartsWith(LocalProposalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var fileName = url[LocalProposalPrefix.Length..].Trim();
            if (fileStorageService is not LocalDevMediaStorageService localDev ||
                !localDev.TryOpenPublicMedia("mentor-proposals", fileName, out var localStream, out var detectedMime) ||
                localStream is null)
            {
                return controller.NotFound(new { Message = "Tệp đính kèm không còn khả dụng." });
            }

            ApplyContentDisposition(controller.Response, displayName, ext, download);
            return controller.File(localStream, ResolveMime(attachment.ContentType, detectedMime, ext));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            return controller.NotFound(new { Message = "Không tìm thấy đường dẫn tệp hợp lệ." });
        }

        if (fileStorageService is LocalDevMediaStorageService)
        {
            return controller.NotFound(new { Message = "Tệp đính kèm không còn khả dụng." });
        }

        string fetchUrl;
        try
        {
            fetchUrl = await fileStorageService.CreateSignedReadUrlAsync(url, ct);
        }
        catch (ArgumentException)
        {
            return controller.NotFound(new { Message = "Không tìm thấy đường dẫn tệp đính kèm hợp lệ." });
        }

        using var client = httpClientFactory.CreateClient();
        using var upstream = await client.GetAsync(fetchUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!upstream.IsSuccessStatusCode)
            return controller.NotFound(new { Message = "Không thể tải nội dung tệp đính kèm." });

        if (upstream.Content.Headers.ContentLength is > MaxAttachmentStreamBytes)
            return controller.StatusCode(StatusCodes.Status413PayloadTooLarge, new { Message = "Tệp đính kèm vượt quá kích thước cho phép (25 MB)." });

        var buffer = new MemoryStream();
        await using (var upstreamStream = await upstream.Content.ReadAsStreamAsync(ct))
        {
            var chunk = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await upstreamStream.ReadAsync(chunk.AsMemory(0, chunk.Length), ct)) > 0)
            {
                totalRead += read;
                if (totalRead > MaxAttachmentStreamBytes)
                {
                    await buffer.DisposeAsync();
                    return controller.StatusCode(StatusCodes.Status413PayloadTooLarge, new { Message = "Tệp đính kèm vượt quá kích thước cho phép (25 MB)." });
                }
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }
        }
        buffer.Position = 0;

        var upstreamMime = upstream.Content.Headers.ContentType?.MediaType;
        ApplyContentDisposition(controller.Response, displayName, ext, download);
        return controller.File(buffer, ResolveMime(attachment.ContentType, upstreamMime, ext));
    }

    private static void ApplyContentDisposition(HttpResponse response, string fileName, string ext, bool download)
    {
        var inline = !download && !string.IsNullOrWhiteSpace(ext) && InlineViewableExtensions.Contains(ext);
        var disposition = new ContentDisposition
        {
            Inline = inline,
            FileName = fileName,
        };
        response.Headers.ContentDisposition = disposition.ToString();
    }

    private static string ResolveMime(string? recorded, string? fallback, string ext)
    {
        if (!string.IsNullOrWhiteSpace(fallback) && !string.Equals(fallback, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return fallback;
        if (!string.IsNullOrWhiteSpace(recorded) && !string.Equals(recorded, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return recorded;
        return ext.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv; charset=utf-8",
            ".txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream",
        };
    }
}
