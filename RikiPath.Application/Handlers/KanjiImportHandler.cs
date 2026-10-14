using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Handlers
{
    public sealed class KanjiImportHandler(IUnitOfWork unitOfWork) : ISheetImportHandler
    {
        public ImportEntityType EntityType => ImportEntityType.Kanji;
        public string SheetName => "Kanji";

        public async Task<SheetImportResponse> BuildDiffAsync(
            IReadOnlyList<ExcelRow> rows, CancellationToken cancellationToken)
        {
            // Thay CertificateLevel bằng ContentLevelMappings
            var existingKanjis = await unitOfWork.Kanjis.Query()
                .Include(k => k.ContentLevelMappings!)
                    .ThenInclude(m => m.CertificateLevel)
                .Include(k => k.LessonKanjis!)
                    .ThenInclude(lk => lk.Lesson)
                        .ThenInclude(l => l.CertificateLevel)
                .Where(k => !k.IsDeleted)
                .ToListAsync(cancellationToken);

            var (existingByCharacter, ambiguousCharacters) = ImportHandlerSupport.IndexByKey(existingKanjis, k => k.Character);
            var (levelsByCode, ambiguousLevelCodes) = await ImportHandlerSupport.LoadLevelsAsync(unitOfWork, cancellationToken);
            var openRowKeys = await ImportHandlerSupport.LoadOpenRowKeysAsync(unitOfWork, EntityType, cancellationToken);

            var result = new SheetImportResponse { EntityType = EntityType, SheetName = SheetName, TotalRows = rows.Count };
            var seenInFile = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                var diff = BuildRowDiff(row, existingByCharacter, ambiguousCharacters, levelsByCode, ambiguousLevelCodes, openRowKeys, seenInFile);
                result.Rows.Add(diff);
                ImportHandlerSupport.Tally(result, diff);
            }

            return result;
        }

        private static ImportRowDiffResponse BuildRowDiff(
            ExcelRow row,
            Dictionary<string, Kanji> existingByCharacter,
            HashSet<string> ambiguousCharacters,
            Dictionary<string, int> levelsByCode,
            HashSet<string> ambiguousLevelCodes,
            HashSet<string> openRowKeys,
            HashSet<string> seenInFile)
        {
            var character = row.Get("Character");
            var meaning = row.Get("Meaning");
            var sinoVietnamese = row.Get("Sino Vietnamese");
            var onYomi = row.Get("On Yomi");
            var kunYomi = row.Get("Kun Yomi");
            var strokeCountRaw = row.Get("Stroke Count");
            var levelCode = row.Get("Certificate Level Code");
            var lessonTitle = row.Get("Lesson Title");

            var diff = new ImportRowDiffResponse
            {
                SourceRowNumber = row.RowNumber,
                RowKey = character ?? $"(dòng {row.RowNumber})",
                ParsedFields = new Dictionary<string, string?>
                {
                    ["character"] = character,
                    ["meaning"] = meaning,
                    ["sinoVietnamese"] = sinoVietnamese,
                    ["onYomi"] = onYomi,
                    ["kunYomi"] = kunYomi,
                    ["strokeCount"] = strokeCountRaw,
                    ["certificateLevelCode"] = levelCode,
                    ["lessonTitle"] = lessonTitle,
                },
            };

            // ---- Validate ----
            if (string.IsNullOrWhiteSpace(character))
                diff.Errors.Add("Thiếu cột Character.");
            if (string.IsNullOrWhiteSpace(meaning))
                diff.Errors.Add("Thiếu cột Meaning.");

            var isValidStrokeCount = int.TryParse(strokeCountRaw, out var strokeCount) && strokeCount > 0;
            if (!isValidStrokeCount)
                diff.Errors.Add("Stroke Count phải là số nguyên dương.");

            ImportHandlerSupport.CheckLevel(levelCode, levelsByCode, ambiguousLevelCodes, diff.Errors);

            if (!string.IsNullOrWhiteSpace(character))
            {
                if (!seenInFile.Add(character))
                    diff.Errors.Add($"Character '{character}' bị lặp lại ở nhiều dòng trong cùng file.");
                if (ambiguousCharacters.Contains(character))
                    diff.Errors.Add($"Có nhiều Kanji '{character}' trùng nhau trong hệ thống, cần dọn dữ liệu trước khi import.");
                if (openRowKeys.Contains(character))
                    diff.Errors.Add($"Kanji '{character}' đang có bản nháp chờ Admin duyệt, chưa thể import lại.");
            }

            if (diff.Errors.Count > 0)
            {
                diff.Action = ImportRowAction.Error;
                return diff;
            }

            // ---- Build payload ----
            var payload = new KanjiImportPayloadResponse
            {
                Character = character!,
                Meaning = meaning!,
                SinoVietnamese = sinoVietnamese,
                OnYomi = onYomi,
                KunYomi = kunYomi,
                StrokeCount = strokeCount,
                CertificateLevelCode = levelCode!,
                CertificateLevelId = levelsByCode[levelCode!],
                LessonTitle = lessonTitle,
            };

            // ---- Diff với DB ----
            if (!existingByCharacter.TryGetValue(character!, out var existing))
            {
                diff.Action = ImportRowAction.New;
                diff.PayloadJson = JsonSerializer.Serialize(payload);
                return diff;
            }

            diff.TargetEntityId = existing.Id;
            var changed = new Dictionary<string, string>();

            ImportHandlerSupport.CompareField(changed, "meaning", existing.Meaning, payload.Meaning);
            ImportHandlerSupport.CompareField(changed, "sinoVietnamese", existing.SinoVietnamese, payload.SinoVietnamese);
            ImportHandlerSupport.CompareField(changed, "onYomi", existing.OnYomi, payload.OnYomi);
            ImportHandlerSupport.CompareField(changed, "kunYomi", existing.KunYomi, payload.KunYomi);
            ImportHandlerSupport.CompareField(changed, "strokeCount", existing.StrokeCount.ToString(), payload.StrokeCount.ToString());

            // Lấy mã trình độ hiện tại từ danh sách ContentLevelMappings
            var existingLevelCode = existing.ContentLevelMappings?
                .Where(m => !m.IsDeleted && m.CertificateLevel != null)
                .Select(m => m.CertificateLevel.Code)
                .FirstOrDefault();

            ImportHandlerSupport.CompareField(changed, "certificateLevelCode", existingLevelCode, payload.CertificateLevelCode);

            ImportHandlerSupport.CompareLesson(changed, lessonTitle, payload.CertificateLevelId, payload.CertificateLevelCode,
                existing.LessonKanjis?.Select(lk => lk.Lesson));

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