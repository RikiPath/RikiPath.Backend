using ClosedXML.Excel;
using Moq;
using RikiPath.Application;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Services;

namespace RikiPath.Tests;

public sealed class WorkbookPreviewTests
{
    [Fact]
    public void Workbook_preview_preserves_visible_sheets_and_source_cell_coordinates()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var lesson = workbook.AddWorksheet("Lesson");
            lesson.Cell("A1").Value = "Tiêu đề";
            lesson.Cell("C4").Value = "日本語";
            lesson.Cell("D4").FormulaA1 = "1+1";
            workbook.AddWorksheet("Ghi chú").Cell("B2").Value = "Nội dung ngoài mẫu import";
            workbook.AddWorksheet("Ẩn").Visibility = XLWorksheetVisibility.Hidden;
            workbook.SaveAs(stream);
        }
        stream.Position = 0;
        var unit = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var service = new ExcelContentImportService(unit.Object, []);

        var response = service.PreviewWorkbook(stream, "hoc-lieu.xlsx", default);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var preview = response.Result!;
        Assert.Equal("hoc-lieu.xlsx", preview.FileName);
        Assert.False(preview.IsTruncated);
        Assert.Equal(new[] { "Lesson", "Ghi chú" }, preview.Sheets.Select(sheet => sheet.Name));
        var lessonSheet = preview.Sheets[0];
        Assert.Equal(new[] { "A", "C", "D" }, lessonSheet.Columns.Select(column => column.Label));
        Assert.Equal(new[] { 1, 4 }, lessonSheet.Rows.Select(row => row.Number));
        Assert.Equal("日本語", lessonSheet.Rows[1].Cells[3]);
        Assert.True(lessonSheet.Rows[1].Cells.ContainsKey(4));
        Assert.Equal("Nội dung ngoài mẫu import", Assert.Single(preview.Sheets[1].Rows).Cells[2]);
        unit.VerifyNoOtherCalls();
    }

    [Fact]
    public void Workbook_preview_rejects_invalid_xlsx()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var service = new ExcelContentImportService(Mock.Of<IUnitOfWork>(), []);
        Assert.Equal(400, service.PreviewWorkbook(stream, "bad.xlsx", default).StatusCode);
    }

    [Fact]
    public void Edited_workbook_keeps_styles_formulas_and_untouched_sheets()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var lesson = workbook.AddWorksheet("Lesson");
            lesson.Cell("A1").Value = "Tiêu đề cũ";
            lesson.Cell("A1").Style.Font.Bold = true;
            lesson.Cell("B1").Value = 15.5;
            lesson.Cell("C1").FormulaA1 = "B1+1";
            lesson.Range("A2:B2").Merge().FirstCell().Value = "Nội dung gộp";
            workbook.AddWorksheet("Ghi chú").Cell("A1").Value = "Giữ nội dung";
            workbook.SaveAs(stream);
        }
        stream.Position = 0;
        var service = new ExcelContentImportService(Mock.Of<IUnitOfWork>(), []);
        var edits = new WorkbookCellEdit[]
        {
            new("Lesson", 1, 1, "日本語"),
            new("Lesson", 1, 2, "17.5"),
            new("Lesson", 1, 4, "=1+1"),
            new("Lesson", 2, 1, ""),
        };
        var result = service.EditWorkbook(stream, edits, default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        using var edited = new XLWorkbook(new MemoryStream(result.Result!));
        var updated = edited.Worksheet("Lesson");
        Assert.Equal("日本語", updated.Cell("A1").GetString());
        Assert.True(updated.Cell("A1").Style.Font.Bold);
        Assert.Equal(17.5, updated.Cell("B1").GetDouble());
        Assert.Equal("B1+1", updated.Cell("C1").FormulaA1);
        Assert.Equal("=1+1", updated.Cell("D1").GetString());
        Assert.False(updated.Cell("D1").HasFormula);
        Assert.Equal("", updated.Cell("A2").GetString());
        Assert.True(updated.Cell("B2").IsMerged());
        Assert.Equal("Giữ nội dung", edited.Worksheet("Ghi chú").Cell("A1").GetString());

        stream.Position = 0;
        var formulaEdit = service.EditWorkbook(stream, [new("Lesson", 1, 3, "20")], default);
        Assert.Equal(400, formulaEdit.StatusCode);
        Assert.Contains("công thức", formulaEdit.ErrorMessage);

        stream.Position = 0;
        var mergedEdit = service.EditWorkbook(stream, [new("Lesson", 2, 2, "20")], default);
        Assert.Equal(400, mergedEdit.StatusCode);
        Assert.Contains("vùng gộp", mergedEdit.ErrorMessage);
    }
}
