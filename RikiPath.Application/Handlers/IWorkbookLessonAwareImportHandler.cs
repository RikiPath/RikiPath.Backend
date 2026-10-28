using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;

namespace RikiPath.Application.Handlers;

/// <summary>Import handler that can resolve lessons declared in another sheet of the same workbook.</summary>
public interface IWorkbookLessonAwareImportHandler : ISheetImportHandler
{
    Task<SheetImportResponse> BuildDiffAsync(
        IReadOnlyList<ExcelRow> rows,
        IReadOnlyList<LessonImportPayloadResponse> workbookLessons,
        CancellationToken cancellationToken);
}
