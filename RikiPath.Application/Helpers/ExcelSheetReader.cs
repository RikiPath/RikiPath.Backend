using ClosedXML.Excel;

using System.Globalization;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses.Content;

namespace RikiPath.Application.Helpers
{
    /// <summary>1 dòng dữ liệu đã đọc từ Excel, key của Fields đã chuẩn hoá qua <see cref="ExcelSheetReader.Normalize"/>.</summary>
    public sealed class ExcelRow
    {
        public int RowNumber { get; init; }
        public Dictionary<string, string> Fields { get; init; } = new();
        internal List<string> MediaImportErrors { get; } = [];

        /// <summary>Lấy giá trị theo tên cột gốc (vd "Certificate Level Code") - tự chuẩn hoá để so khớp, không cần biết thứ tự cột.</summary>
        public string? Get(string headerName)
            => Fields.TryGetValue(ExcelSheetReader.Normalize(headerName), out var v) && v.Length > 0 ? v : null;

        public bool Has(string headerName) => Get(headerName) is not null;
    }

    /// <summary>
    /// Đọc Excel theo tên cột/tên sheet (KHÔNG hardcode index) bằng ClosedXML.
    /// Yêu cầu "Dynamic Columns" + "Dynamic Sheets" + "ignore case/spaces" nằm hết ở đây,
    /// mỗi ISheetImportHandler chỉ việc gọi row.Get("Tên cột") là lấy được giá trị.
    /// </summary>
    public static class ExcelSheetReader
    {
        public static WorkbookPreviewResponse ReadWorkbookPreview(Stream stream, string fileName, CancellationToken ct)
        {
            const int maxCells = 100_000;
            const int maxCharacters = 4_000_000;
            var culture = CultureInfo.GetCultureInfo("vi-VN");
            using var workbook = new XLWorkbook(stream);
            var sheets = new List<WorkbookPreviewSheet>();
            var cellCount = 0;
            var characterCount = 0;

            foreach (var sheet in workbook.Worksheets)
            {
                ct.ThrowIfCancellationRequested();
                if (sheet.Visibility != XLWorksheetVisibility.Visible) continue;
                var columns = new SortedDictionary<int, WorkbookPreviewColumn>();
                var rows = new SortedDictionary<int, WorkbookPreviewRow>();
                foreach (var cell in sheet.CellsUsed(XLCellsUsedOptions.Contents))
                {
                    ct.ThrowIfCancellationRequested();
                    var value = cell.HasFormula
                        ? (cell.CachedValue.IsBlank ? "=" + cell.FormulaA1 : cell.CachedValue.ToString(culture))
                        : cell.GetFormattedString(culture);
                    if (value.Length == 0) continue;
                    if (cellCount == maxCells || characterCount + value.Length > maxCharacters)
                    {
                        sheets.Add(new WorkbookPreviewSheet(sheet.Name, columns.Values.ToList(), rows.Values.ToList()));
                        return new WorkbookPreviewResponse(fileName, sheets, IsTruncated: true);
                    }

                    var column = cell.Address.ColumnNumber;
                    var row = cell.Address.RowNumber;
                    if (!rows.TryGetValue(row, out var previewRow))
                        rows[row] = previewRow = new WorkbookPreviewRow(row, new Dictionary<int, string>());
                    columns.TryAdd(column, new WorkbookPreviewColumn(column, cell.Address.ColumnLetter));
                    previewRow.Cells.Add(column, value);
                    if (cell.HasFormula)
                        (previewRow.FormulaColumns ??= []).Add(column);
                    else if (cell.DataType == XLDataType.Number)
                        (previewRow.NumericValues ??= [])[column] = cell.GetDouble().ToString("R", CultureInfo.InvariantCulture);
                    cellCount++;
                    characterCount += value.Length;
                }
                sheets.Add(new WorkbookPreviewSheet(sheet.Name, columns.Values.ToList(), rows.Values.ToList()));
            }

            return new WorkbookPreviewResponse(fileName, sheets, IsTruncated: false);
        }

        public static byte[] ApplyWorkbookEdits(Stream stream, IReadOnlyList<WorkbookCellEdit> edits, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            using var workbook = new XLWorkbook(stream);
            var visibleSheets = workbook.Worksheets.Where(sheet => sheet.Visibility == XLWorksheetVisibility.Visible)
                .ToDictionary(sheet => sheet.Name, StringComparer.Ordinal);
            var addresses = new HashSet<(string Sheet, int Row, int Column)>();
            foreach (var edit in edits)
            {
                ct.ThrowIfCancellationRequested();
                if (edit is null || string.IsNullOrWhiteSpace(edit.SheetName) ||
                    !visibleSheets.TryGetValue(edit.SheetName, out var sheet) ||
                    edit.Row is < 1 or > 1_048_576 || edit.Column is < 1 or > 16_384 ||
                    edit.Value is null || edit.Value.Length > 32_767)
                    throw new ArgumentException("Vị trí ô hoặc giá trị sửa không hợp lệ.");
                if (!addresses.Add((edit.SheetName, edit.Row, edit.Column)))
                    throw new ArgumentException("Một ô Excel chỉ được sửa một lần trong mỗi yêu cầu.");

                var cell = sheet.Cell(edit.Row, edit.Column);
                if (cell.HasFormula)
                    throw new ArgumentException($"Ô {cell.Address} trong sheet {sheet.Name} chứa công thức.");
                if (cell.IsMerged())
                {
                    var first = cell.MergedRange().FirstCell().Address;
                    if (first.RowNumber != edit.Row || first.ColumnNumber != edit.Column)
                        throw new ArgumentException($"Hãy sửa ô đầu tiên của vùng gộp tại {sheet.Name}!{first}.");
                }

                if (edit.Value.Length == 0)
                    cell.Clear(XLClearOptions.Contents);
                else if (cell.DataType == XLDataType.Number &&
                    double.TryParse(edit.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
                    double.IsFinite(number))
                    cell.Value = number;
                else if (cell.DataType == XLDataType.Boolean && bool.TryParse(edit.Value, out var flag))
                    cell.Value = flag;
                else
                    cell.Value = edit.Value;
            }

            using var edited = new MemoryStream();
            workbook.SaveAs(edited);
            return edited.ToArray();
        }

        /// <summary>Chuẩn hoá tên để so khớp: bỏ hết khoảng trắng, hạ thường. "Certificate Level Code" == "certificatelevelcode".</summary>
        public static string Normalize(string input)
            => string.IsNullOrWhiteSpace(input)
                ? string.Empty
                : new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

        public static IXLWorksheet? FindSheet(XLWorkbook workbook, string wantedName)
        {
            var target = Normalize(wantedName);
            return workbook.Worksheets.FirstOrDefault(ws => Normalize(ws.Name) == target);
        }

        /// <summary>
        /// Đọc toàn bộ dòng dữ liệu của 1 sheet. Dòng đầu tiên có dữ liệu luôn được coi là header.
        /// Dòng trống hoàn toàn bị bỏ qua (không tính vào TotalRows).
        /// </summary>
        public static List<ExcelRow> ReadRows(IXLWorksheet sheet)
        {
            var result = new List<ExcelRow>();

            var headerRow = sheet.FirstRowUsed();
            var lastRow = sheet.LastRowUsed();
            if (headerRow is null || lastRow is null) return result;

            // column index -> tên cột đã chuẩn hoá. Cột header rỗng (file có cột thừa) bị bỏ qua.
            var headerMap = new Dictionary<int, string>();
            foreach (var cell in headerRow.CellsUsed())
            {
                var normalized = Normalize(cell.GetString());
                if (normalized.Length > 0) headerMap[cell.Address.ColumnNumber] = normalized;
            }

            for (var r = headerRow.RowNumber() + 1; r <= lastRow.RowNumber(); r++)
            {
                var xlRow = sheet.Row(r);
                if (xlRow.IsEmpty()) continue;

                var fields = new Dictionary<string, string>();
                var hasValue = false;
                foreach (var (colIndex, header) in headerMap)
                {
                    var value = xlRow.Cell(colIndex).GetString().Trim();
                    fields[header] = value;
                    if (value.Length > 0) hasValue = true;
                }

                if (!hasValue) continue; // dòng trống xét theo đúng các cột đã khai báo ở header

                result.Add(new ExcelRow { RowNumber = r, Fields = fields });
            }

            return result;
        }
    }
}
