namespace RikiPath.Application.Requests.Content;

public sealed record WorkbookCellEdit(string SheetName, int Row, int Column, string Value);
