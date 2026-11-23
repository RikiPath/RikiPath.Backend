using System.Text.RegularExpressions;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;

namespace RikiPath.WebApi.Services;

/// <summary>Kho media cô lập chỉ dành cho Development/QA để không chạm Shared Supabase hoặc S3 dùng chung.</summary>
public sealed class LocalDevMediaStorageService : IFileStorageService
{
    private const long MaxFileBytes = 50 * 1024 * 1024;
    private static readonly Regex SafeFolderRegex = new(@"^[a-z0-9_-]{1,40}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SafeStoredFileRegex = new(
        @"^[a-f0-9]{32}\.(?:jpg|jpeg|png|webp|gif|pdf|mp3|wav|m4a|ogg|webm)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly string _root;

    public LocalDevMediaStorageService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("MentorQA"))
            throw new InvalidOperationException("LocalDevMediaStorageService chỉ được bật trong Development hoặc MentorQA.");

        var configured = configuration["LocalDevMediaStorage:RootPath"];
        _root = !string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured)
            ? Path.GetFullPath(configured)
            : Path.Combine(Path.GetTempPath(), "rikipath-dev-media");

        var contentRoot = Path.GetFullPath(environment.ContentRootPath);
        if (IsWithin(_root, contentRoot))
            throw new InvalidOperationException("Kho media Dev phải nằm ngoài thư mục source.");
    }

    public async Task<UploadedFileResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
            throw new ArgumentException("Tệp phải có phần mở rộng hợp lệ.", nameof(fileName));

        var normalizedFolder = NormalizeFolder(folder);
        var targetDir = Path.Combine(_root, "public", normalizedFolder);
        Directory.CreateDirectory(targetDir);

        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(targetDir, storedFileName);

        await using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length <= 0)
            throw new ArgumentException("Tệp tải lên rỗng.", nameof(fileStream));
        if (buffer.Length > MaxFileBytes)
            throw new ArgumentException("Tệp vượt quá giới hạn 50 MB.", nameof(fileStream));

        buffer.Position = 0;
        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await buffer.CopyToAsync(fs, cancellationToken);
            await fs.FlushAsync(cancellationToken);
        }

        var url = $"/api/FileStorage/local/{normalizedFolder}/{storedFileName}";
        return new UploadedFileResult(url, storedFileName, buffer.Length);
    }

    public Task DeleteAsync(string storedFileName, string folder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedFolder = NormalizeFolder(folder);
        var cleanFile = Path.GetFileName(storedFileName);
        if (!SafeStoredFileRegex.IsMatch(cleanFile))
            return Task.CompletedTask;

        var fullPath = Path.Combine(_root, "public", normalizedFolder, cleanFile);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public Task<string> CreateSignedReadUrlAsync(string storedUrl, CancellationToken cancellationToken)
        => Task.FromResult(storedUrl);

    public async Task<UploadedFileResult> UploadPrivateVerificationAsync(
        Stream fileStream, string contentType, int ownerId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ownerId);
        var extension = contentType switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => HomeworkAudioFormat.Extension(contentType)
                ?? throw new ArgumentException("Loại tệp không được hỗ trợ.", nameof(contentType))
        };

        var dir = Path.Combine(_root, "private-verification", ownerId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(dir, fileName);

        long total = 0;
        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await fileStream.CopyToAsync(fs, cancellationToken);
            total = fs.Length;
            await fs.FlushAsync(cancellationToken);
        }

        return new UploadedFileResult($"localdev://private-verification/{ownerId}/{fileName}", fileName, total);
    }

    public Task VerifyPrivateVerificationAsync(string storedUrl, CancellationToken cancellationToken)
    {
        var path = ResolvePrivatePath(storedUrl);
        if (!File.Exists(path))
            throw new FileNotFoundException("Không tìm thấy tài liệu.", storedUrl);
        return Task.CompletedTask;
    }

    public Task<PrivateVerificationReadResult> OpenPrivateVerificationAsync(string storedUrl, CancellationToken cancellationToken)
    {
        var path = ResolvePrivatePath(storedUrl);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var contentType = ResolveContentType(ext);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(new PrivateVerificationReadResult(stream, contentType));
    }

    public bool TryOpenPublicMedia(string? folder, string fileName, out Stream? stream, out string contentType)
    {
        stream = null;
        contentType = "application/octet-stream";
        if (string.IsNullOrWhiteSpace(fileName) || !SafeStoredFileRegex.IsMatch(fileName))
            return false;

        var normalizedFolder = string.IsNullOrWhiteSpace(folder) ? "general" : folder.Trim().ToLowerInvariant();
        if (!SafeFolderRegex.IsMatch(normalizedFolder))
            return false;

        var fullPath = Path.GetFullPath(Path.Combine(_root, "public", normalizedFolder, fileName));
        if (!IsWithin(fullPath, Path.Combine(_root, "public")) || !File.Exists(fullPath))
            return false;

        contentType = ResolveContentType(Path.GetExtension(fullPath).ToLowerInvariant());
        stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return true;
    }

    private string ResolvePrivatePath(string storedUrl)
    {
        if (!Uri.TryCreate(storedUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != "localdev" || uri.Host != "private-verification")
            throw new ArgumentException("Tham chiếu không hợp lệ.", nameof(storedUrl));
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out _) || !SafeStoredFileRegex.IsMatch(parts[1]))
            throw new ArgumentException("Đường dẫn không hợp lệ.", nameof(storedUrl));
        return Path.Combine(_root, "private-verification", parts[0], parts[1]);
    }

    private static string NormalizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return "general";
        var cleaned = folder.Trim().ToLowerInvariant().Replace('/', '-');
        return SafeFolderRegex.IsMatch(cleaned) ? cleaned : "general";
    }

    private static string ResolveContentType(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".pdf" => "application/pdf",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".m4a" => "audio/mp4",
        ".ogg" => "audio/ogg",
        ".webm" => "audio/webm",
        _ => "application/octet-stream"
    };

    private static bool IsWithin(string candidate, string directory) =>
        candidate.Equals(directory, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
