using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;
using System.Text.Json;

namespace RikiPath.Application.Handlers
{
    /// <summary>
    /// Sheet "Practice Exercise" dạng phẳng (mỗi dòng 1 câu hỏi). Handler GỘP các dòng cùng "Exercise Title" thành 1 bài tập
    /// (bài tập -> câu hỏi -> đáp án) nên 1 kết quả diff = 1 bài tập = 1 ContentApprovalRequest.
    ///
    /// Quy ước:
    ///  - "Exercise Title" phải khác nhau trong 1 file. Exercise Description / Lesson Title / Language Skill / Exercise Sort Order
    ///    có thể chỉ ghi ở dòng đầu; các dòng sau để trống hoặc phải trùng (lệch nhau là lỗi - thường do gõ nhầm tên bài học).
    ///  - Bài tập gắn với 1 Lesson (tra theo "Lesson Title"): Lesson phải đã có trong hệ thống HOẶC đang có bản nháp chờ duyệt.
    ///    Nếu trùng tên ở nhiều level thì thêm cột tùy chọn "Certificate Level Code" để phân biệt.
    ///  - Cột "Language Skill Code" thực chất chứa TÊN kỹ năng (khớp LanguageSkill.Name, không phân biệt hoa/thường).
    ///  - Số thứ tự câu ("Question Sort Order") không được trùng trong 1 bài tập.
    ///  - Khóa so khớp với DB: Lesson + Title.
    /// </summary>
    public sealed class PracticeExerciseImportHandler(IUnitOfWork unitOfWork) : ISheetImportHandler
    {
        private static readonly string[] OptionHeaders = ["Option 1", "Option 2", "Option 3", "Option 4"];

        public ImportEntityType EntityType => ImportEntityType.PracticeExercise;
        public string SheetName => "Practice Exercise";

        public async Task<SheetImportResponse> BuildDiffAsync(IReadOnlyList<ExcelRow> rows, CancellationToken cancellationToken)
        {
            var titles = rows.Select(r => ImportHandlerSupport.Clean(r.Get("Exercise Title")))
                .Where(t => t is not null).Select(t => t!).Distinct().ToList();

            // Bài tập đã có (chỉ những bài có tên xuất hiện trong file), kèm câu hỏi + đáp án để so sánh.
            var existingExercises = await unitOfWork.PracticeExercises.Query()
                .Include(e => e.Questions).ThenInclude(q => q.Options)
                .Where(e => !e.IsDeleted && titles.Contains(e.Title))
                .ToListAsync(cancellationToken);
            var (existingByKey, ambiguousKeys) = ImportHandlerSupport.IndexByKey(
                existingExercises, e => NaturalKey(e.LessonId, e.Title));

            var skills = await unitOfWork.LanguageSkills.Query().Where(s => !s.IsDeleted).ToListAsync(cancellationToken);
            var (skillsByName, ambiguousSkillNames) = ImportHandlerSupport.IndexByKey(
                skills, s => s.Name, StringComparer.OrdinalIgnoreCase);

            var lessonsByTitle = await LoadLessonCandidatesAsync(cancellationToken);
            var openRowKeys = await ImportHandlerSupport.LoadOpenRowKeysAsync(unitOfWork, EntityType, cancellationToken);

            var ctx = new DiffContext(existingByKey, ambiguousKeys, skillsByName, ambiguousSkillNames, lessonsByTitle, openRowKeys);

            // Gộp dòng theo Exercise Title (giữ thứ tự xuất hiện). Dòng thiếu tên bài tập -> 1 nhóm riêng để báo lỗi.
            var groups = new List<List<ExcelRow>>();
            var byTitle = new Dictionary<string, List<ExcelRow>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var title = ImportHandlerSupport.Clean(row.Get("Exercise Title"));
                if (title is null) { groups.Add([row]); continue; }
                if (!byTitle.TryGetValue(title, out var list)) { byTitle[title] = list = []; groups.Add(list); }
                list.Add(row);
            }

            // TotalRows = số dòng Excel; các bộ đếm New/Modified/... tính theo BÀI TẬP (mỗi bài gồm nhiều dòng).
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

        private static string NaturalKey(int lessonId, string title) => $"{lessonId}|{title}";

        private sealed record LessonRef(int? Id, string Title, int CertificateLevelId, string CertificateLevelCode);

        private sealed record DiffContext(
            Dictionary<string, Domain.Entities.PracticeExercise> ExistingByKey,
            HashSet<string> AmbiguousKeys,
            Dictionary<string, Domain.Entities.LanguageSkill> SkillsByName,
            HashSet<string> AmbiguousSkillNames,
            Dictionary<string, List<LessonRef>> LessonsByTitle,
            HashSet<string> OpenRowKeys);

        /// <summary>Lesson đã duyệt (có Id) + Lesson đang có bản nháp chờ duyệt (chưa có Id), nhóm theo tên.</summary>
        private async Task<Dictionary<string, List<LessonRef>>> LoadLessonCandidatesAsync(CancellationToken cancellationToken)
        {
            var dbLessons = await unitOfWork.Lessons.Query()
                .Where(l => !l.IsDeleted)
                .Select(l => new { l.Id, l.Title, l.CertificateLevelId, Code = l.CertificateLevel.Code })
                .ToListAsync(cancellationToken);
            var candidates = dbLessons.Select(l => new LessonRef(l.Id, l.Title, l.CertificateLevelId, l.Code)).ToList();

            var pendingPayloads = await unitOfWork.ContentApprovalRequests.Query()
                .Where(x => !x.IsDeleted
                    && x.EntityType == ImportEntityType.Lesson
                    && (x.Status == ContentApprovalStatus.Draft || x.Status == ContentApprovalStatus.PendingReview))
                .Select(x => x.PayloadJson)
                .ToListAsync(cancellationToken);

            foreach (var json in pendingPayloads)
            {
                LessonImportPayloadResponse? p;
                try { p = JsonSerializer.Deserialize<LessonImportPayloadResponse>(json); }
                catch (JsonException) { continue; }
                if (p is null || string.IsNullOrWhiteSpace(p.Title)) continue;
                if (candidates.Any(c => c.Title == p.Title && c.CertificateLevelId == p.CertificateLevelId)) continue;
                candidates.Add(new LessonRef(null, p.Title, p.CertificateLevelId, p.CertificateLevelCode));
            }

            return candidates.GroupBy(c => c.Title).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        }

        private static ImportRowDiffResponse BuildGroupDiff(List<ExcelRow> group, DiffContext ctx)
        {
            var first = group[0];
            var errors = new List<string>();
            static string P(ExcelRow r) => $"Dòng {r.RowNumber}: ";

            // ---- Thuộc tính cấp bài tập ----
            var title = ImportHandlerSupport.Clean(first.Get("Exercise Title"));
            if (title is null) errors.Add($"{P(first)}Thiếu cột Exercise Title.");

            var description = ImportHandlerSupport.Consistent(group, "Exercise Description", errors);
            var lessonTitle = ImportHandlerSupport.Consistent(group, "Lesson Title", errors);
            var levelCode = ImportHandlerSupport.Consistent(group, "Certificate Level Code", errors);
            var skillName = ImportHandlerSupport.Consistent(group, "Language Skill Code",
                r => r.Get("Language Skill Code") ?? r.Get("Language Skill Name"), errors);
            var sortRaw = ImportHandlerSupport.Consistent(group, "Exercise Sort Order", errors);

            // Lesson
            LessonRef? lesson = null;
            if (lessonTitle is null)
                errors.Add($"{P(first)}Thiếu cột Lesson Title.");
            else
            {
                var candidates = ctx.LessonsByTitle.GetValueOrDefault(lessonTitle) ?? [];
                if (levelCode is not null) candidates = candidates.Where(c => c.CertificateLevelCode == levelCode).ToList();

                if (candidates.Count == 0)
                    errors.Add($"{P(first)}Không tìm thấy Lesson '{lessonTitle}' (chưa có trong hệ thống và cũng chưa có bản nháp chờ duyệt). Hãy import Lesson trước hoặc kiểm tra lại tên bài học.");
                else if (candidates.Count > 1)
                    errors.Add($"{P(first)}Có nhiều Lesson tên '{lessonTitle}' ở các level khác nhau, thêm cột 'Certificate Level Code' để chỉ rõ.");
                else
                    lesson = candidates[0];
            }

            // Kỹ năng
            Domain.Entities.LanguageSkill? skill = null;
            if (skillName is null)
                errors.Add($"{P(first)}Thiếu cột Language Skill Code.");
            else if (ctx.AmbiguousSkillNames.Contains(skillName))
                errors.Add($"{P(first)}Language Skill '{skillName}' bị trùng tên trong hệ thống, không xác định được kỹ năng.");
            else if (!ctx.SkillsByName.TryGetValue(skillName, out skill))
                errors.Add($"{P(first)}Language Skill '{skillName}' không tồn tại trong hệ thống.");

            // Sort order của bài tập (tùy chọn)
            int? exerciseSort = null;
            if (sortRaw is not null)
            {
                if (int.TryParse(sortRaw, out var parsedSort) && parsedSort >= 0) exerciseSort = parsedSort;
                else errors.Add($"{P(first)}Exercise Sort Order phải là số nguyên không âm.");
            }

            var rowKey = title is not null && lessonTitle is not null ? $"{title} / {lessonTitle}" : title ?? $"(dòng {first.RowNumber})";
            if (title is not null && lessonTitle is not null && ctx.OpenRowKeys.Contains(rowKey))
                errors.Add($"{P(first)}Bài tập '{title}' của bài '{lessonTitle}' đang có bản nháp chờ Admin duyệt, chưa thể import lại.");

            var diff = new ImportRowDiffResponse
            {
                SourceRowNumber = first.RowNumber,
                RowKey = rowKey,
                ParsedFields = new Dictionary<string, string?>
                {
                    ["exerciseTitle"] = title,
                    ["exerciseDescription"] = description,
                    ["lessonTitle"] = lessonTitle,
                    ["certificateLevelCode"] = levelCode,
                    ["languageSkillCode"] = skillName,
                    ["exerciseSortOrder"] = sortRaw,
                    ["rows"] = $"{group.Min(r => r.RowNumber)}–{group.Max(r => r.RowNumber)} ({group.Count} dòng)",
                },
            };

            // ---- Câu hỏi ----
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
                if (text is null) errors.Add($"{rp}Thiếu cột Question Text.");

                var options = ImportHandlerSupport.ParseOptions(row, OptionHeaders, "Correct Option Number", rp, errors);

                if (errors.Count > before || text is null || options is null) continue;

                questions.Add(new PracticeQuestionPayloadResponse
                {
                    SortOrder = qSort,
                    QuestionText = text,
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

            // Tra bài tập đã có theo Lesson (chỉ Lesson đã duyệt mới có Id).
            Domain.Entities.PracticeExercise? existing = null;
            if (lesson!.Id is int lessonId)
            {
                var key = NaturalKey(lessonId, title!);
                if (ctx.AmbiguousKeys.Contains(key))
                {
                    diff.Errors.Add($"Có nhiều bài tập '{title}' trong bài '{lessonTitle}' trùng nhau trong hệ thống, cần dọn dữ liệu trước khi import.");
                    diff.Action = ImportRowAction.Error;
                    return diff;
                }
                ctx.ExistingByKey.TryGetValue(key, out existing);
            }

            var payload = new PracticeExerciseImportPayloadResponse
            {
                Title = title!,
                Description = description,
                // Không có cột Exercise Sort Order: giữ thứ tự cũ nếu bài đã có, bài mới lấy số dòng đầu của nhóm.
                SortOrder = exerciseSort ?? existing?.SortOrder ?? first.RowNumber,
                LessonTitle = lessonTitle!,
                LessonId = lesson.Id,
                CertificateLevelId = lesson.CertificateLevelId,
                LanguageSkillId = skill!.Id,
                LanguageSkillName = skill.Name,
                Questions = questions.OrderBy(q => q.SortOrder).ToList(),
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

            ImportHandlerSupport.CompareField(changed, "exerciseDescription", existing.Description, payload.Description);
            if (existing.LanguageSkillId != skill.Id)
                changed["languageSkill"] = $"{existing.LanguageSkillId} → {skill.Name}";
            ImportHandlerSupport.CompareField(changed, "exerciseSortOrder", existing.SortOrder.ToString(), payload.SortOrder.ToString());

            var oldQuestions = new Dictionary<int, string>();
            foreach (var q in (existing.Questions ?? []).Where(q => !q.IsDeleted))
            {
                var opts = (q.Options ?? []).Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).Select(o => (o.OptionText, o.IsCorrect));
                oldQuestions[q.SortOrder] = ImportHandlerSupport.QuestionSignature("", q.QuestionText, q.Explanation, null, null, opts);
            }

            var newQuestions = payload.Questions.ToDictionary(
                q => q.SortOrder,
                q => ImportHandlerSupport.QuestionSignature("", q.QuestionText, q.Explanation, null, null,
                    q.Options.Select(o => (o.OptionText, o.IsCorrect))));

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