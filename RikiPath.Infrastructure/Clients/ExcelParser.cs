using ClosedXML.Excel;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.IClients;

namespace RikiPath.Infrastructure.Clients
{
    public class ExcelParser : IExcelParser
    {
        public Task<ExcelParseResult> ParseAsync(Stream fileStream, ContentEntityType entityType, CancellationToken cancellationToken)
        {
            var result = new ExcelParseResult();

            try
            {
                using var workbook = new XLWorkbook(fileStream);
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet is null)
                {
                    result.Success = false;
                    result.Errors.Add("File Excel không có sheet nào.");
                    return Task.FromResult(result);
                }

                var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
                if (lastColumn == 0)
                {
                    result.Success = false;
                    result.Errors.Add("File Excel không có dữ liệu.");
                    return Task.FromResult(result);
                }

                // Đọc header ở dòng 1: cột -> tên cột
                var headerRow = worksheet.Row(1);
                var headers = new Dictionary<int, string>();
                for (var col = 1; col <= lastColumn; col++)
                {
                    var headerText = headerRow.Cell(col).GetString().Trim();
                    if (!string.IsNullOrWhiteSpace(headerText))
                        headers[col] = headerText;
                }

                if (headers.Count == 0)
                {
                    result.Success = false;
                    result.Errors.Add("Không đọc được header ở dòng 1. Dòng đầu tiên phải là tên cột.");
                    return Task.FromResult(result);
                }

                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                for (var rowNum = 2; rowNum <= lastRow; rowNum++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var row = worksheet.Row(rowNum);
                    if (row.IsEmpty()) continue; // bỏ qua dòng trống (VD dòng cuối file)

                    var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (col, headerName) in headers)
                        fields[headerName] = row.Cell(col).GetString().Trim();

                    result.Rows.Add(new ParsedContentRow
                    {
                        RowNumber = rowNum,
                        Fields = fields
                    });
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Lỗi đọc file Excel: {ex.Message}");
            }

            return Task.FromResult(result);
        }
    }
}

