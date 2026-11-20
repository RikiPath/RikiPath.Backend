using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Infrastructure.Clients;

namespace RikiPath.Infrastructure.Services;

public sealed class AwsAudioFileStorageService(AppSettings appSettings) : IAwsAudioFileStorageService
{
    private AwsAudioSettings Settings => appSettings.AwsAudio;
    private static readonly Regex FilePattern = new(@"^[a-f0-9]{32}\.(mp3|wav|m4a|mp4|webm|ogg|flac|amr)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public bool IsConfigured => AwsAudioCredentials.IsConfigured(Settings);

    public bool IsAudio(string fileName, string? contentType) =>
        contentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true ||
        Path.GetExtension(fileName).ToLowerInvariant() is ".mp3" or ".wav" or ".m4a" or ".webm" or ".ogg" or ".flac" or ".amr" or ".mpeg" or ".mpga";

    public async Task<UploadedFileResult> UploadAsync(
        Stream audio, string fileName, int? ownerId, bool content, CancellationToken ct)
    {
        if (ownerId is <= 0 || (content && ownerId is null))
            throw new ArgumentException("Người tải audio không hợp lệ.");
        if (!IsConfigured)
            throw new InvalidOperationException("Chưa cấu hình AwsAudio để lưu file âm thanh.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is ".mpeg" or ".mpga") extension = ".mp3";
        if (!FilePattern.IsMatch(new string('0', 32) + extension))
            throw new ArgumentException("Định dạng audio không được hỗ trợ.");

        await using var buffered = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await audio.ReadAsync(buffer, ct)) > 0)
        {
            if (buffered.Length + count > ListeningAudioFilePolicy.MaxBytes)
                throw new ArgumentException("File audio tối đa 25 MB.");
            await buffered.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        if (buffered.Length < 4) throw new ArgumentException("File audio trống hoặc không hợp lệ.");
        var detected = DetectType(buffered.GetBuffer(), (int)Math.Min(buffered.Length, 12), extension);
        buffered.Position = 0;
        var storedFile = Guid.NewGuid().ToString("N") + extension;
        var prefix = content ? Settings.ContentPrefix : ownerId.HasValue ? Settings.RecordingPrefix : Settings.TemporaryPrefix;
        var key = ownerId.HasValue ? $"{prefix.Trim('/')}/{ownerId}/{storedFile}" : $"{prefix.Trim('/')}/{storedFile}";
        var reference = content ? ContentReference(ownerId!.Value, storedFile) : S3Reference(key);
        using var s3 = AwsAudioCredentials.S3(Settings);
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Settings.Bucket, Key = key, InputStream = buffered, AutoCloseStream = false,
            ContentType = detected, ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        }, ct);
        return new(reference, key, buffered.Length);
    }

    private static string DetectType(byte[] bytes, int count, string extension)
    {
        var signature = bytes.AsSpan(0, count);
        var detected = HomeworkAudioFormat.Detect(signature);
        if (extension == ".mp3" && (signature.StartsWith("ID3"u8) ||
            (signature[0] == 0xff && (signature[1] & 0xe0) == 0xe0))) detected = "audio/mpeg";
        if (extension == ".flac" && signature.StartsWith("fLaC"u8)) detected = "audio/flac";
        if (extension == ".ogg" && signature.StartsWith("OggS"u8)) detected = "audio/ogg";
        if (extension == ".amr" && signature.StartsWith("#!AMR"u8)) detected = "audio/amr";
        if (detected is null) throw new ArgumentException("Nội dung file audio không có định dạng hợp lệ.");
        return detected;
    }

    public string ContentReference(int ownerId, string fileName)
    {
        if (ownerId <= 0 || !FilePattern.IsMatch(fileName))
            throw new ArgumentException("Đường dẫn audio không hợp lệ.");
        if (!Uri.TryCreate(Settings.PublicBaseUrl, UriKind.Absolute, out var origin) ||
            origin.Scheme is not ("https" or "http") || origin.UserInfo.Length != 0 ||
            origin.Query.Length != 0 || origin.Fragment.Length != 0)
            throw new InvalidOperationException("AwsAudio:PublicBaseUrl phải là địa chỉ backend công khai.");
        return $"{Settings.PublicBaseUrl.TrimEnd('/')}/api/audio/content/{ownerId}/{fileName}";
    }

    public string S3Reference(string key) => $"s3://{Settings.Bucket}/{EscapeKey(key)}";

    private static string EscapeKey(string key) => string.Join('/', key.Split('/').Select(Uri.EscapeDataString));

    private bool IsPrivateKey(string key) =>
        key.StartsWith(Settings.RecordingPrefix.Trim('/') + "/", StringComparison.Ordinal) ||
        key.StartsWith(Settings.TemporaryPrefix.Trim('/') + "/", StringComparison.Ordinal);

    public bool TryGetContentReference(string reference, out string canonicalUrl)
    {
        canonicalUrl = string.Empty;
        // A filename in Excel is resolved against the selected directory, never guessed as an S3 object.
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri) || uri.Scheme is not ("s3" or "https" or "http") ||
            !TryGetObjectKey(reference, out var key) || IsPrivateKey(key) ||
            !key.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return false;
        var prefix = Settings.ContentPrefix.Trim('/') + "/";
        if (key.StartsWith(prefix, StringComparison.Ordinal))
        {
            var parts = key[prefix.Length..].Split('/');
            if (parts.Length == 2 && int.TryParse(parts[0], out var owner) && owner > 0 &&
                parts[0] == owner.ToString(System.Globalization.CultureInfo.InvariantCulture) && FilePattern.IsMatch(parts[1]))
            {
                canonicalUrl = ContentReference(owner, parts[1]);
                return true;
            }
        }
        // Library URLs remain playable with a private bucket; the playback endpoint signs each request.
        var origin = new Uri(ContentReference(1, new string('0', 32) + ".mp3"));
        var root = origin.AbsoluteUri[..origin.AbsoluteUri.IndexOf("/api/audio/content/", StringComparison.Ordinal)];
        canonicalUrl = root + "/api/audio/library/" + EscapeKey(key);
        return true;
    }

    public bool IsRecordingOwnedBy(string reference, int ownerId) =>
        ownerId > 0 && TryGetObjectKey(reference, out var key) &&
        key.StartsWith(Settings.RecordingPrefix.Trim('/') + "/" + ownerId + "/", StringComparison.Ordinal);

    public bool TryGetObjectKey(string reference, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(reference)) return false;
        if (reference.StartsWith("s3://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri) || uri.Host != Settings.Bucket ||
                uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0) return false;
            key = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        }
        else if (Uri.TryCreate(reference, UriKind.Absolute, out var uri))
        {
            if (uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0) return false;
            var s3Hosts = new[] { $"{Settings.Bucket}.s3.{Settings.Region}.amazonaws.com",
                $"{Settings.Bucket}.s3.amazonaws.com", $"{Settings.Bucket}.s3-{Settings.Region}.amazonaws.com" };
            if (uri.Scheme == "https" && uri.IsDefaultPort && s3Hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
                key = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            else if (uri.Scheme == "https" && uri.IsDefaultPort &&
                (uri.Host == $"s3.{Settings.Region}.amazonaws.com" || uri.Host == "s3.amazonaws.com") &&
                uri.AbsolutePath.StartsWith("/" + Settings.Bucket + "/", StringComparison.Ordinal))
                key = Uri.UnescapeDataString(uri.AbsolutePath[(Settings.Bucket.Length + 2)..]);
            else
            {
                if (!Uri.TryCreate(Settings.PublicBaseUrl, UriKind.Absolute, out var origin) ||
                    uri.GetLeftPart(UriPartial.Authority) != origin.GetLeftPart(UriPartial.Authority)) return false;
                var prefix = origin.AbsolutePath.TrimEnd('/') + "/api/audio/";
                if (uri.AbsolutePath.StartsWith(prefix + "content/", StringComparison.Ordinal))
                    key = Settings.ContentPrefix.Trim('/') + "/" + Uri.UnescapeDataString(uri.AbsolutePath[(prefix.Length + 8)..]);
                else if (uri.AbsolutePath.StartsWith(prefix + "library/", StringComparison.Ordinal))
                {
                    key = Uri.UnescapeDataString(uri.AbsolutePath[(prefix.Length + 8)..]);
                    if (IsPrivateKey(key)) { key = string.Empty; return false; }
                }
                else return false;
            }
        }
        else
        {
            key = reference;
            if (!key.StartsWith(Settings.ContentPrefix.Trim('/') + "/", StringComparison.Ordinal) && !IsPrivateKey(key))
            { key = string.Empty; return false; }
        }

        var valid = key.Length > 0 && System.Text.Encoding.UTF8.GetByteCount(key) <= 1024 &&
            !key.Contains('\\') && !key.Any(char.IsControl) &&
            key.Split('/').All(part => part.Length > 0 && part is not ("." or ".."));
        if (valid && IsPrivateKey(key))
        {
            var prefix = key.StartsWith(Settings.RecordingPrefix.Trim('/') + "/", StringComparison.Ordinal)
                ? Settings.RecordingPrefix.Trim('/') + "/" : Settings.TemporaryPrefix.Trim('/') + "/";
            var parts = key[prefix.Length..].Split('/');
            valid = parts.Length == 1 && prefix == Settings.TemporaryPrefix.Trim('/') + "/"
                ? FilePattern.IsMatch(parts[0])
                : parts.Length == 2 && int.TryParse(parts[0], out var ownerId) && ownerId > 0 && FilePattern.IsMatch(parts[1]);
        }
        if (!valid) key = string.Empty;
        return valid;
    }

    public async Task<PrivateVerificationReadResult> OpenAsync(string reference, CancellationToken ct)
    {
        if (!TryGetObjectKey(reference, out var key)) throw new ArgumentException("Audio không thuộc kho AWS đã cấu hình.");
        using var s3 = AwsAudioCredentials.S3(Settings);
        using var response = await s3.GetObjectAsync(new GetObjectRequest { BucketName = Settings.Bucket, Key = key }, ct);
        if (response.ContentLength > ListeningAudioFilePolicy.MaxBytes)
            throw new ArgumentException("File audio tối đa 25 MB.");
        var buffer = new MemoryStream();
        try
        {
            var chunk = new byte[81920];
            int count;
            while ((count = await response.ResponseStream.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > ListeningAudioFilePolicy.MaxBytes)
                    throw new ArgumentException("File audio tối đa 25 MB.");
                await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
            }
            buffer.Position = 0;
            return new(buffer, response.Headers.ContentType ?? "application/octet-stream");
        }
        catch { await buffer.DisposeAsync(); throw; }
    }

    public async Task<string> CreateSignedReadUrlAsync(string reference, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!TryGetObjectKey(reference, out var key)) throw new ArgumentException("Audio không thuộc kho AWS đã cấu hình.");
        using var s3 = AwsAudioCredentials.S3(Settings);
        return await s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = Settings.Bucket, Key = key, Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddHours(1), Protocol = Protocol.HTTPS
        });
    }

    public async Task DeleteAsync(string reference, CancellationToken ct)
    {
        if (!TryGetObjectKey(reference, out var key)) throw new ArgumentException("Audio không thuộc kho AWS đã cấu hình.");
        using var s3 = AwsAudioCredentials.S3(Settings);
        await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = Settings.Bucket, Key = key }, ct);
    }
}
