using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;
using System.Text.Json;

namespace RikiPath.Application.Handlers
{
    public sealed class MockTestImportHandler(IUnitOfWork unitOfWork) : ISheetImportHandler
    {
        private static readonly string[] OptionHeaders = ["Option1", "Option2", "Option3", "Option4"];
        private static readonly HashSet<string> StructuredQuestionTypes = new(StringComparer.OrdinalIgnoreCase)
            { "Ordering", "FillBlank", "Matching", "ShortAnswer" };

        public ImportEntityType EntityType => ImportEntityType.MockTest;
        public string SheetName => "Mock Test";

        public async Task<SheetImportResponse> BuildDiffAsync(IReadOnlyList<ExcelRow> rows, CancellationToken cancellationToken)
        {
            // Chỉ nạp các đề có tên xuất hiện trong file (kèm cả cây phần/câu/đáp án) để so sánh, tránh nạp cả bảng.
            var titles = rows.Select(r => ImportHandlerSupport.Clean(r.Get("MockTestTitle")))
                .Where(t => t is not null).Select(t => t!).Distinct().ToList();

            var existingTests = await unitOfWork.MockTests.Query()
                .Include(t => t.CertificateLevel)
                .Include(t => t.Sections).ThenInclude(s => s.Questions).ThenInclude(q => q.Options)
                .Where(t => !t.IsDeleted && titles.Contains(t.Title))
                .ToListAsync(cancellationToken);
            var (existingByKey, ambiguousKeys) = ImportHandlerSupport.IndexByKey(
                existingTests, t => NaturalKey(t.Title, t.CertificateLevelId));

            var (levelsByCode, ambiguousLevelCodes) = await ImportHandlerSupport.LoadLevelsAsync(unitOfWork, cancellationToken);

            var skills = await unitOfWork.LanguageSkills.Query().Where(s => !s.IsDeleted).ToListAsync(cancellationToken);
            var (skillsByName, ambiguousSkillNames) = ImportHandlerSupport.IndexByKey(
                skills, s => s.Name, StringComparer.OrdinalIgnoreCase);

            // CertificateSection do Admin tạo sẵn; Excel chỉ mang SectionCode, FK được tra ở đây.
            var certSections = await unitOfWork.CertificateSections.Query().Where(s => !s.IsDeleted).ToListAsync(cancellationToken);
            var (certSectionsByCode, ambiguousSectionCodes) = ImportHandlerSupport.IndexByKey(
                certSections, s => s.Code, StringComparer.OrdinalIgnoreCase);

            var openRowKeys = await ImportHandlerSupport.LoadOpenRowKeysAsync(unitOfWork, EntityType, cancellationToken);

            var ctx = new DiffContext(existingByKey, ambiguousKeys, levelsByCode, ambiguousLevelCodes,
                skillsByName, ambiguousSkillNames, skills.ToDictionary(s => s.Id, s => s.Name),
                certSectionsByCode, ambiguousSectionCodes, certSections.ToDictionary(s => s.Id, s => s.Code),
                openRowKeys);

            // Gộp dòng theo MockTestTitle (giữ thứ tự xuất hiện). Dòng thiếu tên đề -> 1 nhóm riêng để báo lỗi.
            var groups = new List<List<ExcelRow>>();
            var byTitle = new Dictionary<string, List<ExcelRow>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var title = ImportHandlerSupport.Clean(row.Get("MockTestTitle"));
                if (title is null) { groups.Add([row]); continue; }
                if (!byTitle.TryGetValue(title, out var list)) { byTitle[title] = list = []; groups.Add(list); }
                list.Add(row);
            }

            // TotalRows = số dòng Excel; các bộ đếm New/Modified/... tính theo ĐỀ THI (mỗi đề gồm nhiều dòng).
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

        private static string NaturalKey(string title, int certificateLevelId) => $"{title}|{certificateLevelId}";

        private sealed record DiffContext(
            Dictionary<string, Domain.Entities.MockTest> ExistingByKey,
            HashSet<string> AmbiguousKeys,
            Dictionary<string, int> LevelsByCode,
            HashSet<string> AmbiguousLevelCodes,
            Dictionary<string, Domain.Entities.LanguageSkill> SkillsByName,
            HashSet<string> AmbiguousSkillNames,
            Dictionary<int, string> SkillNameById,
            Dictionary<string, Domain.Entities.CertificateSection> CertSectionsByCode,
            HashSet<string> AmbiguousSectionCodes,
            Dictionary<int, string> SectionCodeById,
            HashSet<string> OpenRowKeys);

        private sealed class SectionBuilder(string code, string title, int sortOrder, int firstRow)
        {
            public string Code { get; } = code;
            public string Title { get; } = title;
            public int SortOrder { get; } = sortOrder;
            public int FirstRow { get; } = firstRow;
            public List<MockQuestionPayloadResponse> Questions { get; } = [];
        }

        private static ImportRowDiffResponse BuildGroupDiff(List<ExcelRow> group, DiffContext ctx)
        {
            var first = group[0];
            var errors = new List<string>();
            static string P(ExcelRow r) => $"Dòng {r.RowNumber}: ";

            // ---- Thuộc tính cấp đề ----
            var title = ImportHandlerSupport.Clean(first.Get("MockTestTitle"));
            if (title is null) errors.Add($"{P(first)}Thiếu cột MockTestTitle.");

            var description = ImportHandlerSupport.Consistent(group, "MockTestDescription", errors);
            var levelCode = ImportHandlerSupport.Consistent(group, "CertificateLevelCode", errors);
            var timeRaw = ImportHandlerSupport.Consistent(group, "TimeLimitMinutes", errors);
            var placementRaw = ImportHandlerSupport.Consistent(group, "IsPlacementAssessment", errors);
            var isPlacement = false;
            if (placementRaw is not null && !bool.TryParse(placementRaw, out isPlacement))
                errors.Add($"{P(first)}IsPlacementAssessment chỉ nhận true hoặc false.");

            var levelErrors = new List<string>();
            ImportHandlerSupport.CheckLevel(levelCode, ctx.LevelsByCode, ctx.AmbiguousLevelCodes, levelErrors);
            errors.AddRange(levelErrors.Select(e => P(first) + e));

            var timeLimit = 0;
            if (timeRaw is null) errors.Add($"{P(first)}Thiếu cột TimeLimitMinutes.");
            else if (!int.TryParse(timeRaw, out timeLimit) || timeLimit <= 0)
                errors.Add($"{P(first)}TimeLimitMinutes phải là số nguyên dương.");

            var rowKey = title is not null && levelCode is not null ? $"{title} / {levelCode}" : title ?? $"(dòng {first.RowNumber})";
            if (title is not null && levelCode is not null && ctx.OpenRowKeys.Contains(rowKey))
                errors.Add($"{P(first)}Đề thi '{title}' ({levelCode}) đang có bản nháp chờ Admin duyệt, chưa thể import lại.");

            var diff = new ImportRowDiffResponse
            {
                SourceRowNumber = first.RowNumber,
                RowKey = rowKey,
                ParsedFields = new Dictionary<string, string?>
                {
                    ["mockTestTitle"] = title,
                    ["mockTestDescription"] = description,
                    ["timeLimitMinutes"] = timeRaw,
                    ["certificateLevelCode"] = levelCode,
                    ["isPlacementAssessment"] = isPlacement.ToString().ToLowerInvariant(),
                    ["rows"] = $"{group.Min(r => r.RowNumber)}–{group.Max(r => r.RowNumber)} ({group.Count} dòng)",
                    ["questionCount"] = group.Count(row => ImportHandlerSupport.Clean(row.Get("QuestionText")) is not null).ToString(),
                    ["questionTypes"] = string.Join(", ", group
                        .Select(row => ImportHandlerSupport.Clean(row.Get("QuestionType")) ?? "SingleChoice")
                        .GroupBy(type => type, StringComparer.OrdinalIgnoreCase)
                        .Select(types => $"{types.Key}: {types.Count()}")),
                    ["sections"] = string.Join(" | ", group.Select(r => ImportHandlerSupport.Clean(r.Get("SectionCode")))
                        .Where(c => c is not null).Distinct(StringComparer.OrdinalIgnoreCase)),
                },
            };

            // ---- Phần thi nhận diện bằng SectionCode (CertificateSection do Admin tạo sẵn) ----
            var certByCode = new Dictionary<string, Domain.Entities.CertificateSection?>(StringComparer.OrdinalIgnoreCase);
            var explicitSkills = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in group)
            {
                var code = ImportHandlerSupport.Clean(r.Get("SectionCode"));
                if (code is null) continue;

                if (!certByCode.ContainsKey(code))
                {
                    var rp0 = P(r);
                    if (ctx.AmbiguousSectionCodes.Contains(code))
                    {
                        errors.Add($"{rp0}SectionCode '{code}' bị trùng trong hệ thống, không xác định được phần thi.");
                        certByCode[code] = null;
                    }
                    else if (!ctx.CertSectionsByCode.TryGetValue(code, out var cs))
                    {
                        errors.Add($"{rp0}SectionCode '{code}' không tồn tại trong hệ thống (Admin cần tạo sẵn CertificateSection).");
                        certByCode[code] = null;
                    }
                    else certByCode[code] = cs;
                }

                // Kỹ năng khai báo tường minh theo từng phần (cột tùy chọn)
                var sk = ImportHandlerSupport.Clean(r.Get("Language Skill Name"));
                if (sk is null) continue;
                if (!explicitSkills.TryGetValue(code, out var set)) explicitSkills[code] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(sk);
            }

            // ---- Câu hỏi: validate từng dòng, gom vào phần ----
            var sections = new List<SectionBuilder>();
            var sectionByCode = new Dictionary<string, SectionBuilder>(StringComparer.OrdinalIgnoreCase);
            var seenSortOrders = new Dictionary<int, int>();

            foreach (var row in group)
            {
                var rp = P(row);
                var before = errors.Count;

                var sectionCode = ImportHandlerSupport.Clean(row.Get("SectionCode"));
                if (sectionCode is null)
                {
                    var sectionTitle = ImportHandlerSupport.Clean(row.Get("SectionTitle"));
                    errors.Add(sectionTitle is null
                        ? $"{rp}Thiếu SectionCode (mã CertificateSection)."
                        : $"{rp}Có SectionTitle '{sectionTitle}' nhưng thiếu SectionCode; thêm mã CertificateSection tương ứng để hệ thống gắn đúng phần thi.");
                }

                var sortRaw = ImportHandlerSupport.Clean(row.Get("QuestionSortOrder"));
                var sortOrder = 0;
                if (sortRaw is null || !ImportHandlerSupport.TryPositiveInt(sortRaw, out sortOrder))
                    errors.Add($"{rp}QuestionSortOrder phải là số nguyên dương.");
                else if (seenSortOrders.TryGetValue(sortOrder, out var otherRow))
                    errors.Add($"{rp}QuestionSortOrder {sortOrder} trùng với dòng {otherRow}.");
                else
                    seenSortOrders[sortOrder] = row.RowNumber;

                var text = ImportHandlerSupport.Clean(row.Get("QuestionText"));
                if (text is null) errors.Add($"{rp}Thiếu cột QuestionText.");

                var questionType = ImportHandlerSupport.Clean(row.Get("QuestionType")) ?? "SingleChoice";
                var supportedQuestionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "SingleChoice", "MultipleChoice", "Ordering", "FillBlank", "Matching", "ShortAnswer", "Listening", "Reading" };
                if (!supportedQuestionTypes.Contains(questionType))
                    errors.Add($"{rp}QuestionType '{questionType}' không hợp lệ. Dùng SingleChoice, MultipleChoice, Ordering, FillBlank, Matching hoặc ShortAnswer.");
                else
                    questionType = supportedQuestionTypes.First(type => type.Equals(questionType, StringComparison.OrdinalIgnoreCase));

                JsonElement? interactionData = ParseJsonCell(row.Get("InteractionDataJson"), "InteractionDataJson", rp, errors);
                JsonElement? answerKey = ParseJsonCell(row.Get("AnswerKeyJson"), "AnswerKeyJson", rp, errors);

                var audio = ImportHandlerSupport.CheckUrl(row.Get("AudioUrl"), "AudioUrl", rp, errors);
                var image = ImportHandlerSupport.CheckUrl(row.Get("ImageUrl"), "ImageUrl", rp, errors);
                var options = questionType.Equals("SingleChoice", StringComparison.OrdinalIgnoreCase)
                    || questionType.Equals("Listening", StringComparison.OrdinalIgnoreCase)
                    || questionType.Equals("Reading", StringComparison.OrdinalIgnoreCase)
                    ? ImportHandlerSupport.ParseOptions(row, OptionHeaders, "CorrectOptionNumber", rp, errors)
                    : questionType.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase)
                        ? ParseMultipleChoiceOptions(row, rp, errors)
                        : ParseStructuredQuestion(questionType, interactionData, answerKey, rp, errors);

                if (errors.Count > before || sectionCode is null || text is null || options is null) continue;

                if (certByCode.GetValueOrDefault(sectionCode) is not { } certSection) continue; // lỗi đã ghi ở bước trên

                if (!sectionByCode.TryGetValue(sectionCode, out var section))
                {
                    section = new SectionBuilder(certSection.Code, SectionTitleOf(certSection), sections.Count + 1, row.RowNumber);
                    sectionByCode[sectionCode] = section;
                    sections.Add(section);
                }
                section.Questions.Add(new MockQuestionPayloadResponse
                {
                    SortOrder = sortOrder,
                    QuestionText = text,
                    QuestionType = questionType,
                    InteractionData = interactionData,
                    AnswerKey = answerKey,
                    AudioUrl = audio,
                    ImageUrl = image,
                    Explanation = ImportHandlerSupport.Clean(row.Get("Explanation")),
                    Options = options,
                });
            }

            // ---- Kỹ năng + CertificateSection của từng phần ----
            var sectionPayloads = new List<MockTestSectionPayloadResponse>();
            foreach (var s in sections)
            {
                var skill = ResolveSkill(s, explicitSkills, ctx, errors);
                if (skill is null) continue;
                sectionPayloads.Add(new MockTestSectionPayloadResponse
                {
                    Title = s.Title,
                    SortOrder = s.SortOrder,
                    LanguageSkillId = skill.Id,
                    LanguageSkillName = skill.Name,
                    CertificateSectionCode = s.Code,
                    Questions = s.Questions.OrderBy(q => q.SortOrder).ToList(),
                });
            }

            if (errors.Count > 0)
            {
                diff.Errors.AddRange(errors);
                diff.Action = ImportRowAction.Error;
                return diff;
            }

            var levelId = ctx.LevelsByCode[levelCode!];
            var key = NaturalKey(title!, levelId);
            if (ctx.AmbiguousKeys.Contains(key))
            {
                diff.Errors.Add($"Có nhiều đề thi '{title}' ({levelCode}) trùng nhau trong hệ thống, cần dọn dữ liệu trước khi import.");
                diff.Action = ImportRowAction.Error;
                return diff;
            }

            var payload = new MockTestImportPayloadResponse
            {
                Title = title!,
                Description = description,
                TimeLimitMinutes = timeLimit,
                IsPlacementAssessment = isPlacement,
                CertificateLevelCode = levelCode!,
                CertificateLevelId = levelId,
                Sections = sectionPayloads,
            };

            // ---- Diff với DB ----
            if (!ctx.ExistingByKey.TryGetValue(key, out var existing))
            {
                diff.Action = ImportRowAction.New;
                diff.PayloadJson = JsonSerializer.Serialize(payload);
                return diff;
            }

            diff.TargetEntityId = existing.Id;
            var changed = new Dictionary<string, string>();

            ImportHandlerSupport.CompareField(changed, "description", existing.Description, payload.Description);
            ImportHandlerSupport.CompareField(changed, "timeLimitMinutes", existing.TimeLimitMinutes.ToString(), payload.TimeLimitMinutes.ToString());
            ImportHandlerSupport.CompareField(changed, "isPlacementAssessment", existing.IsPlacementAssessment.ToString(), payload.IsPlacementAssessment.ToString());

            var oldSections = existing.Sections?.Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).ToList()
                              ?? new List<Domain.Entities.MockTestSection>();
            ImportHandlerSupport.CompareField(changed, "sections",
                string.Join(" | ", oldSections.Select(s =>
                    $"{s.Title} [{ctx.SkillNameById.GetValueOrDefault(s.LanguageSkillId, s.LanguageSkillId.ToString())}" +
                    $"/{ctx.SectionCodeById.GetValueOrDefault(s.CertificateSectionId, s.CertificateSectionId.ToString())}]")),
                string.Join(" | ", payload.Sections.Select(s => $"{s.Title} [{s.LanguageSkillName}/{s.CertificateSectionCode}]")));

            var oldQuestions = new Dictionary<int, string>();
            foreach (var s in oldSections)
            {
                if (s.Questions is null) continue;
                foreach (var q in s.Questions.Where(q => !q.IsDeleted))
                {
                    var opts = (q.Options ?? []).Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).Select(o => (o.OptionText, o.IsCorrect));
                    oldQuestions[q.SortOrder] = ImportHandlerSupport.QuestionSignature(
                        s.Title, $"{q.QuestionType}|{q.InteractionDataJson}|{q.AnswerKeyJson}|{q.QuestionText}", q.Explanation, q.AudioUrl, q.ImageUrl, opts);
                }
            }

            var newQuestions = new Dictionary<int, string>();
            foreach (var s in payload.Sections)
                foreach (var q in s.Questions)
                    newQuestions[q.SortOrder] = ImportHandlerSupport.QuestionSignature(
                        s.Title, $"{q.QuestionType}|{q.InteractionData?.GetRawText()}|{q.AnswerKey?.GetRawText()}|{q.QuestionText}", q.Explanation, q.AudioUrl, q.ImageUrl,
                        q.Options.Select(o => (o.OptionText, o.IsCorrect)));

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

        /// <summary>Tên hiển thị của phần thi, lấy từ CertificateSection do Admin tạo sẵn (đổi property nếu entity đặt tên khác).</summary>
        private static string SectionTitleOf(Domain.Entities.CertificateSection certSection) => certSection.Name;

        private static JsonElement? ParseJsonCell(string? raw, string header, string rowPrefix, List<string> errors)
        {
            var value = ImportHandlerSupport.Clean(raw);
            if (value is null) return null;
            try
            {
                using var document = JsonDocument.Parse(value);
                var element = document.RootElement;
                if (element.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.String))
                {
                    errors.Add($"{rowPrefix}{header} phải là object, array hoặc string JSON.");
                    return null;
                }
                return element.Clone();
            }
            catch (JsonException)
            {
                errors.Add($"{rowPrefix}{header} phải là JSON hợp lệ.");
                return null;
            }
        }

        private static List<ImportOptionPayloadResponse>? ParseMultipleChoiceOptions(
            ExcelRow row, string rowPrefix, List<string> errors)
        {
            var initialErrorCount = errors.Count;
            var values = OptionHeaders.Select(header => ImportHandlerSupport.Clean(row.Get(header))).ToList();
            if (values.Count(value => value is not null) < 2)
                errors.Add($"{rowPrefix}MultipleChoice cần ít nhất 2 lựa chọn trong Option1..Option4.");
            var lastFilled = values.FindLastIndex(value => value is not null);
            if (lastFilled >= 0 && values.Take(lastFilled).Any(value => value is null))
                errors.Add($"{rowPrefix}Option1..Option4 không được để trống xen giữa các lựa chọn.");

            var correctRaw = ImportHandlerSupport.Clean(row.Get("CorrectOptionNumbers"));
            if (correctRaw is null)
                correctRaw = ImportHandlerSupport.Clean(row.Get("CorrectOptionNumber"));
            var correct = new HashSet<int>();
            if (correctRaw is null)
                errors.Add($"{rowPrefix}MultipleChoice cần CorrectOptionNumbers (ví dụ 1,3).");
            else
            {
                foreach (var token in correctRaw.Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!int.TryParse(token, out var index) || index < 1 || index > values.Count || values[index - 1] is null)
                        errors.Add($"{rowPrefix}CorrectOptionNumbers '{correctRaw}' chứa số không trỏ tới lựa chọn hợp lệ.");
                    else correct.Add(index);
                }
                if (correct.Count == 0) errors.Add($"{rowPrefix}Phải có ít nhất một lựa chọn đúng.");
            }

            if (errors.Count > initialErrorCount) return null;
            return values.Select((value, index) => (value, index))
                .Where(item => item.value is not null)
                .Select(item => new ImportOptionPayloadResponse
                {
                    SortOrder = item.index + 1,
                    OptionText = item.value!,
                    IsCorrect = correct.Contains(item.index + 1)
                }).ToList();
        }

        private static List<ImportOptionPayloadResponse>? ParseStructuredQuestion(
            string questionType, JsonElement? interactionData, JsonElement? answerKey,
            string rowPrefix, List<string> errors)
        {
            var initialErrorCount = errors.Count;
            if (interactionData is null)
                errors.Add($"{rowPrefix}{questionType} cần InteractionDataJson.");
            if (answerKey is null)
                errors.Add($"{rowPrefix}{questionType} cần AnswerKeyJson.");
            if (interactionData is { ValueKind: JsonValueKind.Object } interaction
                && !interaction.EnumerateObject().Any())
                errors.Add($"{rowPrefix}InteractionDataJson không được là object rỗng.");
            if (interactionData is { ValueKind: JsonValueKind.Array } dataArray && dataArray.GetArrayLength() == 0)
                errors.Add($"{rowPrefix}InteractionDataJson không được là array rỗng.");
            if (answerKey is { ValueKind: JsonValueKind.Object } keyObject
                && !keyObject.EnumerateObject().Any())
                errors.Add($"{rowPrefix}AnswerKeyJson không được là object rỗng.");
            if (answerKey is { ValueKind: JsonValueKind.Array } keyArray && keyArray.GetArrayLength() == 0)
                errors.Add($"{rowPrefix}AnswerKeyJson không được là array rỗng.");

            if (questionType.Equals("Ordering", StringComparison.OrdinalIgnoreCase)
                && interactionData is { ValueKind: JsonValueKind.Object } orderingData
                && orderingData.TryGetProperty("tokens", out var tokens))
            {
                if (tokens.ValueKind != JsonValueKind.Array || tokens.GetArrayLength() == 0)
                    errors.Add($"{rowPrefix}Ordering cần InteractionDataJson.tokens là array có phần tử.");
                else if (answerKey is null || answerKey.Value.ValueKind != JsonValueKind.Array)
                    errors.Add($"{rowPrefix}Ordering cần AnswerKeyJson là array đúng thứ tự.");
                else if (tokens.GetArrayLength() != answerKey.Value.GetArrayLength())
                    errors.Add($"{rowPrefix}Ordering: số token phải bằng số phần tử trong AnswerKeyJson.");
            }
            return errors.Count == initialErrorCount ? [] : null;
        }

        private static Domain.Entities.LanguageSkill? ResolveSkill(
            SectionBuilder section, Dictionary<string, HashSet<string>> explicitSkills, DiffContext ctx, List<string> errors)
        {
            var prefix = $"Dòng {section.FirstRow}: Phần '{section.Title}': ";
            explicitSkills.TryGetValue(section.Code, out var names);

            if (names is { Count: > 1 })
            {
                errors.Add($"{prefix}các dòng khai báo Language Skill Name khác nhau ({string.Join(", ", names)}).");
                return null;
            }

            var inferred = names is null || names.Count == 0;
            var name = inferred ? (InferSkillName(section.Title) ?? InferSkillName(section.Code)) : names!.First();
            if (name is null)
            {
                errors.Add($"{prefix}không xác định được kỹ năng. Thêm cột 'Language Skill Name' hoặc đặt tên/code phần có từ khóa (Từ vựng, Ngữ pháp, Nghe hiểu, Đọc hiểu, Hán tự).");
                return null;
            }
            if (ctx.AmbiguousSkillNames.Contains(name))
            {
                errors.Add($"{prefix}kỹ năng '{name}' bị trùng tên trong hệ thống, không xác định được kỹ năng.");
                return null;
            }
            if (!ctx.SkillsByName.TryGetValue(name, out var skill))
            {
                errors.Add($"{prefix}kỹ năng '{name}'{(inferred ? " (suy ra từ tên phần)" : "")} không tồn tại trong hệ thống.");
                return null;
            }
            return skill;
        }

        /// <summary>Suy tên kỹ năng (khớp LanguageSkill.Name, không phân biệt hoa/thường) từ từ khóa trong tên phần.</summary>
        private static string? InferSkillName(string sectionTitle)
        {
            var lower = sectionTitle.ToLowerInvariant();
            static bool Has(string text, params string[] keys) => keys.Any(k => text.Contains(k, StringComparison.Ordinal));

            if (Has(lower, "từ vựng", "tu vung", "vocabulary", "語彙", "moji")) return "VOCABULARY";
            if (Has(lower, "ngữ pháp", "ngu phap", "grammar", "文法", "bunpou", "bunpo")) return "GRAMMAR";
            if (Has(lower, "hán tự", "han tu", "kanji", "漢字")) return "KANJI";
            if (Has(lower, "nghe", "choukai", "chōkai", "listening", "聴解")) return "LISTENING";
            if (Has(lower, "đọc", "doc hieu", "dokkai", "reading", "読解")) return "READING";
            return null;
        }
    }
}
