using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Services;

public sealed class ImportImageFiles
{
    private readonly List<IFormFile> selected;
    private readonly IFileStorageService storage;
    private readonly Func<string, CancellationToken, Task<string>> readServerFile;
    private readonly Dictionary<IFormFile, string> uploaded = new();
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    public const long MaxImageBytes = 10 * 1024 * 1024;

    public ImportImageFiles(IEnumerable<IFormFile>? files, IFileStorageService storage,
        Func<string, CancellationToken, Task<string>> readServerFile)
    {
        selected = (files ?? []).Where(file => Extensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())).ToList();
        if (selected.Count > 200) throw new InvalidDataException("Mỗi lần import tối đa 200 ảnh.");
        if (selected.Any(file => file.Length < 12 || file.Length > MaxImageBytes))
            throw new InvalidDataException("Mỗi ảnh phải có dữ liệu và tối đa 10 MB.");
        this.storage = storage;
        this.readServerFile = readServerFile;
    }

    public async Task<string> ResolveAsync(string source, CancellationToken ct)
    {
        source = source.Trim().Trim('"');
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") return source;
        var path = Normalize(source);
        var matches = selected.Where(file => path.Equals(Normalize(file.FileName), StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/" + Normalize(file.FileName), StringComparison.OrdinalIgnoreCase) ||
            Normalize(file.FileName).EndsWith("/" + path, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
        {
            var name = path.Split('/').Last();
            matches = selected.Where(file => Normalize(file.FileName).Split('/').Last()
                .Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (matches.Count > 1) throw new InvalidDataException("Có nhiều ảnh trùng tên. Điền đường dẫn thư mục tương đối đầy đủ trong Excel.");
        if (matches.Count == 0) return await readServerFile(source, ct);
        var file = matches[0];
        if (uploaded.TryGetValue(file, out var existing)) return existing;
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAtLeastAsync(header, header.Length, false, ct);
        var type = DetectContentType(header, read, file.FileName);
        stream.Position = 0;
        try
        {
            var stored = await storage.UploadAsync(stream, path.Split('/').Last(), type, "content-images", ct);
            uploaded[file] = stored.Url;
            return stored.Url;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            throw new InvalidDataException("Chưa tải được ảnh lên Supabase. Vui lòng kiểm tra kho lưu trữ rồi thử lại.");
        }
    }

    public static string DetectContentType(byte[] header, int count, string fileName)
    {
        var bytes = header.AsSpan(0, count);
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" when bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff => "image/jpeg",
            ".png" when bytes.StartsWith(new byte[] {137,80,78,71,13,10,26,10}) => "image/png",
            ".webp" when bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes[8..].StartsWith("WEBP"u8) => "image/webp",
            ".gif" when bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8) => "image/gif",
            _ => throw new InvalidDataException("Ảnh phải là JPG, PNG, WebP hoặc GIF có nội dung đúng định dạng.")
        };
    }

    private static string Normalize(string value) => value.Replace('\\', '/').TrimStart('/');
}
