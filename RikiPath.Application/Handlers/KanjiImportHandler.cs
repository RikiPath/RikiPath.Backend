using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;

namespace RikiPath.Application.Handlers
{
    public sealed class KanjiImportHandler(IUnitOfWork unitOfWork) : ISheetImportHandler
    {
        public ImportEntityType EntityType => ImportEntityType.Kanji;
        public string SheetName => "Kanji";

        public async Task<SheetImportResponse> BuildDiffAsync(
            IReadOnlyList<ExcelRow> rows, CancellationToken cancellationToken)
        {
            // Nạp toàn bộ dữ liệu cần so khớp 1 lần duy nhất, tránh N+1 query theo từng dòng Excel.
            // Chỉ lấy Kanji chưa xóa. Chữ nào trùng nhiều bản ghi trong DB thì đánh dấu "ambiguous" thay vì ném lỗi.
            var existingKanjis = await unitOfWork.Kanjis.Query()
                .Include(k => k.CertificateLevel)
                .Include(k => k.LessonKanjis).ThenInclude(lk => lk.Lesson).ThenInclude(l => l.CertificateLevel)
                .Where(k => !k.IsDeleted)
                .ToListAsync(cancellationToken);
            var (existingByCharacter, ambiguousCharacters) = ImportHandlerSupport.IndexByKey(existingKanjis, k => k.Character);

            var (levelsByCode, ambiguousLevelCodes) = await ImportHandlerSupport.LoadLevelsAsync(unitOfWork, cancellationToken);

            // Kanji đã import nhưng đang chờ Admin duyệt (chưa có trong bảng chính).
            var openRowKeys = await ImportHandlerSupport.LoadOpenRowKeysAsync(unitOfWork, EntityType, cancellationToken);

            var result = new SheetImportResponse { EntityType = EntityType, SheetName = SheetName, TotalRows = rows.Count };
            var seenInFile = new HashSet<string>();

            foreach (var row in rows)
            {
                var diff = BuildRowDiff(row, existingByCharacter, ambiguousCharacters, levelsByCode, ambiguousLevelCodes, openRowKeys, seenInFile);
                result.Rows.Add(diff);

                switch (diff.Action)
                {
                    case ImportRowAction.New: result.NewCount++; break;
                    case ImportRowAction.Modified: result.ModifiedCount++; break;
                    case ImportRowAction.Unchanged: result.UnchangedCount++; break;
                    case ImportRowAction.Error: result.ErrorCount++; break;
                }
            }

            return result;
        }

        private static ImportRowDiffResponse BuildRowDiff(
            ExcelRow row,
            Dictionary<string, Domain.Entities.Kanji> existingByCharacter,
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
            if (string.IsNullOrWhiteSpace(strokeCountRaw) || !int.TryParse(strokeCountRaw, out var strokeCount) || strokeCount <= 0)
                diff.Errors.Add("Stroke Count phải là số nguyên dương.");
            else
                strokeCount = int.Parse(strokeCountRaw!);

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

            // ---- Build payload (đã qua validate, mọi giá trị required chắc chắn có) ----
            var payload = new KanjiImportPayloadResponse
            {
                Character = character!,
                Meaning = meaning!,
                SinoVietnamese = sinoVietnamese,
                OnYomi = onYomi,
                KunYomi = kunYomi,
                StrokeCount = int.Parse(strokeCountRaw!),
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
            ImportHandlerSupport.CompareField(changed, "certificateLevelCode", existing.CertificateLevel?.Code, payload.CertificateLevelCode);

            // Excel ghi 1 bài học; nếu Kanji đang thuộc bài khác thì báo rõ "bài hiện tại → bài trong file" để Content Author tự kiểm tra.
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

