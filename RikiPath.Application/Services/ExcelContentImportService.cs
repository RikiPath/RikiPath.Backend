using System.Text.Json;
using ClosedXML.Excel;
using RikiPath.Application.Handlers;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services
{
    /// <summary>
    /// Orchestrator: đọc workbook -> với mỗi handler đã đăng ký, tìm đúng sheet theo tên -> gọi handler
    /// diff -> gộp kết quả. Service này KHÔNG biết gì về Kanji/Vocabulary/... cụ thể, toàn bộ nghiệp vụ
    /// riêng từng loại nằm trong các ISheetImportHandler (xem thư mục Handlers/).
    /// </summary>
    public sealed class ExcelContentImportService(
        IUnitOfWork unitOfWork,
        IEnumerable<ISheetImportHandler> handlers,
        IContentApprovalNotifierService notifier) : IExcelContentImportService
    {
        public async Task<ApiResponse<ImportPreviewResponse>> PreviewAsync(
            Stream fileStream, string fileName, ImportEntityType? onlyType, CancellationToken cancellationToken)
        {
            try
            {
                if (fileStream is null || fileStream.Length == 0)
                    return ApiResponse<ImportPreviewResponse>.Fail("File rỗng hoặc không đọc được.");

                using var workbook = new XLWorkbook(fileStream);

                var allHandlers = handlers.ToList();
                var targetHandlers = (onlyType is null
                    ? allHandlers
                    : allHandlers.Where(h => h.EntityType == onlyType.Value)).ToList();

                var response = new ImportPreviewResponse { PreviewId = Guid.NewGuid(), FileName = fileName };

                // Resolve Lesson rows first, even when only Practice Exercise was requested.
                var lessonHandler = allHandlers.FirstOrDefault(handler => handler.EntityType == ImportEntityType.Lesson);
                SheetImportResponse? lessonPreview = null;
                var workbookLessons = new List<LessonImportPayloadResponse>();
                if (lessonHandler is not null)
                {
                    var lessonSheet = ExcelSheetReader.FindSheet(workbook, lessonHandler.SheetName);
                    if (lessonSheet is not null)
                    {
                        var lessonRows = ToExcelRows(ExcelSheetReader.ReadRows(lessonSheet));
                        if (lessonRows.Count > 0)
                        {
                            lessonPreview = await lessonHandler.BuildDiffAsync(lessonRows, cancellationToken);
                            foreach (var lessonRow in lessonPreview.Rows.Where(row =>
                                (row.Action is ImportRowAction.New or ImportRowAction.Modified)
                                && !string.IsNullOrWhiteSpace(row.PayloadJson)))
                            {
                                try
                                {
                                    var payload = JsonSerializer.Deserialize<LessonImportPayloadResponse>(lessonRow.PayloadJson!);
                                    if (payload is not null) workbookLessons.Add(payload);
                                }
                                catch (JsonException) { /* the Lesson preview already reports invalid rows */ }
                            }
                        }
                    }
                }

                if (lessonPreview is not null && targetHandlers.Contains(lessonHandler!))
                    response.Sheets.Add(lessonPreview);

                foreach (var handler in targetHandlers)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ReferenceEquals(handler, lessonHandler)) continue;

                    var sheet = ExcelSheetReader.FindSheet(workbook, handler.SheetName);
                    if (sheet is null) continue; // Single upload chỉ 1 sheet; Bulk upload cho phép thiếu sheet

                    var rows = ToExcelRows(ExcelSheetReader.ReadRows(sheet));
                    if (rows.Count == 0) continue;

                    var sheetResult = handler is IWorkbookLessonAwareImportHandler lessonAwareHandler
                        ? await lessonAwareHandler.BuildDiffAsync(rows, workbookLessons, cancellationToken)
                        : await handler.BuildDiffAsync(rows, cancellationToken);
                    response.Sheets.Add(sheetResult);
                }

                if (response.Sheets.Count == 0)
                {
                    var expected = onlyType is null
                        ? string.Join(", ", allHandlers.Select(h => h.SheetName))
                        : targetHandlers.FirstOrDefault()?.SheetName ?? onlyType.Value.ToString();
                    return ApiResponse<ImportPreviewResponse>.Fail(
                        $"Không tìm thấy sheet hợp lệ nào trong file. Các tên sheet được hỗ trợ: {expected}.");
                }

                return ApiResponse<ImportPreviewResponse>.Success(response);
            }
            catch (Exception ex)
            {
                return ApiResponse<ImportPreviewResponse>.Fail("Không đọc được file Excel: " + ex.Message);
            }
        }

        private static List<ExcelRow> ToExcelRows(IReadOnlyList<ExcelRow> rows)
            => rows.Select(row => new ExcelRow
            {
                RowNumber = row.RowNumber,
                Fields = row.Fields.ToDictionary(
                    pair => ExcelSheetReader.Normalize(pair.Key),
                    pair => pair.Value)
            }).ToList();

        public async Task<ApiResponse<ImportConfirmResponse>> ConfirmAsync(
            ConfirmImportRequest request, int actorUserId, CancellationToken cancellationToken)
        {
            try
            {
                if (request.Rows.Count == 0)
                    return ApiResponse<ImportConfirmResponse>.Fail("Không có dòng nào được gửi lên để xác nhận.");

                var batchId = Guid.NewGuid();
                var now = DateTime.UtcNow;
                var drafts = new List<ContentApprovalRequest>();

                // Từ lúc xem trước đến lúc xác nhận, người khác có thể vừa import cùng nội dung -> nạp lại các bản nháp đang mở
                // để không tạo trùng bản nháp cho Admin.
                var openKeysByType = new Dictionary<ImportEntityType, HashSet<string>>();
                foreach (var type in request.Rows.Select(r => r.EntityType).Distinct())
                    openKeysByType[type] = await ImportHandlerSupport.LoadOpenRowKeysAsync(unitOfWork, type, cancellationToken);
                var seen = new HashSet<(ImportEntityType, string)>();

                foreach (var row in request.Rows)
                {
                    // Phòng thủ: chỉ nhận New/Modified có payload, dù FE đáng lẽ đã lọc trước khi gửi.
                    if (row.Action is not (ImportRowAction.New or ImportRowAction.Modified)) continue;
                    if (string.IsNullOrWhiteSpace(row.PayloadJson) || string.IsNullOrWhiteSpace(row.RowKey)) continue;
                    if (row.EntityType == ImportEntityType.MockTest)
                    {
                        var validationError = ValidateMockTestPayload(row.PayloadJson);
                        if (validationError is not null)
                            return ApiResponse<ImportConfirmResponse>.Fail(
                                $"Dòng {row.SourceRowNumber}: {validationError}");
                    }
                    if (openKeysByType[row.EntityType].Contains(row.RowKey)) continue;
                    if (!seen.Add((row.EntityType, row.RowKey))) continue;

                    drafts.Add(new ContentApprovalRequest
                    {
                        BatchId = batchId,
                        EntityType = row.EntityType,
                        Action = row.Action,
                        TargetEntityId = row.TargetEntityId,
                        RowKey = row.RowKey,
                        SourceRowNumber = row.SourceRowNumber,
                        SourceFileName = request.SourceFileName ?? string.Empty,
                        PayloadJson = row.PayloadJson,
                        // Admin xem được "đổi gì" (vd "Bài 1 (N5) → Bài 2 (N5)") khi duyệt.
                        DiffSummaryJson = row.ChangedFields is { Count: > 0 } ? JsonSerializer.Serialize(row.ChangedFields) : null,
                        Status = ContentApprovalStatus.Draft,
                        SubmittedByUserId = actorUserId,
                        SubmittedAt = now,
                    });
                }

                if (drafts.Count == 0)
                    return ApiResponse<ImportConfirmResponse>.Fail(
                        "Không có dòng hợp lệ (New/Modified kèm dữ liệu) nào để tạo bản nháp.");

                await unitOfWork.ContentApprovalRequests.AddRangeAsync(drafts);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                // Bản nháp đã lưu xong: gửi thông báo lỗi thì không được báo "thất bại" (author bấm lại sẽ tạo trùng).
                try
                {
                    // Đặt tên lô dữ liệu hiển thị trong thông báo (VD: tên file Excel hoặc BatchId)
                    var batchName = string.IsNullOrWhiteSpace(request.SourceFileName)
                        ? $"Lô {batchId.ToString()[..8]}"
                        : request.SourceFileName;

                    await notifier.NotifyAdminsOnRequestCreatedAsync(drafts.Count, batchName, cancellationToken);
                }
                catch (Exception)
                {
                    // Best-effort: Bỏ qua lỗi gửi thông báo để không làm crash luồng import
                    /* TODO: ghi log nếu có ILogger */
                }

                return ApiResponse<ImportConfirmResponse>.Success(new ImportConfirmResponse
                {
                    BatchId = batchId,
                    DraftsCreatedCount = drafts.Count,
                    SkippedCount = request.Rows.Count - drafts.Count,
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<ImportConfirmResponse>.Fail("Lỗi khi tạo bản nháp: " + ex.Message);
            }
        }

        private static string? ValidateMockTestPayload(string payloadJson)
        {
            MockTestImportPayloadResponse? payload;
            try
            {
                payload = JsonSerializer.Deserialize<MockTestImportPayloadResponse>(payloadJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex) { return $"Payload MockTest không hợp lệ: {ex.Message}"; }
            if (payload is null || payload.Sections.Count == 0) return "MockTest phải có ít nhất một section.";
            foreach (var question in payload.Sections.SelectMany(section => section.Questions))
            {
                var type = string.IsNullOrWhiteSpace(question.QuestionType) ? "SingleChoice" : question.QuestionType;
                if (type.Equals("SingleChoice", StringComparison.OrdinalIgnoreCase)
                    || type.Equals("Listening", StringComparison.OrdinalIgnoreCase)
                    || type.Equals("Reading", StringComparison.OrdinalIgnoreCase))
                {
                    if (question.Options.Count < 2 || question.Options.Count(option => option.IsCorrect) != 1)
                        return $"Câu '{question.QuestionText}' dạng {type} cần ít nhất 2 lựa chọn và đúng 1 đáp án đúng.";
                }
                else if (type.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase))
                {
                    if (question.Options.Count < 2 || question.Options.All(option => !option.IsCorrect))
                        return $"Câu '{question.QuestionText}' dạng MultipleChoice cần ít nhất 2 lựa chọn và có đáp án đúng.";
                }
                else if (new[] { "Ordering", "FillBlank", "Matching", "ShortAnswer" }
                    .Contains(type, StringComparer.OrdinalIgnoreCase))
                {
                    if (question.Options.Count != 0 || question.InteractionData is null || question.AnswerKey is null)
                        return $"Câu '{question.QuestionText}' dạng {type} cần InteractionDataJson và AnswerKeyJson, không dùng các cột Option.";
                }
                else return $"QuestionType '{type}' không được hỗ trợ.";
            }
            return null;
        }
    }
}
