using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Entities;
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
                .Include(l => l.Topic)
                .Include(l => l.PrerequisiteLesson)
                .Where(l => !l.IsDeleted)
                .ToListAsync(cancellationToken);
            var existingByKey = existingLessons.ToLookup(lesson => (lesson.Title, lesson.CertificateLevelId));

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
                var diff = BuildRowDiff(row, existingByKey, levelsByCode, ambiguousLevelCodes,
                    skillsByName, ambiguousSkillNames, openRowKeys, seenInFile);
                result.Rows.Add(diff);
                ImportHandlerSupport.Tally(result, diff);
            }

            return result;
        }

        private static ImportRowDiffResponse BuildRowDiff(
            ExcelRow row,
            ILookup<(string Title, int CertificateLevelId), Lesson> existingByKey,
            Dictionary<string, int> levelsByCode,
            HashSet<string> ambiguousLevelCodes,
            Dictionary<string, Domain.Entities.LanguageSkill> skillsByName,
            HashSet<string> ambiguousSkillNames,
            HashSet<string> openRowKeys,
            HashSet<string> seenInFile)
        {
            var title = row.Get("LessonTitle");
            var description = row.Get("Description");
            var audio = row.Get("AudioUrl");
            var levelCode = row.Get("Certificate Level Code");
            var skillName = row.Get("Language Skill Name");
            var sortOrderRaw = row.Get("Sort Order");
            var topicTitle = ImportHandlerSupport.Clean(row.Get("Topic Title") ?? row.Get("Chapter Title"));
            var stageCode = ImportHandlerSupport.Clean(row.Get("Stage Code"));
            var stageObjectives = ImportHandlerSupport.Clean(row.Get("Stage Objectives"));
            var stageSortRaw = ImportHandlerSupport.Clean(row.Get("Stage Sort Order"));
            var estimatedMinutesRaw = ImportHandlerSupport.Clean(row.Get("Estimated Minutes"));
            var learningObjectives = ImportHandlerSupport.Clean(row.Get("Learning Objectives"));
            var prerequisiteTitle = ImportHandlerSupport.Clean(row.Get("Prerequisite Lesson Title"));
            var sourceProvenance = ImportHandlerSupport.Clean(row.Get("Source Provenance"));

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
                    ["audioUrl"] = audio,
                    ["certificateLevelCode"] = levelCode,
                    ["languageSkillName"] = skillName,
                    ["sortOrder"] = sortOrderRaw,
                    ["topicTitle"] = topicTitle,
                    ["estimatedMinutes"] = estimatedMinutesRaw,
                },
            };

            diff.Errors.AddRange(row.MediaImportErrors);
            var imageUrl = ImportHandlerSupport.CheckUrl(row.Get("ImageUrl"), "ImageUrl", "", diff.Errors);

            if (string.IsNullOrWhiteSpace(title)) diff.Errors.Add("Thiếu cột LessonTitle.");
            var audioUrl = ImportHandlerSupport.CheckUrl(audio, "AudioUrl", "", diff.Errors);
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

            int? estimatedMinutes = null;
            if (!string.IsNullOrWhiteSpace(estimatedMinutesRaw))
            {
                if (int.TryParse(estimatedMinutesRaw, out var parsedMinutes) && parsedMinutes is >= 1 and <= 300)
                    estimatedMinutes = parsedMinutes;
                else
                    diff.Errors.Add("Estimated Minutes phải từ 1 đến 300.");
            }

            int? stageSortOrder = null;
            if (!string.IsNullOrWhiteSpace(stageSortRaw))
            {
                if (int.TryParse(stageSortRaw, out var parsedStageSort) && parsedStageSort >= 0)
                    stageSortOrder = parsedStageSort;
                else
                    diff.Errors.Add("Stage Sort Order phải là số nguyên không âm.");
            }

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
            var matches = existingByKey[(title!, levelId)];
            if (matches.Skip(1).Any())
            {
                var ids = string.Join(", ", matches.Select(lesson => lesson.Id).Order().Select(id => $"#{id}"));
                diff.Errors.Add($"Có nhiều bài học cùng tên '{title}' ở cấp {levelCode} trong hệ thống (ID {ids}). Hãy kiểm tra các bài trùng trước khi nhập dòng này.");
                diff.Action = ImportRowAction.Error;
                return diff;
            }

            var existing = matches.SingleOrDefault();

            var payload = new LessonImportPayloadResponse
            {
                ImageUrl = imageUrl,
                Title = title!,
                Description = description,
                AudioUrl = audioUrl,
                SortOrder = sortOrder ?? existing?.SortOrder ?? row.RowNumber,
                EstimatedMinutes = estimatedMinutes ?? existing?.EstimatedMinutes ?? 15,
                LearningObjectives = learningObjectives ?? existing?.LearningObjectives,
                PrerequisiteLessonTitle = prerequisiteTitle,
                SourceProvenance = sourceProvenance ?? existing?.SourceProvenance,
                CertificateLevelCode = levelCode!,
                CertificateLevelId = levelId,
                LanguageSkillId = skill!.Id,
                TopicId = existing?.TopicId,
                TopicTitle = topicTitle ?? existing?.Topic?.Title,
                StageCode = stageCode ?? existing?.Topic?.StageCode,
                StageSortOrder = stageSortOrder ?? existing?.Topic?.SortOrder,
                StageObjectives = stageObjectives ?? existing?.Topic?.LearningObjectives,
            };

            if (existing is null)
            {
                diff.Action = ImportRowAction.New;
                diff.PayloadJson = JsonSerializer.Serialize(payload);
                return diff;
            }

            diff.TargetEntityId = existing.Id;
            var changed = new Dictionary<string, string>();
            if (payload.ImageUrl is not null)
                ImportHandlerSupport.CompareField(changed, "imageUrl", existing.ImageUrl, payload.ImageUrl);

            ImportHandlerSupport.CompareField(changed, "description", existing.Description, payload.Description);
            if (audioUrl is not null)
                ImportHandlerSupport.CompareField(changed, "audioUrl", existing.AudioUrl, audioUrl);
            ImportHandlerSupport.CompareField(changed, "languageSkillName", existing.LanguageSkill?.Name, skill.Name);
            ImportHandlerSupport.CompareField(changed, "sortOrder", existing.SortOrder.ToString(), payload.SortOrder.ToString());
            if (topicTitle is not null)
                ImportHandlerSupport.CompareField(changed, "topicTitle", existing.Topic?.Title, topicTitle);
            if (estimatedMinutes.HasValue)
                ImportHandlerSupport.CompareField(changed, "estimatedMinutes", existing.EstimatedMinutes.ToString(), estimatedMinutes.Value.ToString());
            if (learningObjectives is not null)
                ImportHandlerSupport.CompareField(changed, "learningObjectives", existing.LearningObjectives, learningObjectives);
            if (prerequisiteTitle is not null)
                ImportHandlerSupport.CompareField(changed, "prerequisiteLesson", existing.PrerequisiteLesson?.Title, prerequisiteTitle);

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
