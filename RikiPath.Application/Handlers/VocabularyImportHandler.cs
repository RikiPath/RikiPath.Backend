using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Handlers
{
    /// <summary>Khóa tự nhiên: Word + Reading (cùng chữ nhưng khác cách đọc là 2 từ khác nhau).</summary>
    public sealed class VocabularyImportHandler(IUnitOfWork unitOfWork) : ISheetImportHandler
    {
        public ImportEntityType EntityType => ImportEntityType.Vocabulary;
        public string SheetName => "Vocabulary";

        public async Task<SheetImportResponse> BuildDiffAsync(IReadOnlyList<ExcelRow> rows, CancellationToken cancellationToken)
        {
            // Nạp dữ liệu cần so khớp 1 lần duy nhất, tránh N+1 query theo từng dòng Excel.
            var existingVocabularies = await unitOfWork.Vocabularies.Query()
                .Include(v => v.CertificateLevel)
                .Include(v => v.LessonVocabularies).ThenInclude(lv => lv.Lesson).ThenInclude(l => l.CertificateLevel)
                .Where(v => !v.IsDeleted)
                .ToListAsync(cancellationToken);
            var (existingByKey, ambiguousKeys) = ImportHandlerSupport.IndexByKey(
                existingVocabularies, v => NaturalKey(v.Word, v.Reading));

            var (levelsByCode, ambiguousLevelCodes) = await ImportHandlerSupport.LoadLevelsAsync(unitOfWork, cancellationToken);
            var openRowKeys = await ImportHandlerSupport.LoadOpenRowKeysAsync(unitOfWork, EntityType, cancellationToken);

            var result = new SheetImportResponse { EntityType = EntityType, SheetName = SheetName, TotalRows = rows.Count };
            var seenInFile = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                var diff = BuildRowDiff(row, existingByKey, ambiguousKeys, levelsByCode, ambiguousLevelCodes, openRowKeys, seenInFile);
                result.Rows.Add(diff);
                ImportHandlerSupport.Tally(result, diff);
            }

            return result;
        }

        private static string NaturalKey(string word, string reading) => $"{word}|{reading}";
        private static string RowKeyOf(string word, string reading) => $"{word} / {reading}";

        private static ImportRowDiffResponse BuildRowDiff(
            ExcelRow row,
            Dictionary<string, Domain.Entities.Vocabulary> existingByKey,
            HashSet<string> ambiguousKeys,
            Dictionary<string, int> levelsByCode,
            HashSet<string> ambiguousLevelCodes,
            HashSet<string> openRowKeys,
            HashSet<string> seenInFile)
        {
            var word = row.Get("Word");
            var reading = row.Get("Reading");
            var meaning = row.Get("Meaning");
            var exampleSentence = row.Get("ExampleSentence");
            var exampleSentenceMeaning = row.Get("ExampleSentenceMeaning");
            var audioUrl = row.Get("AudioUrl");
            var levelCode = row.Get("CertificateLevelCode");
            var lessonTitle = row.Get("Lesson Title");

            var hasKey = !string.IsNullOrWhiteSpace(word) && !string.IsNullOrWhiteSpace(reading);
            var rowKey = hasKey ? RowKeyOf(word!, reading!) : word ?? $"(dòng {row.RowNumber})";

            var diff = new ImportRowDiffResponse
            {
                SourceRowNumber = row.RowNumber,
                RowKey = rowKey,
                ParsedFields = new Dictionary<string, string?>
                {
                    ["word"] = word,
                    ["reading"] = reading,
                    ["meaning"] = meaning,
                    ["exampleSentence"] = exampleSentence,
                    ["exampleSentenceMeaning"] = exampleSentenceMeaning,
                    ["audioUrl"] = audioUrl,
                    ["certificateLevelCode"] = levelCode,
                    ["lessonTitle"] = lessonTitle,
                },
            };

            // ---- Validate ----
            if (string.IsNullOrWhiteSpace(word)) diff.Errors.Add("Thiếu cột Word.");
            if (string.IsNullOrWhiteSpace(reading)) diff.Errors.Add("Thiếu cột Reading.");
            if (string.IsNullOrWhiteSpace(meaning)) diff.Errors.Add("Thiếu cột Meaning.");
            ImportHandlerSupport.CheckLevel(levelCode, levelsByCode, ambiguousLevelCodes, diff.Errors);

            // ---- Kiểm tra "đã có chưa" ngoài dữ liệu của chính dòng ----
            if (hasKey)
            {
                if (!seenInFile.Add(NaturalKey(word!, reading!)))
                    diff.Errors.Add($"Từ '{word}' ({reading}) bị lặp lại ở nhiều dòng trong cùng file.");
                if (ambiguousKeys.Contains(NaturalKey(word!, reading!)))
                    diff.Errors.Add($"Có nhiều từ '{word}' ({reading}) trùng nhau trong hệ thống, cần dọn dữ liệu trước khi import.");
                if (openRowKeys.Contains(rowKey))
                    diff.Errors.Add($"Từ '{word}' ({reading}) đang có bản nháp chờ Admin duyệt, chưa thể import lại.");
            }

            if (diff.Errors.Count > 0)
            {
                diff.Action = ImportRowAction.Error;
                return diff;
            }

            // ---- Build payload (đã qua validate) ----
            var payload = new VocabularyImportPayloadResponse
            {
                Word = word!,
                Reading = reading!,
                Meaning = meaning!,
                ExampleSentence = exampleSentence,
                ExampleSentenceMeaning = exampleSentenceMeaning,
                AudioUrl = audioUrl,
                CertificateLevelCode = levelCode!,
                CertificateLevelId = levelsByCode[levelCode!],
                LessonTitle = lessonTitle,
            };

            // ---- Diff với DB ----
            if (!existingByKey.TryGetValue(NaturalKey(word!, reading!), out var existing))
            {
                diff.Action = ImportRowAction.New;
                diff.PayloadJson = JsonSerializer.Serialize(payload);
                return diff;
            }

            diff.TargetEntityId = existing.Id;
            var changed = new Dictionary<string, string>();

            ImportHandlerSupport.CompareField(changed, "meaning", existing.Meaning, payload.Meaning);
            ImportHandlerSupport.CompareField(changed, "exampleSentence", existing.ExampleSentence, payload.ExampleSentence);
            ImportHandlerSupport.CompareField(changed, "exampleSentenceMeaning", existing.ExampleSentenceMeaning, payload.ExampleSentenceMeaning);
            // AudioUrl là cột tùy chọn: file không có cột này thì coi như không đổi.
            if (payload.AudioUrl is not null)
                ImportHandlerSupport.CompareField(changed, "audioUrl", existing.AudioUrl, payload.AudioUrl);
            ImportHandlerSupport.CompareField(changed, "certificateLevelCode", existing.CertificateLevel?.Code, payload.CertificateLevelCode);

            ImportHandlerSupport.CompareLesson(changed, lessonTitle, payload.CertificateLevelId, payload.CertificateLevelCode,
                existing.LessonVocabularies?.Select(lv => lv.Lesson));

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
