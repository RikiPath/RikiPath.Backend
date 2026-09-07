using RikiPath.Application.DTOs.Content;

namespace RikiPath.Application.IClients
{
    public interface IExcelParser
    {
        Task<ExcelParseResult> ParseAsync(Stream fileStream, ContentEntityType entityType, CancellationToken cancellationToken);
    }

    public class ParsedContentRow
    {
        public int RowNumber { get; set; }

        public Dictionary<string, string> Fields { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
    }

    public class ExcelParseResult
    {
        public bool Success { get; set; }

        public List<ParsedContentRow> Rows { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
