using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Handlers
{
    /// <summary>Khóa tự nhiên: Title (tên mẫu ngữ pháp).</summary>
    public sealed class GrammarImportHandler(IUnitOfWork unitOfWork) : ISheetImportHandler
    {
        public ImportEntityType EntityType => ImportEntityType.Grammar;
        public string SheetName => "Grammar Pattern";

        public async Task<SheetImportResponse> BuildDiffAsync(IReadOnlyList<ExcelRow> rows, CancellationToken cancellationToken)
        {
            // Sửa Include: chuyển từ CertificateLevel sang ContentLevelMappings
            var existingPatterns = await unitOfWork.GrammarPatterns.Query()
                .Include(g => g.ContentLevelMappings!)
                    .ThenInclude(m => m.CertificateLevel)
                .Include(g => g.LessonGrammars!)
                    .ThenInclude(lg => lg.Lesson)
                        .ThenInclude(l => l.CertificateLevel)
                .Where(g => !g.IsDeleted)
                .ToListAsync(cancellationToken);

            var (existingByTitle, ambiguousTitles) = ImportHandlerSupport.IndexByKey(existingPatterns, g => g.Title);

            var (levelsByCode, ambiguousLevelCodes) = await ImportHandlerSupport.LoadLevelsAsync(unitOfWork, cancellationToken);
            var openRowKeys = await ImportHandlerSupport.LoadOpenRowKeysAsync(unitOfWork, EntityType, cancellationToken);

            var result = new SheetImportResponse { EntityType = EntityType, SheetName = SheetName, TotalRows = rows.Count };
            var seenInFile = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                var diff = BuildRowDiff(row, existingByTitle, ambiguousTitles, levelsByCode, ambiguousLevelCodes, openRowKeys, seenInFile);
                result.Rows.Add(diff);
                ImportHandlerSupport.Tally(result, diff);
            }

            return result;
        }

        private static ImportRowDiffResponse BuildRowDiff(
            ExcelRow row,
            Dictionary<string, GrammarPattern> existingByTitle,
            HashSet<string> ambiguousTitles,
            Dictionary<string, int> levelsByCode,
            HashSet<string> ambiguousLevelCodes,
            HashSet<string> openRowKeys,
            HashSet<string> seenInFile)
        {
            var title = row.Get("Title");
            var structure = row.Get("Structure");
            var usageNotes = row.Get("UsageNotes");
            var exampleSentence = row.Get("Example Sentence");
            var exampleSentenceMeaning = row.Get("Example Sentence Meaning");
            var levelCode = row.Get("Certificate Level Code");
            var lessonTitle = row.Get("Lesson Title");

            var diff = new ImportRowDiffResponse
            {
                SourceRowNumber = row.RowNumber,
                RowKey = title ?? $"(dòng {row.RowNumber})",
                ParsedFields = new Dictionary<string, string?>
                {
                    ["title"] = title,
                    ["structure"] = structure,
                    ["usageNotes"] = usageNotes,
                    ["exampleSentence"] = exampleSentence,
                    ["exampleSentenceMeaning"] = exampleSentenceMeaning,
                    ["certificateLevelCode"] = levelCode,
                    ["lessonTitle"] = lessonTitle,
                },
            };

            // ---- Validate ----
            if (string.IsNullOrWhiteSpace(title)) diff.Errors.Add("Thiếu cột Title.");
            if (string.IsNullOrWhiteSpace(structure)) diff.Errors.Add("Thiếu cột Structure.");
            ImportHandlerSupport.CheckLevel(levelCode, levelsByCode, ambiguousLevelCodes, diff.Errors);

            // ---- Kiểm tra "đã có chưa" ngoài dữ liệu của chính dòng ----
            if (!string.IsNullOrWhiteSpace(title))
            {
                if (!seenInFile.Add(title))
                    diff.Errors.Add($"Title '{title}' bị lặp lại ở nhiều dòng trong cùng file.");
                if (ambiguousTitles.Contains(title))
                    diff.Errors.Add($"Có nhiều mẫu ngữ pháp '{title}' trùng nhau trong hệ thống, cần dọn dữ liệu trước khi import.");
                if (openRowKeys.Contains(title))
                    diff.Errors.Add($"Mẫu ngữ pháp '{title}' đang có bản nháp chờ Admin duyệt, chưa thể import lại.");
            }

            if (diff.Errors.Count > 0)
            {
                diff.Action = ImportRowAction.Error;
                return diff;
            }

            // ---- Build payload (đã qua validate) ----
            var payload = new GrammarImportPayloadResponse
            {
                Title = title!,
                Structure = structure!,
                UsageNotes = usageNotes,
                ExampleSentence = exampleSentence,
                ExampleSentenceMeaning = exampleSentenceMeaning,
                CertificateLevelCode = levelCode!,
                CertificateLevelId = levelsByCode[levelCode!],
                LessonTitle = lessonTitle,
            };

            // ---- Diff với DB ----
            if (!existingByTitle.TryGetValue(title!, out var existing))
            {
                diff.Action = ImportRowAction.New;
                diff.PayloadJson = JsonSerializer.Serialize(payload);
                return diff;
            }

            diff.TargetEntityId = existing.Id;
            var changed = new Dictionary<string, string>();

            ImportHandlerSupport.CompareField(changed, "structure", existing.Structure, payload.Structure);
            ImportHandlerSupport.CompareField(changed, "usageNotes", existing.UsageNotes, payload.UsageNotes);
            ImportHandlerSupport.CompareField(changed, "exampleSentence", existing.ExampleSentence, payload.ExampleSentence);
            ImportHandlerSupport.CompareField(changed, "exampleSentenceMeaning", existing.ExampleSentenceMeaning, payload.ExampleSentenceMeaning);

            // Lấy mã trình độ hiện tại từ danh sách ContentLevelMappings
            var existingLevelCode = existing.ContentLevelMappings?
                .Where(m => !m.IsDeleted && m.CertificateLevel != null)
                .Select(m => m.CertificateLevel.Code)
                .FirstOrDefault();

            ImportHandlerSupport.CompareField(changed, "certificateLevelCode", existingLevelCode, payload.CertificateLevelCode);

            ImportHandlerSupport.CompareLesson(changed, lessonTitle, payload.CertificateLevelId, payload.CertificateLevelCode,
                existing.LessonGrammars?.Select(lg => lg.Lesson));

            if (changed.Count == 0)
            {
                diff.Action = ImportRowAction.Unchanged;
                return diff;
            }

            diff.Action = ImportRowAction.Modified;
            diff.ChangedFields = changed;
            diff.PayloadJson = JsonSerializer.Serialize(payload);
            return diff;
        }
    }
}