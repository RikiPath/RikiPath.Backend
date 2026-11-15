using System.Text.RegularExpressions;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Services;

/// <summary>Kho chứng chỉ riêng tư chỉ dành cho môi trường MentorQA cô lập.</summary>
public sealed class LocalQaFileStorageService : IFileStorageService
{
    private const int MaxDocumentBytes = 5 * 1024 * 1024;
    private static readonly Regex ReferencePattern = new(
        @"^/(?<owner>[1-9][0-9]*)/(?<file>[a-f0-9]{32}\.(?:pdf|jpg|png))$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly string _root;

    public LocalQaFileStorageService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (!environment.IsEnvironment("MentorQA") ||
            !configuration.GetValue<bool>("LocalQaVerificationStorage:Enabled"))
            throw new InvalidOperationException("Kho chứng chỉ Local QA chỉ được dùng trong MentorQA.");

        var configured = configuration["LocalQaVerificationStorage:RootPath"];
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured))
            throw new InvalidOperationException("Thiếu đường dẫn tuyệt đối cho kho chứng chỉ Local QA.");
        _root = Path.GetFullPath(configured);
        var contentRoot = Path.GetFullPath(environment.ContentRootPath);
        var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(contentRoot, "wwwroot"));
        if (IsWithin(_root, contentRoot) || IsWithin(_root, webRoot) ||
            _root.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals("wwwroot", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Kho chứng chỉ QA phải nằm ngoài source và thư mục public.");
    }

    public async Task<UploadedFileResult> UploadPrivateVerificationAsync(
        Stream fileStream, string contentType, int ownerId, CancellationToken cancellationToken)
    {
        if (ownerId <= 0) throw new ArgumentOutOfRangeException(nameof(ownerId));
        var extension = contentType switch
        {
            "application/pdf" => "pdf",
            "image/jpeg" => "jpg",
            "image/png" => "png",
            _ => throw new ArgumentException("Loại chứng chỉ không được hỗ trợ.", nameof(contentType))
        };
        await using var document = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await fileStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (document.Length + count > MaxDocumentBytes)
                throw new ArgumentException("Chứng chỉ vượt quá 5 MB.", nameof(fileStream));
            await document.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        if (document.Length < 4 || !MatchesSignature(document.GetBuffer(), document.Length, contentType))
            throw new ArgumentException("Nội dung chứng chỉ không khớp loại PDF, JPEG hoặc PNG.", nameof(fileStream));

        var ownerDirectory = Path.Combine(_root, ownerId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(ownerDirectory);
        var fileName = $"{Guid.NewGuid():N}.{extension}";
        var fullPath = Path.Combine(ownerDirectory, fileName);
        document.Position = 0;
        await using (var destination = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await document.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }
        return new UploadedFileResult($"localqa://mentor-verification/{ownerId}/{fileName}", fileName, document.Length);
    }

    public Task VerifyPrivateVerificationAsync(string storedUrl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storedUrl);
        if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy chứng chỉ QA.");
        return Task.CompletedTask;
    }

    public Task<PrivateVerificationReadResult> OpenPrivateVerificationAsync(
        string storedUrl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storedUrl);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf", ".jpg" => "image/jpeg", ".png" => "image/png",
            _ => throw new ArgumentException("Loại chứng chỉ không hợp lệ.", nameof(storedUrl))
        };
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(new PrivateVerificationReadResult(stream, contentType));
    }

    public Task<string> CreateSignedReadUrlAsync(string storedUrl, CancellationToken cancellationToken)
        => throw new NotSupportedException("Kho QA chỉ cho phép đọc tài liệu qua endpoint xác thực.");

    public Task<UploadedFileResult> UploadAsync(Stream fileStream, string fileName, string contentType,
        string folder, CancellationToken cancellationToken)
        => throw new NotSupportedException("Kho QA không dùng cho tài liệu công khai.");

    public Task DeleteAsync(string storedFileName, string folder, CancellationToken cancellationToken)
        => throw new NotSupportedException("Kho QA không tự xóa tài liệu.");

    private string Resolve(string reference)
    {
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri) ||
            uri.Scheme != "localqa" || uri.Host != "mentor-verification" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            reference.Contains('%', StringComparison.Ordinal) ||
            reference.Contains("..", StringComparison.Ordinal) ||
            reference.Contains('\\', StringComparison.Ordinal))
            throw new ArgumentException("Tham chiếu chứng chỉ QA không hợp lệ.", nameof(reference));
        var match = ReferencePattern.Match(uri.AbsolutePath);
        if (!match.Success)
            throw new ArgumentException("Đường dẫn chứng chỉ QA không hợp lệ.", nameof(reference));
        return Path.Combine(_root, match.Groups["owner"].Value, match.Groups["file"].Value);
    }

    private static bool IsWithin(string candidate, string directory) =>
        candidate.Equals(directory, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSignature(byte[] bytes, long length, string contentType) => contentType switch
    {
        "application/pdf" => bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46,
        "image/jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
        _ => false
    };
}
