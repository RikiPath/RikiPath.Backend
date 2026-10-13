using ClosedXML.Excel;

namespace RikiPath.Application.Helpers
{
    /// <summary>1 dòng dữ liệu đã đọc từ Excel, key của Fields đã chuẩn hoá qua <see cref="ExcelSheetReader.Normalize"/>.</summary>
    public sealed class ExcelRow
    {
        public int RowNumber { get; init; }
        public Dictionary<string, string> Fields { get; init; } = new();

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
