using ClosedXML.Excel;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.IClients;
using System.Text;

namespace RikiPath.Infrastructure.Clients
{
    public class ExcelParser : IExcelParser
    {
        public Task<ExcelParseResult> ParseAsync(Stream fileStream, CancellationToken cancellationToken)
        {
            var result = new ExcelParseResult();

            try
            {
                using var workbook = new XLWorkbook(fileStream);
                var dataWorksheets = workbook.Worksheets
                    .Where(sheet => sheet.LastRowUsed() is not null && sheet.LastColumnUsed() is not null)
                    .ToList();
                if (dataWorksheets.Count == 0)
                {
                    result.Success = false;
                    result.Errors.Add("File Excel không có sheet chứa dữ liệu.");
                    return Task.FromResult(result);
                }
                if (dataWorksheets.Count > 1)
                {
                    result.Success = false;
                    result.Errors.Add("File import chỉ được có một sheet chứa dữ liệu.");
                    return Task.FromResult(result);
                }
                var worksheet = dataWorksheets[0];

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
                    var originalHeader = headerRow.Cell(col).GetString().Trim();
                    if (!string.IsNullOrWhiteSpace(originalHeader))
                    {
                        var headerText = NormalizeHeader(originalHeader);
                        if (headers.Values.Contains(headerText, StringComparer.OrdinalIgnoreCase))
                        {
                            result.Success = false;
                            result.Errors.Add($"Header '{originalHeader}' bị trùng sau khi chuẩn hóa thành '{headerText}'. Mỗi tên cột chỉ được xuất hiện một lần.");
                            return Task.FromResult(result);
                        }
                        headers[col] = headerText;
                    }
                }

                if (headers.Count == 0)
                {
                    result.Success = false;
                    result.Errors.Add("Không đọc được header ở dòng 1. Dòng đầu tiên phải là tên cột.");
                    return Task.FromResult(result);
                }

                if (!headers.Values.Any(x => string.Equals(x, "Type", StringComparison.OrdinalIgnoreCase)))
                {
                    result.Success = false;
                    result.Errors.Add("Thiếu cột Type. Mỗi dòng cần khai báo loại nội dung cần import.");
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
                        // Đọc chuỗi đã định dạng để giữ đúng giá trị hiển thị trong Excel (ví dụ
                        // CertificateLevel dạng text, mã ID/số nét, ngày giờ) thay vì chỉ lấy text literal.
                        fields[headerName] = row.Cell(col).GetFormattedString().Trim();

                    result.Rows.Add(new ParsedContentRow
                    {
                        RowNumber = rowNum,
                        Fields = fields
                    });
                }

                if (result.Rows.Count == 0)
                {
                    result.Success = false;
                    result.Errors.Add("Sheet chỉ có header, chưa có dòng nội dung nào để import.");
                    return Task.FromResult(result);
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

        /// <summary>
        /// Chuẩn hóa tên cột để các cách viết như Certificate Level, certificate_level và
        /// Certificate-Level cùng ánh xạ vào khóa CertificateLevel, đồng thời vẫn giữ mọi cột khác.
        /// </summary>
        private static string NormalizeHeader(string header)
        {
            var builder = new StringBuilder(header.Length);
            foreach (var character in header)
            {
                if (!char.IsWhiteSpace(character) && character is not '_' and not '-')
                    builder.Append(character);
            }
            return builder.ToString();
        }
    }
}

