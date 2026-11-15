using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
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
        IEnumerable<ISheetImportHandler> handlers) : IExcelContentImportService
    {
        public ApiResponse<WorkbookPreviewResponse> PreviewWorkbook(Stream fileStream, string fileName, CancellationToken cancellationToken)
        {
            try
            {
                return ApiResponse<WorkbookPreviewResponse>.Success(
                    ExcelSheetReader.ReadWorkbookPreview(fileStream, fileName, cancellationToken));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                return ApiResponse<WorkbookPreviewResponse>.Fail("Không đọc được file .xlsx. Hãy kiểm tra file rồi thử lại.");
            }
        }

        public ApiResponse<byte[]> EditWorkbook(Stream fileStream, IReadOnlyList<WorkbookCellEdit> edits, CancellationToken cancellationToken)
        {
            if (edits.Count == 0)
                return ApiResponse<byte[]>.Fail("Chưa có ô Excel nào được sửa.");
            try
            {
                return ApiResponse<byte[]>.Success(ExcelSheetReader.ApplyWorkbookEdits(fileStream, edits, cancellationToken));
            }
            catch (OperationCanceledException) { throw; }
            catch (ArgumentException ex) { return ApiResponse<byte[]>.Fail(ex.Message); }
            catch (Exception)
            {
                return ApiResponse<byte[]>.Fail("Không thể tạo bản Excel đã sửa. Hãy kiểm tra file rồi thử lại.");
            }
        }

        public async Task<ApiResponse<ImportPreviewResponse>> PreviewAsync(
            Stream fileStream, string fileName, ImportEntityType? onlyType, CancellationToken cancellationToken,
            Func<string, CancellationToken, Task<string>>? uploadLocalAudio = null,
            Func<string, CancellationToken, Task<string>>? uploadLocalImage = null)
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

                    if (handler.EntityType == ImportEntityType.MockTest && (uploadLocalAudio is not null || uploadLocalImage is not null))
                    {
                        var uploaded = new Dictionary<(bool IsAudio, string Path), string>();
                        var mediaColumns = new[] { "AudioUrl", "ImageUrl", "Option1ImageUrl", "Option2ImageUrl", "Option3ImageUrl", "Option4ImageUrl" };
                        foreach (var row in rows)
                        foreach (var column in mediaColumns)
                        {
                            var isAudio = column == "AudioUrl";
                            var upload = isAudio ? uploadLocalAudio : uploadLocalImage;
                            var path = row.Get(column)?.Trim();
                            if (upload is null || string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path)) continue;
                            try
                            {
                                var key = (isAudio, path);
                                if (!uploaded.TryGetValue(key, out var url))
                                {
                                    url = await upload(path, cancellationToken);
                                    uploaded.Add(key, url);
                                }
                                row.Fields[ExcelSheetReader.Normalize(column)] = url;
                            }
                            catch (InvalidDataException ex)
                            {
                                row.MediaImportErrors.Add($"{column}: {ex.Message}");
                            }
                        }
                    }

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
            var batchId = request.BatchId ?? Guid.NewGuid();
            var response = ApiResponse<ImportConfirmResponse>.Fail("Không thể lưu bản nháp.");
            try
            {
                await unitOfWork.ExecuteContentAuthorWriteAsync(actorUserId, async token =>
                    response = await SaveDraftsAsync(request, actorUserId, batchId, token), cancellationToken);
                return response;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<ImportConfirmResponse>.Fail("Lỗi khi lưu bản nháp: " + ex.Message);
            }
        }

        private async Task<ApiResponse<ImportConfirmResponse>> SaveDraftsAsync(ConfirmImportRequest request, int actorUserId,
            Guid batchId, CancellationToken cancellationToken)
        {
            if (batchId == Guid.Empty || request.Rows is not { Count: > 0 })
                return ApiResponse<ImportConfirmResponse>.Fail("Không có dòng hợp lệ để lưu bản nháp.");
            var sourceFileName = string.IsNullOrWhiteSpace(request.SourceFileName)
                ? "Import Excel.xlsx" : Path.GetFileName(request.SourceFileName.Trim());
            if (sourceFileName.Length > 260)
                return ApiResponse<ImportConfirmResponse>.Fail("Tên file Excel quá dài.");

            var existingBatch = await unitOfWork.ContentApprovalRequests.Query()
                .Where(item => !item.IsDeleted && item.BatchId == batchId)
                .GroupBy(item => item.SubmittedByUserId)
                .Select(group => new { AuthorId = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);
            if (existingBatch.Count != 0)
            {
                var existing = existingBatch.SingleOrDefault(item => item.AuthorId == actorUserId);
                if (existing is null)
                    return ApiResponse<ImportConfirmResponse>.Fail("Mã lô import đã được sử dụng.");
                return ApiResponse<ImportConfirmResponse>.Success(new ImportConfirmResponse
                {
                    BatchId = batchId,
                    DraftsCreatedCount = existing.Count,
                    SkippedCount = Math.Max(0, request.Rows.Count - existing.Count),
                });
            }
            var now = DateTime.UtcNow;
            var drafts = new List<ContentApprovalRequest>();

            var types = request.Rows.Select(row => row.EntityType).Distinct().ToArray();
            var openRows = await unitOfWork.ContentApprovalRequests.Query()
                .Where(item => !item.IsDeleted && item.SubmittedByUserId == actorUserId && types.Contains(item.EntityType) &&
                    (item.Status == ContentApprovalStatus.Draft || item.Status == ContentApprovalStatus.PendingReview))
                .Select(item => new { item.EntityType, item.RowKey, item.PayloadJson, item.TargetEntityId }).ToListAsync(cancellationToken);
            var seen = openRows.Select(item => (item.EntityType, item.RowKey,
                item.EntityType == ImportEntityType.PracticeExercise ? ManualContentService.ReadListInfo(item.PayloadJson)?.CertificateLevelId : null))
                .ToHashSet();
            var skillNames = types.Contains(ImportEntityType.MockTest)
                ? (await unitOfWork.LanguageSkills.GetAllAsync(cancellationToken)).ToDictionary(skill => skill.Id, skill => skill.Name)
                : new Dictionary<int, string>();
            var levelCodes = await unitOfWork.CertificateLevels.Query()
                .Where(level => !level.IsDeleted && !level.CertificateType.IsDeleted &&
                    level.CertificateType.IsActive && level.CertificateType.Name == "JLPT")
                .ToDictionaryAsync(level => level.Id, level => level.Code, cancellationToken);
            var seenTargets = openRows.Where(item => item.TargetEntityId.HasValue)
                .Select(item => (item.EntityType, item.TargetEntityId!.Value)).ToHashSet();

            foreach (var row in request.Rows)
            {
                // Phòng thủ: chỉ nhận New/Modified có payload, dù FE đáng lẽ đã lọc trước khi gửi.
                if (row.Action is not (ImportRowAction.New or ImportRowAction.Modified)) continue;
                if (string.IsNullOrWhiteSpace(row.PayloadJson) || string.IsNullOrWhiteSpace(row.RowKey)) continue;
                if (row.RowKey.Length > 500 || !Enum.IsDefined(row.EntityType))
                    return ApiResponse<ImportConfirmResponse>.Fail($"Dòng {row.SourceRowNumber}: thông tin học liệu không hợp lệ.");
                if (row.EntityType == ImportEntityType.MockTest)
                {
                    var validationError = ValidateMockTestPayload(row.PayloadJson, skillNames, levelCodes);
                    if (validationError is not null)
                        return ApiResponse<ImportConfirmResponse>.Fail(
                            $"Dòng {row.SourceRowNumber}: {validationError}");
                }
                var info = ManualContentService.ReadListInfo(row.PayloadJson);
                if (info?.CertificateLevelId is not int levelId || !levelCodes.TryGetValue(levelId, out var levelCode)
                    || levelCode is not ("N5" or "N4" or "N3" or "N2"))
                    return ApiResponse<ImportConfirmResponse>.Fail($"Dòng {row.SourceRowNumber}: hãy chọn cấp độ từ N5 đến N2.");
                if (!seen.Add((row.EntityType, row.RowKey, row.EntityType == ImportEntityType.PracticeExercise ? levelId : null))) continue;
                if (row.Action == ImportRowAction.Modified)
                {
                    if (row.TargetEntityId is not > 0)
                        return ApiResponse<ImportConfirmResponse>.Fail($"Dòng {row.SourceRowNumber}: thiếu học liệu gốc để cập nhật.");
                    if (!seenTargets.Add((row.EntityType, row.TargetEntityId.Value))) continue;
                }

                drafts.Add(new ContentApprovalRequest
                {
                    BatchId = batchId,
                    EntityType = row.EntityType,
                    Action = row.Action,
                    TargetEntityId = row.Action == ImportRowAction.Modified ? row.TargetEntityId : null,
                    RowKey = row.RowKey,
                    SourceRowNumber = row.SourceRowNumber,
                    SourceFileName = sourceFileName,
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
                    "Không có nội dung mới để lưu. Hãy kiểm tra các bản nháp hoặc nội dung đang chờ duyệt của bạn.");

            await unitOfWork.ContentApprovalRequests.AddRangeAsync(drafts, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<ImportConfirmResponse>.Success(new ImportConfirmResponse
            {
                BatchId = batchId,
                DraftsCreatedCount = drafts.Count,
                SkippedCount = request.Rows.Count - drafts.Count,
            });
        }

        internal static string? ValidateMockTestPayload(string payloadJson, IReadOnlyDictionary<int, string> skillNames,
            IReadOnlyDictionary<int, string> levelCodes)
        {
            MockTestImportPayloadResponse? payload;
            try
            {
                payload = JsonSerializer.Deserialize<MockTestImportPayloadResponse>(payloadJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex) { return $"Payload MockTest không hợp lệ: {ex.Message}"; }
            if (payload is null || payload.Sections.Count == 0) return "MockTest phải có ít nhất một section.";
            foreach (var section in payload.Sections)
            foreach (var question in section.Questions)
            {
                var type = string.IsNullOrWhiteSpace(question.QuestionType) ? "SingleChoice" : question.QuestionType;
                var audioError = ImportHandlerSupport.ValidateMockListeningAudio(type, question.AudioUrl,
                    skillNames.GetValueOrDefault(section.LanguageSkillId) ?? section.LanguageSkillName,
                    section.Title, section.CertificateSectionCode);
                if (audioError is not null)
                    return $"Phần '{section.Title}', câu số {question.SortOrder}: {audioError}";
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
                    if (SentenceOrdering.IsType(type) && !SentenceOrdering.TryRead(question.InteractionData, question.AnswerKey, out _, out _))
                        return $"Câu '{question.QuestionText}': {SentenceOrdering.InvalidContentMessage}";
                }
                else return $"QuestionType '{type}' không được hỗ trợ.";
            }
            if (!levelCodes.TryGetValue(payload.CertificateLevelId, out var levelCode))
                return "Cấp độ của đề thi không tồn tại hoặc đã bị xóa.";
            return ImportHandlerSupport.ValidateMockTestCoverage(levelCode, payload.Sections, skillNames);
        }
    }
}
