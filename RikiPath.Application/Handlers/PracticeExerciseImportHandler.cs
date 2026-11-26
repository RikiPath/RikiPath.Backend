using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Handlers
{
    /// <summary>
    /// Sheet "Practice Exercise" dạng phẳng (mỗi dòng 1 câu hỏi). Hỗ trợ cả 3 tầng:
    /// LessonQuickCheck (gắn Lesson), GroupQuiz (gắn Topic/Stage) và StageAssessment (gắn CertificateLevel).
    /// </summary>
    public sealed class PracticeExerciseImportHandler(IUnitOfWork unitOfWork, IClaimService? claims = null) : IWorkbookLessonAwareImportHandler
    {
        private static readonly string[] OptionHeaders = ["Option 1", "Option 2", "Option 3", "Option 4"];

        public ImportEntityType EntityType => ImportEntityType.PracticeExercise;
        public string SheetName => "Practice Exercise";

        public async Task<SheetImportResponse> BuildDiffAsync(IReadOnlyList<ExcelRow> rows, CancellationToken cancellationToken)
            => await BuildDiffAsync(rows, [], cancellationToken);

        public async Task<SheetImportResponse> BuildDiffAsync(
            IReadOnlyList<ExcelRow> rows,
            IReadOnlyList<LessonImportPayloadResponse> workbookLessons,
            CancellationToken cancellationToken)
        {
            var titles = rows.Select(r => ImportHandlerSupport.Clean(r.Get("Exercise Title")))
                .Where(t => t is not null).Select(t => t!).Distinct().ToList();

            var existingExercises = await unitOfWork.PracticeExercises.Query()
                .Include(e => e.Questions).ThenInclude(q => q.Options)
                .Where(e => !e.IsDeleted && titles.Contains(e.Title))
                .ToListAsync(cancellationToken);

            var (existingByKey, ambiguousKeys) = ImportHandlerSupport.IndexByKey(
                existingExercises,
                e => ExistingKey(e.Scope, e.LessonId, e.TopicId, e.CertificateLevelId ?? 0, e.Title));

            var (levelsByCode, _) = await ImportHandlerSupport.LoadLevelsAsync(unitOfWork, cancellationToken);
            var skills = await unitOfWork.LanguageSkills.Query().Where(s => !s.IsDeleted).ToListAsync(cancellationToken);
            var (skillsByName, ambiguousSkillNames) = ImportHandlerSupport.IndexByKey(
                skills, s => s.Name, StringComparer.OrdinalIgnoreCase);

            var lessonsByTitle = await LoadLessonCandidatesAsync(workbookLessons, cancellationToken);
            var topicsByTitle = await LoadTopicCandidatesAsync(workbookLessons, cancellationToken);
            var openRowKeys = await ImportHandlerSupport.LoadOpenRowKeysAsync(unitOfWork, EntityType, cancellationToken);

            var ctx = new DiffContext(
                existingByKey,
                ambiguousKeys,
                levelsByCode,
                skillsByName,
                ambiguousSkillNames,
                lessonsByTitle,
                topicsByTitle,
                openRowKeys);

            var groups = new List<List<ExcelRow>>();
            var byCompositeKey = new Dictionary<string, List<ExcelRow>>(StringComparer.Ordinal);
            var lastTargetByTitle = new Dictionary<string, (string? Lesson, string? Topic, string? Level, string? Scope)>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                var title = ImportHandlerSupport.Clean(row.Get("Exercise Title"));
                if (title is null) { groups.Add([row]); continue; }

                var lessonTitle = ImportHandlerSupport.Clean(row.Get("Lesson Title"));
                var topicTitle = ImportHandlerSupport.Clean(row.Get("Topic Title") ?? row.Get("Chapter Title"));
                var levelCode = ImportHandlerSupport.Clean(row.Get("Certificate Level Code"));
                var scope = ImportHandlerSupport.Clean(row.Get("Exercise Scope"));

                if (lessonTitle is not null || topicTitle is not null || levelCode is not null || scope is not null)
                    lastTargetByTitle[title] = (lessonTitle, topicTitle, levelCode, scope);
                else if (lastTargetByTitle.TryGetValue(title, out var prev))
                    (lessonTitle, topicTitle, levelCode, scope) = prev;

                var groupKey = $"{title}\u001f{PracticeExerciseScopes.Normalize(scope)}\u001f{lessonTitle}\u001f{topicTitle}\u001f{levelCode}";
                if (!byCompositeKey.TryGetValue(groupKey, out var list))
                {
                    byCompositeKey[groupKey] = list = [];
                    groups.Add(list);
                }
                list.Add(row);
            }

            var result = new SheetImportResponse { EntityType = EntityType, SheetName = SheetName, TotalRows = rows.Count };
            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var diff = BuildGroupDiff(group, ctx);
                result.Rows.Add(diff);
                ImportHandlerSupport.Tally(result, diff);
            }
            return result;
        }

        private static string ExistingKey(string scope, int? lessonId, int? topicId, int levelId, string title) =>
            PracticeExerciseScopes.Normalize(scope) switch
            {
                PracticeExerciseScopes.GroupQuiz => $"G|{topicId ?? 0}|{title}",
                PracticeExerciseScopes.StageAssessment => $"S|{levelId}|{title}",
                _ => $"L|{lessonId ?? 0}|{title}",
            };

        private sealed record LessonRef(int? Id, string Title, int CertificateLevelId, string CertificateLevelCode);
        private sealed record TopicRef(int? Id, string Title, int CertificateLevelId, string CertificateLevelCode);

        private sealed record DiffContext(
            Dictionary<string, Domain.Entities.PracticeExercise> ExistingByKey,
            HashSet<string> AmbiguousKeys,
            Dictionary<string, int> LevelsByCode,
            Dictionary<string, Domain.Entities.LanguageSkill> SkillsByName,
            HashSet<string> AmbiguousSkillNames,
            Dictionary<string, List<LessonRef>> LessonsByTitle,
            Dictionary<string, List<TopicRef>> TopicsByTitle,
            HashSet<string> OpenRowKeys);

        private async Task<Dictionary<string, List<LessonRef>>> LoadLessonCandidatesAsync(
            IReadOnlyList<LessonImportPayloadResponse> workbookLessons,
            CancellationToken cancellationToken)
        {
            var dbLessons = await unitOfWork.Lessons.Query()
                .Where(l => !l.IsDeleted && !l.CertificateLevel.IsDeleted
                    && l.CertificateLevel.CertificateType.IsActive && !l.CertificateLevel.CertificateType.IsDeleted)
                .Select(l => new { l.Id, l.Title, l.CertificateLevelId, Code = l.CertificateLevel.Code })
                .ToListAsync(cancellationToken);
            var candidates = dbLessons.Select(l => new LessonRef(l.Id, l.Title, l.CertificateLevelId, l.Code)).ToList();

            var authorId = claims?.GetUserClaim().Id;
            var pendingPayloads = await unitOfWork.ContentApprovalRequests.Query()
                .Where(x => !x.IsDeleted
                    && x.SubmittedByUserId == authorId
                    && x.EntityType == ImportEntityType.Lesson
                    && (x.Status == ContentApprovalStatus.Draft || x.Status == ContentApprovalStatus.PendingReview))
                .Select(x => x.PayloadJson)
                .ToListAsync(cancellationToken);

            foreach (var json in pendingPayloads)
            {
                LessonImportPayloadResponse? p;
                try { p = JsonSerializer.Deserialize<LessonImportPayloadResponse>(json); }
                catch (JsonException) { continue; }
                if (p is null || string.IsNullOrWhiteSpace(p.Title) || p.CertificateLevelId <= 0) continue;
                if (candidates.Any(c => c.Title == p.Title && c.CertificateLevelId == p.CertificateLevelId)) continue;
                candidates.Add(new LessonRef(null, p.Title, p.CertificateLevelId, p.CertificateLevelCode));
            }

            foreach (var lesson in workbookLessons)
            {
                if (string.IsNullOrWhiteSpace(lesson.Title) || lesson.CertificateLevelId <= 0) continue;
                if (candidates.Any(candidate => candidate.Title == lesson.Title
                    && candidate.CertificateLevelId == lesson.CertificateLevelId)) continue;
                candidates.Add(new LessonRef(null, lesson.Title, lesson.CertificateLevelId, lesson.CertificateLevelCode));
            }

            return candidates.GroupBy(c => c.Title).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        }

        private async Task<Dictionary<string, List<TopicRef>>> LoadTopicCandidatesAsync(
            IReadOnlyList<LessonImportPayloadResponse> workbookLessons,
            CancellationToken cancellationToken)
        {
            var dbTopics = await unitOfWork.Topics.Query()
                .Include(t => t.CertificateLevel)
                .Where(t => !t.IsDeleted)
                .ToListAsync(cancellationToken);

            var candidates = dbTopics
                .Select(t => new TopicRef(
                    t.Id,
                    t.Title,
                    t.CertificateLevelId ?? 0,
                    t.CertificateLevel?.Code ?? string.Empty))
                .ToList();

            foreach (var lesson in workbookLessons)
            {
                if (string.IsNullOrWhiteSpace(lesson.TopicTitle) || lesson.CertificateLevelId <= 0) continue;
                var tt = lesson.TopicTitle.Trim();
                if (candidates.Any(c => c.Title == tt && c.CertificateLevelId == lesson.CertificateLevelId)) continue;
                candidates.Add(new TopicRef(null, tt, lesson.CertificateLevelId, lesson.CertificateLevelCode));
            }

            return candidates.GroupBy(t => t.Title).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        }

        private static ImportRowDiffResponse BuildGroupDiff(List<ExcelRow> group, DiffContext ctx)
        {
            var first = group[0];
            var errors = new List<string>();
            errors.AddRange(group.SelectMany(row => row.MediaImportErrors.Select(error => $"Dòng {row.RowNumber}: {error}")));
            var exerciseImage = ImportHandlerSupport.CheckUrl(ImportHandlerSupport.Consistent(group, "ExerciseImageUrl", errors), "ExerciseImageUrl", "", errors);
            static string P(ExcelRow r) => $"Dòng {r.RowNumber}: ";

            var title = ImportHandlerSupport.Clean(first.Get("Exercise Title"));
            if (title is null) errors.Add($"{P(first)}Thiếu cột Exercise Title.");

            var description = ImportHandlerSupport.Consistent(group, "Exercise Description", errors);
            var scopeRaw = ImportHandlerSupport.Consistent(group, "Exercise Scope", errors);
            var scope = PracticeExerciseScopes.Normalize(scopeRaw);
            var lessonTitle = ImportHandlerSupport.Consistent(group, "Lesson Title", errors);
            var topicTitle = ImportHandlerSupport.Consistent(group, "Topic Title",
                r => r.Get("Topic Title") ?? r.Get("Chapter Title"), errors);
            var levelCode = ImportHandlerSupport.Consistent(group, "Certificate Level Code", errors);
            var skillName = ImportHandlerSupport.Consistent(group, "Language Skill Code",
                r => r.Get("Language Skill Code") ?? r.Get("Language Skill Name"), errors);
            var sortRaw = ImportHandlerSupport.Consistent(group, "Exercise Sort Order", errors);
            var passRaw = ImportHandlerSupport.Consistent(group, "Passing Score Percent", errors);

            LessonRef? lesson = null;
            TopicRef? topic = null;
            var resolvedLevelId = 0;
            string? resolvedLevelCode = levelCode;

            if (scope == PracticeExerciseScopes.LessonQuickCheck)
            {
                if (lessonTitle is null)
                    errors.Add($"{P(first)}Thiếu cột Lesson Title.");
                else
                {
                    var candidates = ctx.LessonsByTitle.GetValueOrDefault(lessonTitle) ?? [];
                    if (levelCode is not null) candidates = candidates.Where(c => string.Equals(c.CertificateLevelCode, levelCode, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (candidates.Count == 0)
                        errors.Add($"{P(first)}Không tìm thấy Lesson '{lessonTitle}'. Hãy import Lesson trước hoặc kiểm tra lại tên bài học.");
                    else if (candidates.Count > 1)
                        errors.Add($"{P(first)}Có nhiều Lesson tên '{lessonTitle}' ở các level khác nhau, thêm cột 'Certificate Level Code' để chỉ rõ.");
                    else
                    {
                        lesson = candidates[0];
                        resolvedLevelId = lesson.CertificateLevelId;
                        resolvedLevelCode = lesson.CertificateLevelCode;
                    }
                }
            }
            else if (scope == PracticeExerciseScopes.GroupQuiz)
            {
                if (topicTitle is null)
                    errors.Add($"{P(first)}Bài kiểm tra GroupQuiz cần cột 'Topic Title'.");
                else
                {
                    var candidates = ctx.TopicsByTitle.GetValueOrDefault(topicTitle) ?? [];
                    if (levelCode is not null)
                    {
                        candidates = candidates.Where(c =>
                            string.IsNullOrEmpty(c.CertificateLevelCode) ||
                            string.Equals(c.CertificateLevelCode, levelCode, StringComparison.OrdinalIgnoreCase)).ToList();
                    }

                    if (candidates.Count == 0)
                        errors.Add($"{P(first)}Không tìm thấy Topic/Chặng '{topicTitle}' cho GroupQuiz.");
                    else if (candidates.Count > 1)
                        errors.Add($"{P(first)}Có nhiều Topic tên '{topicTitle}', hãy bổ sung 'Certificate Level Code'.");
                    else
                    {
                        topic = candidates[0];
                        if (topic.CertificateLevelId > 0)
                        {
                            resolvedLevelId = topic.CertificateLevelId;
                            resolvedLevelCode = topic.CertificateLevelCode;
                        }
                        else if (levelCode is not null && ctx.LevelsByCode.TryGetValue(levelCode, out var mappedLid))
                        {
                            resolvedLevelId = mappedLid;
                        }
                    }
                }
            }
            else // StageAssessment
            {
                if (levelCode is null || !ctx.LevelsByCode.TryGetValue(levelCode, out resolvedLevelId))
                    errors.Add($"{P(first)}StageAssessment cần cột 'Certificate Level Code' hợp lệ (N5–N2).");
            }

            Domain.Entities.LanguageSkill? skill = null;
            if (skillName is null)
                errors.Add($"{P(first)}Thiếu cột Language Skill Code.");
            else if (ctx.AmbiguousSkillNames.Contains(skillName))
                errors.Add($"{P(first)}Language Skill '{skillName}' bị trùng tên trong hệ thống.");
            else if (!ctx.SkillsByName.TryGetValue(skillName, out skill))
                errors.Add($"{P(first)}Language Skill '{skillName}' không tồn tại trong hệ thống.");

            int? exerciseSort = null;
            if (sortRaw is not null)
            {
                if (int.TryParse(sortRaw, out var parsedSort) && parsedSort >= 0) exerciseSort = parsedSort;
                else errors.Add($"{P(first)}Exercise Sort Order phải là số nguyên không âm.");
            }

            double passingScore = 70;
            if (passRaw is not null)
            {
                if (double.TryParse(passRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedPass) && parsedPass is >= 1 and <= 100)
                    passingScore = parsedPass;
                else
                    errors.Add($"{P(first)}Passing Score Percent phải từ 1 đến 100.");
            }

            var targetLabel = lessonTitle ?? topicTitle ?? resolvedLevelCode ?? string.Empty;
            var rowKey = title is not null && !string.IsNullOrEmpty(targetLabel) ? $"{title} / {targetLabel}" : title ?? $"(dòng {first.RowNumber})";
            if (title is not null && !string.IsNullOrEmpty(targetLabel) && ctx.OpenRowKeys.Contains(rowKey))
                errors.Add($"{P(first)}Bài luyện tập '{title}' ({targetLabel}) đang có bản nháp chờ Quản trị viên duyệt.");

            var diff = new ImportRowDiffResponse
            {
                SourceRowNumber = first.RowNumber,
                RowKey = rowKey,
                ParsedFields = new Dictionary<string, string?>
                {
                    ["exerciseTitle"] = title,
                    ["exerciseScope"] = scope,
                    ["exerciseDescription"] = description,
                    ["lessonTitle"] = lessonTitle,
                    ["topicTitle"] = topicTitle,
                    ["certificateLevelCode"] = resolvedLevelCode,
                    ["languageSkillCode"] = skillName,
                    ["exerciseSortOrder"] = sortRaw,
                    ["rows"] = $"{group.Min(r => r.RowNumber)}–{group.Max(r => r.RowNumber)} ({group.Count} dòng)",
                },
            };

            var questions = new List<PracticeQuestionPayloadResponse>();
            var seenSortOrders = new Dictionary<int, int>();

            foreach (var row in group)
            {
                var rp = P(row);
                var before = errors.Count;

                var qSortRaw = ImportHandlerSupport.Clean(row.Get("Question Sort Order"));
                var qSort = 0;
                if (qSortRaw is null || !ImportHandlerSupport.TryPositiveInt(qSortRaw, out qSort))
                    errors.Add($"{rp}Question Sort Order phải là số nguyên dương.");
                else if (seenSortOrders.TryGetValue(qSort, out var otherRow))
                    errors.Add($"{rp}Question Sort Order {qSort} trùng với dòng {otherRow}.");
                else
                    seenSortOrders[qSort] = row.RowNumber;

                var text = ImportHandlerSupport.Clean(row.Get("Question Text"));
                var imageUrl = ImportHandlerSupport.CheckUrl(row.Get("ImageUrl"), "ImageUrl", rp, errors);
                if (text is null && imageUrl is null) errors.Add($"{rp}Thiếu Question Text hoặc ImageUrl.");

                var options = ImportHandlerSupport.ParseOptions(row, OptionHeaders, "Correct Option Number", rp, errors, includeImages: true);

                if (errors.Count > before || (text is null && imageUrl is null) || options is null) continue;

                questions.Add(new PracticeQuestionPayloadResponse
                {
                    SortOrder = qSort,
                    QuestionText = text ?? string.Empty,
                    ImageUrl = imageUrl,
                    Explanation = ImportHandlerSupport.Clean(row.Get("Explanation")),
                    Options = options,
                });
            }

            if (errors.Count > 0)
            {
                diff.Errors.AddRange(errors);
                diff.Action = ImportRowAction.Error;
                return diff;
            }

            Domain.Entities.PracticeExercise? existing = null;
            var key = ExistingKey(scope, lesson?.Id, topic?.Id, resolvedLevelId, title!);
            if (ctx.AmbiguousKeys.Contains(key))
            {
                diff.Errors.Add($"Có nhiều bài tập '{title}' trùng nhau trong hệ thống.");
                diff.Action = ImportRowAction.Error;
                return diff;
            }
            ctx.ExistingByKey.TryGetValue(key, out existing);

            var payload = new PracticeExerciseImportPayloadResponse
            {
                ImageUrl = exerciseImage,
                Title = title!,
                Description = description,
                Scope = scope,
                PassingScorePercent = passingScore,
                SortOrder = exerciseSort ?? existing?.SortOrder ?? first.RowNumber,
                LessonTitle = lessonTitle ?? string.Empty,
                LessonId = lesson?.Id,
                TopicId = topic?.Id,
                TopicTitle = topic?.Title ?? topicTitle,
                CertificateLevelId = resolvedLevelId,
                CertificateLevelCode = resolvedLevelCode,
                LanguageSkillId = skill!.Id,
                LanguageSkillName = skill.Name,
                Questions = questions.OrderBy(q => q.SortOrder).ToList(),
            };

            if (existing is null)
            {
                diff.Action = ImportRowAction.New;
                diff.PayloadJson = JsonSerializer.Serialize(payload);
                return diff;
            }

            diff.TargetEntityId = existing.Id;
            var changed = new Dictionary<string, string>();
            if (payload.ImageUrl is not null) ImportHandlerSupport.CompareField(changed, "imageUrl", existing.ImageUrl, payload.ImageUrl);

            ImportHandlerSupport.CompareField(changed, "exerciseDescription", existing.Description, payload.Description);
            if (existing.LanguageSkillId != skill.Id)
                changed["languageSkill"] = $"{existing.LanguageSkillId} → {skill.Name}";
            ImportHandlerSupport.CompareField(changed, "exerciseSortOrder", existing.SortOrder.ToString(), payload.SortOrder.ToString());
            if (Math.Abs(existing.PassingScorePercent - passingScore) > 0.01)
                changed["passingScorePercent"] = $"{existing.PassingScorePercent} → {passingScore}";

            var oldQuestions = new Dictionary<int, string>();
            foreach (var q in (existing.Questions ?? []).Where(q => !q.IsDeleted))
            {
                var opts = (q.Options ?? []).Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).Select(o => (o.OptionText + "\u001e" + o.ImageUrl, o.IsCorrect));
                oldQuestions[q.SortOrder] = ImportHandlerSupport.QuestionSignature("", q.QuestionText, q.Explanation, null, q.ImageUrl, opts);
            }

            var newQuestions = payload.Questions.ToDictionary(
                q => q.SortOrder,
                q => ImportHandlerSupport.QuestionSignature("", q.QuestionText, q.Explanation, null, q.ImageUrl,
                    q.Options.Select(o => (o.OptionText + "\u001e" + o.ImageUrl, o.IsCorrect))));

            var questionSummary = ImportHandlerSupport.QuestionDiffSummary(oldQuestions, newQuestions);
            if (questionSummary is not null) changed["questions"] = questionSummary;

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
