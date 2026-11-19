using RikiPath.Application.Helpers;

namespace RikiPath.Application.Services;

/// <summary>Resolves local media references before any sheet handler creates an import payload.</summary>
public sealed class WorkbookMediaPreparation(
    Func<string, CancellationToken, Task<string>>? audio,
    Func<string, CancellationToken, Task<string>>? images)
{
    private readonly Dictionary<(bool Audio, string Source), string> uploaded = new();
    private readonly Dictionary<(bool Audio, string Source), string> failed = new();
    private static readonly string[] Columns = ["AudioUrl", "ImageUrl", "StrokeOrderImageUrl", "ExerciseImageUrl",
        "Option1ImageUrl", "Option2ImageUrl", "Option3ImageUrl", "Option4ImageUrl"];

    public async Task PrepareAsync(IReadOnlyList<ExcelRow> rows, CancellationToken ct)
    {
        foreach (var row in rows)
        foreach (var column in Columns)
        {
            ct.ThrowIfCancellationRequested();
            var isAudio = column == "AudioUrl";
            var resolve = isAudio ? audio : images;
            if (resolve is null) continue;
            var stem = column[..^3]; // Audio, Image, Option1Image, ...
            var source = new[] { row.Get(column), column == "ImageUrl" ? row.Get("QuestionImageUrl") : null,
                row.Get(stem + "FileName"), row.Get(stem + "File"), row.Get(stem + "Path") }
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(source)) continue;
            var directory = new[] { row.Get(stem + "Directory"), row.Get(stem + "Folder"),
                isAudio ? null : row.Get("ImageDirectory"), isAudio ? null : row.Get("ImageFolder") }
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(directory) && !Path.IsPathFullyQualified(source) &&
                !Uri.TryCreate(source, UriKind.Absolute, out _))
                source = directory.Trim().Trim('"').TrimEnd('/', '\\') + "/" + source;
            if (!isAudio && Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            {
                row.Fields[ExcelSheetReader.Normalize(column)] = source;
                continue;
            }
            var key = (isAudio, source);
            if (failed.TryGetValue(key, out var error)) { row.MediaImportErrors.Add(column + ": " + error); continue; }
            try
            {
                if (!uploaded.TryGetValue(key, out var url))
                {
                    url = await resolve(source, ct);
                    uploaded[key] = url;
                }
                row.Fields[ExcelSheetReader.Normalize(column)] = url;
            }
            catch (InvalidDataException ex)
            {
                failed[key] = ex.Message;
                row.MediaImportErrors.Add(column + ": " + ex.Message);
            }
        }
    }
}
