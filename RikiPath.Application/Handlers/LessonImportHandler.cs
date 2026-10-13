using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Handlers
{
    /// <summary>Khóa tự nhiên: Title + CertificateLevelId (cùng tên bài ở 2 level khác nhau là 2 bài khác nhau).</summary>
    public sealed class LessonImportHandler(IUnitOfWork unitOfWork) : ISheetImportHandler
    {
        public ImportEntityType EntityType => ImportEntityType.Lesson;
        public string SheetName => "Lesson";

        public async Task<SheetImportResponse> BuildDiffAsync(IReadOnlyList<ExcelRow> rows, CancellationToken cancellationToken)
        {
            var existingLessons = await unitOfWork.Lessons.Query()
                .Include(l => l.CertificateLevel)
                .Include(l => l.LanguageSkill)
                .Where(l => !l.IsDeleted)
                .ToListAsync(cancellationToken);
            var (existingByKey, ambiguousKeys) = ImportHandlerSupport.IndexByKey(
                existingLessons, l => NaturalKey(l.Title, l.CertificateLevelId));

            var (levelsByCode, ambiguousLevelCodes) = await ImportHandlerSupport.LoadLevelsAsync(unitOfWork, cancellationToken);

            // Kỹ năng tra theo tên, không phân biệt hoa/thường (file ghi "VOCABULARY", DB có thể là "Vocabulary").
            var skills = await unitOfWork.LanguageSkills.Query()
                .Where(s => !s.IsDeleted)
                .ToListAsync(cancellationToken);
            var (skillsByName, ambiguousSkillNames) = ImportHandlerSupport.IndexByKey(
                skills, s => s.Name, StringComparer.OrdinalIgnoreCase);

            var openRowKeys = await ImportHandlerSupport.LoadOpenRowKeysAsync(unitOfWork, EntityType, cancellationToken);

            var result = new SheetImportResponse { EntityType = EntityType, SheetName = SheetName, TotalRows = rows.Count };
            var seenInFile = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                var diff = BuildRowDiff(row, existingByKey, ambiguousKeys, levelsByCode, ambiguousLevelCodes,
                    skillsByName, ambiguousSkillNames, openRowKeys, seenInFile);
                result.Rows.Add(diff);
                ImportHandlerSupport.Tally(result, diff);
            }

            return result;
        }

        private static string NaturalKey(string title, int certificateLevelId) => $"{title}|{certificateLevelId}";

        private static ImportRowDiffResponse BuildRowDiff(
            ExcelRow row,
            Dictionary<string, Domain.Entities.Lesson> existingByKey,
            HashSet<string> ambiguousKeys,
            Dictionary<string, int> levelsByCode,
            HashSet<string> ambiguousLevelCodes,
            Dictionary<string, Domain.Entities.LanguageSkill> skillsByName,
            HashSet<string> ambiguousSkillNames,
            HashSet<string> openRowKeys,
            HashSet<string> seenInFile)
        {
            var title = row.Get("LessonTitle");
            var description = row.Get("Description");
            var levelCode = row.Get("Certificate Level Code");
            var skillName = row.Get("Language Skill Name");
            var sortOrderRaw = row.Get("Sort Order");

            var hasKey = !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(levelCode);
            var rowKey = hasKey ? $"{title} / {levelCode}" : title ?? $"(dòng {row.RowNumber})";

            var diff = new ImportRowDiffResponse
            {
                SourceRowNumber = row.RowNumber,
                RowKey = rowKey,
                ParsedFields = new Dictionary<string, string?>
                {
                    ["lessonTitle"] = title,
                    ["description"] = description,
                    ["certificateLevelCode"] = levelCode,
                    ["languageSkillName"] = skillName,
                    ["sortOrder"] = sortOrderRaw,
                },
            };

            // ---- Validate ----
            if (string.IsNullOrWhiteSpace(title)) diff.Errors.Add("Thiếu cột LessonTitle.");
            ImportHandlerSupport.CheckLevel(levelCode, levelsByCode, ambiguousLevelCodes, diff.Errors);

            Domain.Entities.LanguageSkill? skill = null;
            if (string.IsNullOrWhiteSpace(skillName))
                diff.Errors.Add("Thiếu cột Language Skill Name.");
            else if (ambiguousSkillNames.Contains(skillName))
                diff.Errors.Add($"Language Skill '{skillName}' bị trùng tên trong hệ thống, không xác định được kỹ năng.");
            else if (!skillsByName.TryGetValue(skillName, out skill))
                diff.Errors.Add($"Language Skill '{skillName}' không tồn tại trong hệ thống.");

            int? sortOrder = null;
            if (!string.IsNullOrWhiteSpace(sortOrderRaw))
            {
                if (int.TryParse(sortOrderRaw, out var parsedSort) && parsedSort >= 0) sortOrder = parsedSort;
                else diff.Errors.Add("Sort Order phải là số nguyên không âm.");
            }

            // ---- Kiểm tra "đã có chưa" ngoài dữ liệu của chính dòng ----
            if (hasKey)
            {
                if (!seenInFile.Add(rowKey))
                    diff.Errors.Add($"Bài học '{title}' ({levelCode}) bị lặp lại ở nhiều dòng trong cùng file.");
                if (openRowKeys.Contains(rowKey))
                    diff.Errors.Add($"Bài học '{title}' ({levelCode}) đang có bản nháp chờ Admin duyệt, chưa thể import lại.");
            }

            if (diff.Errors.Count > 0)
            {
                diff.Action = ImportRowAction.Error;
                return diff;
            }

            var levelId = levelsByCode[levelCode!];
            var key = NaturalKey(title!, levelId);
            if (ambiguousKeys.Contains(key))
            {
                diff.Errors.Add($"Có nhiều bài học '{title}' ({levelCode}) trùng nhau trong hệ thống, cần dọn dữ liệu trước khi import.");
                diff.Action = ImportRowAction.Error;
                return diff;
            }

            existingByKey.TryGetValue(key, out var existing);

            // ---- Build payload (đã qua validate) ----
            var payload = new LessonImportPayloadResponse
            {
                Title = title!,
                Description = description,
                // Không có cột Sort Order: giữ thứ tự cũ nếu bài đã có, bài mới lấy số dòng trong file.
                SortOrder = sortOrder ?? existing?.SortOrder ?? row.RowNumber,
                CertificateLevelCode = levelCode!,
                CertificateLevelId = levelId,
                LanguageSkillId = skill!.Id,
            };

            // ---- Diff với DB ----
            if (existing is null)
            {
                diff.Action = ImportRowAction.New;
                diff.PayloadJson = JsonSerializer.Serialize(payload);
                return diff;
            }

            diff.TargetEntityId = existing.Id;
            var changed = new Dictionary<string, string>();

            ImportHandlerSupport.CompareField(changed, "description", existing.Description, payload.Description);
            ImportHandlerSupport.CompareField(changed, "languageSkillName", existing.LanguageSkill?.Name, skill.Name);
            ImportHandlerSupport.CompareField(changed, "sortOrder", existing.SortOrder.ToString(), payload.SortOrder.ToString());

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