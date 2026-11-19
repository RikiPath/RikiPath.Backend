using RikiPath.Application.IServices;
using RikiPath.Application.Services;

namespace RikiPath.WebApi.Services;

public sealed class ImportAudioFiles
{
    private readonly List<IFormFile> selected;
    private readonly IAwsAudioFileStorageService? aws;
    private readonly IContentAudioTranscriptionService? transcripts;
    private readonly int authorId;
    private readonly Func<string, CancellationToken, Task<string>> readServerFile;
    private readonly Dictionary<IFormFile, string> uploaded = new();

    public ImportAudioFiles(IEnumerable<IFormFile>? files, IAwsAudioFileStorageService? aws,
        IContentAudioTranscriptionService? transcripts, int authorId,
        Func<string, CancellationToken, Task<string>> readServerFile)
    {
        selected = (files ?? []).Where(file => file.FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)).ToList();
        if (selected.Count > 100 || selected.Sum(file => file.Length) > 250L * 1024 * 1024)
            throw new InvalidDataException("Mỗi lần import tối đa 100 MP3, tổng dung lượng tối đa 250 MB.");
        if (selected.Any(file => file.Length < 4 || file.Length > ListeningAudioFilePolicy.MaxBytes))
            throw new InvalidDataException("Mỗi file MP3 phải có dữ liệu và tối đa 25 MB.");
        this.aws = aws;
        this.transcripts = transcripts;
        this.authorId = authorId;
        this.readServerFile = readServerFile;
    }

    public async Task<string> ResolveAsync(string source, CancellationToken ct)
    {
        source = source.Trim().Trim('"');
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "s3" or "https" or "http")
        {
            if (aws?.TryGetContentReference(source, out var canonical) == true)
            {
                if (transcripts is not null) await transcripts.QueueAsync(canonical, authorId, ct);
                return canonical;
            }
            var previous = transcripts is null ? null : await transcripts.QueueAsync(source, authorId, ct);
            if (previous is not null) return previous.AudioUrl;
            throw new InvalidDataException("AudioUrl phải là MP3 trong bucket S3 đã cấu hình hoặc audio đã lưu trên hệ thống.");
        }
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
        if (matches.Count > 1)
            throw new InvalidDataException("Có nhiều MP3 trùng tên. Điền đường dẫn thư mục tương đối đầy đủ trong Excel.");
        if (matches.Count == 1)
        {
            if (uploaded.TryGetValue(matches[0], out var existing)) return existing;
            if (transcripts is null) throw new InvalidDataException("Dịch vụ nhận dạng audio chưa sẵn sàng.");
            await using var stream = matches[0].OpenReadStream();
            var result = await transcripts.UploadAsync(stream, path.Split('/').Last(), authorId, ct);
            uploaded[matches[0]] = result.Url;
            return result.Url;
        }
        return await readServerFile(source, ct);
    }

    private static string Normalize(string value) => value.Replace('\\', '/').TrimStart('/');
}
