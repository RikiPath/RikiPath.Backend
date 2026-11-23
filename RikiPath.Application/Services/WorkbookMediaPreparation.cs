using RikiPath.Application.Helpers;

namespace RikiPath.Application.Services;

/// <summary>Validates media references without writing to storage during preview.</summary>
public sealed class WorkbookMediaPreparation
{
    private static readonly string[] Columns = ["AudioUrl", "ImageUrl", "StrokeOrderImageUrl", "ExerciseImageUrl",
        "Option1ImageUrl", "Option2ImageUrl", "Option3ImageUrl", "Option4ImageUrl"];

    public Task PrepareAsync(IReadOnlyList<ExcelRow> rows, CancellationToken ct)
    {
        foreach (var row in rows)
        {
            ValidateHostedMediaUrls(row);
            foreach (var column in Columns)
            {
                ct.ThrowIfCancellationRequested();
                var stem = column[..^3];
                var source = new[] { row.Get(column), column == "ImageUrl" ? row.Get("QuestionImageUrl") : null,
                    row.Get(stem + "FileName"), row.Get(stem + "File"), row.Get(stem + "Path") }
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(source)) continue;
                if (!IsAllowedStorageUrl(source) && !row.MediaImportErrors.Any(error => error.StartsWith(column + ":", StringComparison.OrdinalIgnoreCase)))
                    row.MediaImportErrors.Add(column + ": cần URL media hợp lệ đã tải lên storage trước khi xem trước.");
            }
        }
        return Task.CompletedTask;
    }

    private static void ValidateHostedMediaUrls(ExcelRow row)
    {
        foreach (var field in row.Fields)
        {
            var key = field.Key.ToLowerInvariant();
            if (!key.EndsWith("imageurl", StringComparison.Ordinal) && !key.EndsWith("videourl", StringComparison.Ordinal)
                && !key.EndsWith("audiourl", StringComparison.Ordinal)) continue;
            var value = field.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (!IsAllowedStorageUrl(value))
            {
                row.MediaImportErrors.Add(field.Key + " phải là URL media hợp lệ trên storage được phép.");
            }
        }
    }

    private static bool IsAllowedStorageUrl(string value)
        => AllowedMediaUrlPolicy.IsApprovedMediaUrl(value);
}
