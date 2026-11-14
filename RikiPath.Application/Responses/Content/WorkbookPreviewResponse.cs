namespace RikiPath.Application.Responses.Content;

public sealed record WorkbookPreviewResponse(string FileName, List<WorkbookPreviewSheet> Sheets, bool IsTruncated);
public sealed record WorkbookPreviewSheet(string Name, List<WorkbookPreviewColumn> Columns, List<WorkbookPreviewRow> Rows);
public sealed record WorkbookPreviewColumn(int Index, string Label);
public sealed record WorkbookPreviewRow(int Number, Dictionary<int, string> Cells)
{
    public List<int>? FormulaColumns { get; set; }
    public Dictionary<int, string>? NumericValues { get; set; }
}
