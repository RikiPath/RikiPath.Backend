using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Application.Responses.ContentReview;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RikiPath.Application.Services
{
    /// <summary>
    /// AI kiểm tra nội dung Content Author mới thêm: Lesson (+Kanji/Vocabulary/Grammar liên kết),
    /// Kanji, Vocabulary, GrammarPattern, MockTest (+Section +Question +Option), PracticeExercise (+Question +Option).
    /// 2 lớp: (1) Rule — kiểm tra bằng code (chắc chắn); (2) AI — kiểm tra ngữ nghĩa.
    /// Mọi lỗi chỉ rõ: mục nào (ItemType/ItemId/ItemLabel), trường nào (Field), giá trị hiện tại, lý do, gợi ý sửa.
    /// </summary>
    public class ContentAiReviewService(
        IUnitOfWork unitOfWork,
        IAiContentReviewClient aiClient,
        IClaimService claimService,
        ILogger<ContentAiReviewService> logger) : IContentAiReviewService
    {
        private const int UnitsPerBatch = 12;            // Số mục gửi AI mỗi lần (tránh phình token / JSON bị cắt)
        private const int MaxParallelAiCalls = 3;        // Gọi AI song song tối đa N batch
        private const int MaxUnits = 300;                // Nội dung quá lớn thì chỉ kiểm tra tối đa N mục
        private const double MinErrorConfidence = 0.7;   // AI "Error" nhưng tự tin thấp => hạ xuống Warning
        private const double MinReportConfidence = 0.5;  // Dưới ngưỡng này bỏ luôn (giảm báo nhầm)

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // giữ nguyên tiếng Nhật/Việt
        };

        // ---------- Regex ----------
        private static readonly Regex KanaOnly = new(@"^[\u3040-\u309F\u30A0-\u30FFー・\s]+$", RegexOptions.Compiled);
        private static readonly Regex HiraganaTokens = new(@"^[\u3040-\u309Fー.\-]+$", RegexOptions.Compiled);
        private static readonly Regex KatakanaTokens = new(@"^[\u30A0-\u30FFー.\-]+$", RegexOptions.Compiled);
        private static readonly Regex HasJapanese = new(@"[\u3040-\u30FF\u4E00-\u9FFF]", RegexOptions.Compiled);
        private static readonly Regex HasKanji = new(@"[\u4E00-\u9FFF]", RegexOptions.Compiled);
        private static readonly Regex ReadingSeparators = new(@"[、,，/／;；\s]+", RegexOptions.Compiled);

        private static readonly HashSet<string> KnownQuestionTypes = new(StringComparer.OrdinalIgnoreCase)
            { "SingleChoice", "MultipleChoice", "Ordering", "FillBlank", "ShortAnswer" };

        private enum UnitKind
        {
            Lesson, Kanji, Vocabulary, Grammar, MockTest, MockSection, MockQuestion, PracticeExercise, PracticeQuestion
        }

        /// <summary>Danh sách đáp án (đã sắp xếp) và vị trí (1-based) các đáp án được đánh dấu đúng.</summary>
        private sealed record OptionInfo(List<string> Texts, List<int> CorrectIndexes);

        /// <summary>
        /// Một mục cần kiểm tra. Fields = trường author nhập (AI/Rule được phép báo lỗi),
        /// Context = thông tin tham chiếu (không báo lỗi trên đó).
        /// </summary>
        private sealed record ReviewUnit(
            string Key, UnitKind Kind, int Id, string Label, string? LevelCode,
            Dictionary<string, string?> Fields,
            Dictionary<string, string?> Context,
            OptionInfo? Options = null);

        // =====================================================================
        // PUBLIC
        // =====================================================================
        public async Task<ApiResponse<ContentAiReviewResponse>> ReviewAsync(
            ContentEntityType type, int contentId, AuthorContentDetailsResponse? staged, CancellationToken cancellationToken)
        {
            var userId = 0;
            try
            {
                userId = claimService.GetUserClaim().Id;

                var units = await LoadUnitsAsync(type, contentId, staged, cancellationToken);
                if (units is null || units.Count == 0)
                    return ApiResponse<ContentAiReviewResponse>.NotFound($"Không tìm thấy hoặc chưa hỗ trợ AI kiểm tra cho {type} Id = {contentId}.");

                var truncated = units.Count > MaxUnits;
                if (truncated)
                    units = units.Take(MaxUnits).ToList();

                // ---- Lớp 1: Rule ----
                var issues = new List<ContentReviewIssue>();
                foreach (var unit in units)
                    issues.AddRange(RunRules(unit));

                // ---- Lớp 2: AI (theo batch, song song có giới hạn) ----
                string? aiNote = null;
                try
                {
                    var unitByKey = units.ToDictionary(u => u.Key);
                    var batches = units.Chunk(UnitsPerBatch).ToList();
                    var failedBatches = 0;
                    using var gate = new SemaphoreSlim(MaxParallelAiCalls);

                    async Task<List<ContentReviewIssue>> RunBatchAsync(ReviewUnit[] batch)
                    {
                        await gate.WaitAsync(cancellationToken);
                        try
                        {
                            return await CheckBatchWithAiAsync(userId, batch, unitByKey, cancellationToken);
                        }
                        catch (AiQuotaExceededException) { throw; }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            Interlocked.Increment(ref failedBatches);
                            logger.LogWarning(ex, "AI review batch failed. Type={Type} Id={Id}", type, contentId);
                            return new List<ContentReviewIssue>();
                        }
                        finally { gate.Release(); }
                    }

                    var tasks = batches.Select(RunBatchAsync).ToList();

                    foreach (var aiIssues in await Task.WhenAll(tasks))
                        foreach (var ai in aiIssues)
                        {
                            // Rule đã báo Error đúng trường đó rồi thì không báo trùng
                            var duplicated = issues.Any(i =>
                                i.Source == "Rule" && i.Severity == "Error" &&
                                i.ItemType == ai.ItemType && i.ItemId == ai.ItemId &&
                                string.Equals(i.Field, ai.Field, StringComparison.OrdinalIgnoreCase));
                            if (!duplicated)
                                issues.Add(ai);
                        }

                    if (failedBatches > 0)
                        aiNote = $"{failedBatches}/{batches.Count} nhóm nội dung chưa được AI kiểm tra (AI lỗi hoặc trả JSON sai), hãy kiểm tra lại.";
                }
                catch (AiQuotaExceededException ex)
                {
                    return ApiResponse<ContentAiReviewResponse>.Fail(ex.Message, HttpStatusCode.TooManyRequests);
                }

                issues = issues
                    .OrderBy(i => i.Severity == "Error" ? 0 : i.Severity == "Warning" ? 1 : 2)
                    .ThenBy(i => i.ItemType).ThenBy(i => i.ItemId).ThenBy(i => i.Field)
                    .ToList();

                var errors = issues.Count(i => i.Severity == "Error");
                var warnings = issues.Count(i => i.Severity == "Warning");
                var verdict = errors > 0 ? "NeedsRevision" : warnings > 0 ? "PassedWithWarnings" : "Passed";

                var entity = new ContentAiReview
                {
                    ContentType = type.ToString(),
                    ContentId = contentId,
                    Verdict = verdict,
                    Summary = BuildSummary(verdict, errors, warnings, units.Count, truncated, aiNote),
                    ErrorCount = errors,
                    WarningCount = warnings,
                    IssuesJson = JsonSerializer.Serialize(issues, JsonOptions),
                    RequestedByUserId = userId,
                    ReviewedAt = DateTime.UtcNow,
                };
                await unitOfWork.ContentAiReviews.AddAsync(entity, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentAiReviewResponse>.Success(ToResponse(entity, issues, units.Count));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to review content. Type={Type} Id={Id} UserId={UserId}", type, contentId, userId);
                return ApiResponse<ContentAiReviewResponse>.Fail(
                    "Không thể kiểm tra nội dung bằng AI.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<ContentAiReviewResponse?>> GetLatestAsync(
            ContentEntityType type, int contentId, CancellationToken cancellationToken)
        {
            try
            {
                var typeName = type.ToString();
                var latest = await unitOfWork.ContentAiReviews.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.ContentType == typeName && x.ContentId == contentId)
                    .OrderByDescending(x => x.ReviewedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (latest is null)
                    return ApiResponse<ContentAiReviewResponse?>.Success(null);

                var issues = JsonSerializer.Deserialize<List<ContentReviewIssue>>(latest.IssuesJson, JsonOptions) ?? [];
                return ApiResponse<ContentAiReviewResponse?>.Success(ToResponse(latest, issues, 0));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load latest AI review. Type={Type} Id={Id}", type, contentId);
                return ApiResponse<ContentAiReviewResponse?>.Fail(
                    "Không thể tải kết quả kiểm tra gần nhất.", HttpStatusCode.InternalServerError);
            }
        }

        // =====================================================================
        // LOAD: nội dung -> ReviewUnit (tên field = tên input trên form)
        //  - Lesson/Kanji/Vocabulary/GrammarPattern: đọc từ BẢN NHÁP (ContentApprovalRequest.PayloadJson)
        //    thông qua AuthorContentDetailsResponse do ContentAuthorService truyền vào.
        //  - MockTest/PracticeExercise: đọc trực tiếp từ DB (có đủ section/câu hỏi/đáp án).
        // =====================================================================
        private async Task<List<ReviewUnit>?> LoadUnitsAsync(
            ContentEntityType type, int id, AuthorContentDetailsResponse? staged, CancellationToken ct)
        {
            switch (type)
            {
                case ContentEntityType.Lesson:
                case ContentEntityType.Kanji:
                case ContentEntityType.Vocabulary:
                case ContentEntityType.GrammarPattern:
                    return staged is null ? null : [await StagedUnitAsync(type, id, staged, ct)];
                case ContentEntityType.MockTest:
                    return await LoadMockTestAsync(id, ct);
                case ContentEntityType.PracticeExercise:
                    return await LoadPracticeExerciseAsync(id, ct);
                default:
                    return null;
            }
        }

        private async Task<string?> GetLevelCodeAsync(int? certificateLevelId, CancellationToken ct)
        {
            if (certificateLevelId is not > 0) return null;
            var level = await unitOfWork.CertificateLevels.GetByIdAsync(certificateLevelId.Value, ct);
            return level is null || level.IsDeleted ? null : level.Code;
        }

        private async Task<ReviewUnit> StagedUnitAsync(
            ContentEntityType type, int id, AuthorContentDetailsResponse d, CancellationToken ct)
        {
            var level = await GetLevelCodeAsync(d.CertificateLevelId, ct);
            var context = new Dictionary<string, string?>();

            switch (type)
            {
                case ContentEntityType.Kanji:
                    return new($"Kanji#{id}", UnitKind.Kanji, id, $"Kanji: {d.Character}", level,
                        new Dictionary<string, string?>
                        {
                            ["Character"] = d.Character,
                            ["Meaning"] = d.Meaning,
                            ["SinoVietnamese"] = d.SinoVietnamese,
                            ["OnYomi"] = d.OnYomi,
                            ["KunYomi"] = d.KunYomi,
                            ["StrokeCount"] = $"{d.StrokeCount}",
                            ["StrokeOrderImageUrl"] = d.StrokeOrderImageUrl,
                            ["AudioUrl"] = d.AudioUrl,
                        }, context);

                case ContentEntityType.Vocabulary:
                    return new($"Vocabulary#{id}", UnitKind.Vocabulary, id, $"Từ vựng: {d.Word}", level,
                        new Dictionary<string, string?>
                        {
                            ["Word"] = d.Word,
                            ["Reading"] = d.Reading,
                            ["Meaning"] = d.Meaning,
                            ["ExampleSentence"] = d.ExampleSentence,
                            ["ExampleSentenceMeaning"] = d.ExampleSentenceMeaning,
                            ["AudioUrl"] = d.AudioUrl,
                        }, context);

                case ContentEntityType.GrammarPattern:
                    return new($"Grammar#{id}", UnitKind.Grammar, id, $"Ngữ pháp: {d.Title}", level,
                        new Dictionary<string, string?>
                        {
                            ["Title"] = d.Title,
                            ["Structure"] = d.Structure,
                            ["UsageNotes"] = d.UsageNotes,
                            ["ExampleSentence"] = d.ExampleSentence,
                            ["ExampleSentenceMeaning"] = d.ExampleSentenceMeaning,
                        }, context);

                default: // Lesson
                    {
                        int? skillId = d.LanguageSkillId; // int hoặc int? đều gán được
                        if (skillId is > 0)
                        {
                            var skill = await unitOfWork.LanguageSkills.GetByIdAsync(skillId.Value, ct);
                            context["LanguageSkill"] = skill?.Name;
                        }
                        return new($"Lesson#{id}", UnitKind.Lesson, id, $"Bài học: {d.Title}", level,
                            new Dictionary<string, string?>
                            {
                                ["Title"] = d.Title,
                                ["Description"] = d.Description,
                            }, context);
                    }
            }
        }

        private async Task<List<ReviewUnit>?> LoadMockTestAsync(int id, CancellationToken ct)
        {
            var test = await unitOfWork.MockTests.Query().AsNoTracking().AsSplitQuery()
                .Include(x => x.CertificateLevel)
                .Include(x => x.Sections!).ThenInclude(s => s.LanguageSkill)
                .Include(x => x.Sections!).ThenInclude(s => s.CertificateSection)
                .Include(x => x.Sections!).ThenInclude(s => s.Questions!).ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (test is null) return null;

            var levelCode = test.CertificateLevel?.Code;
            var sections = (test.Sections ?? []).Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToList();

            var units = new List<ReviewUnit>
            {
                new($"MockTest#{test.Id}", UnitKind.MockTest, test.Id, $"Đề thi thử: {test.Title}", levelCode,
                    new Dictionary<string, string?>
                    {
                        ["Title"] = test.Title,
                        ["Description"] = test.Description,
                        ["TimeLimitMinutes"] = test.TimeLimitMinutes.ToString(),
                    },
                    new Dictionary<string, string?> { ["SectionCount"] = sections.Count.ToString() })
            };

            foreach (var section in sections)
            {
                var questions = (section.Questions ?? []).Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToList();
                var skill = section.LanguageSkill?.Name;
                var levelMismatch = section.CertificateSection is not null &&
                                    section.CertificateSection.CertificateLevelId != test.CertificateLevelId;

                units.Add(new($"MockSection#{section.Id}", UnitKind.MockSection, section.Id,
                    $"Phần thi \"{section.Title}\" (đề: {test.Title})", levelCode,
                    new Dictionary<string, string?> { ["Title"] = section.Title },
                    new Dictionary<string, string?>
                    {
                        ["LanguageSkill"] = skill,
                        ["CertificateSection"] = section.CertificateSection?.Name,
                        ["QuestionCount"] = questions.Count.ToString(),
                        ["CertificateSectionLevelMismatch"] = levelMismatch ? "true" : "false",
                    }));

                var n = 0;
                foreach (var q in questions)
                {
                    n++;
                    var fields = new Dictionary<string, string?>
                    {
                        ["QuestionText"] = q.QuestionText,
                        ["QuestionType"] = q.QuestionType,
                        ["InteractionDataJson"] = q.InteractionDataJson,
                        ["AnswerKeyJson"] = q.AnswerKeyJson,
                        ["AudioUrl"] = q.AudioUrl,
                        ["ImageUrl"] = q.ImageUrl,
                        ["Explanation"] = q.Explanation,
                    };
                    var options = FillOptions(fields,
                        (q.Options ?? []).Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).ThenBy(o => o.Id)
                            .Select(o => (o.OptionText, o.IsCorrect)));

                    units.Add(new($"MockQuestion#{q.Id}", UnitKind.MockQuestion, q.Id,
                        $"Câu {n} - {section.Title} (đề: {test.Title})", levelCode, fields,
                        new Dictionary<string, string?> { ["SectionSkill"] = skill, ["SectionTitle"] = section.Title },
                        options));
                }
            }
            return units;
        }

        private async Task<List<ReviewUnit>?> LoadPracticeExerciseAsync(int id, CancellationToken ct)
        {
            var ex = await unitOfWork.PracticeExercises.Query().AsNoTracking().AsSplitQuery()
                .Include(x => x.LanguageSkill)
                .Include(x => x.Lesson).ThenInclude(l => l.CertificateLevel)
                .Include(x => x.Lesson).ThenInclude(l => l.LanguageSkill)
                .Include(x => x.Questions!).ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (ex is null) return null;

            var levelCode = ex.Lesson?.CertificateLevel?.Code;
            var questions = (ex.Questions ?? []).Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToList();

            var units = new List<ReviewUnit>
            {
                new($"PracticeExercise#{ex.Id}", UnitKind.PracticeExercise, ex.Id, $"Bài luyện tập: {ex.Title}", levelCode,
                    new Dictionary<string, string?>
                    {
                        ["Title"] = ex.Title,
                        ["Description"] = ex.Description,
                    },
                    new Dictionary<string, string?>
                    {
                        ["LanguageSkill"] = ex.LanguageSkill?.Name,
                        ["LessonTitle"] = ex.Lesson?.Title,
                        ["LessonSkill"] = ex.Lesson?.LanguageSkill?.Name,
                        ["LessonSkillId"] = ex.Lesson?.LanguageSkillId.ToString(),
                        ["ExerciseSkillId"] = ex.LanguageSkillId.ToString(),
                        ["QuestionCount"] = questions.Count.ToString(),
                    })
            };

            var n = 0;
            foreach (var q in questions)
            {
                n++;
                var fields = new Dictionary<string, string?>
                {
                    ["QuestionText"] = q.QuestionText,
                    ["Explanation"] = q.Explanation,
                };
                var options = FillOptions(fields,
                    (q.Options ?? []).Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).ThenBy(o => o.Id)
                        .Select(o => (o.OptionText, o.IsCorrect)));

                units.Add(new($"PracticeQuestion#{q.Id}", UnitKind.PracticeQuestion, q.Id,
                    $"Câu {n} - {ex.Title}", levelCode, fields,
                    new Dictionary<string, string?> { ["ExerciseSkill"] = ex.LanguageSkill?.Name, ["LessonTitle"] = ex.Lesson?.Title },
                    options));
            }
            return units;
        }

        /// <summary>Ghi các đáp án vào Fields thành Option1..OptionN + CorrectOptions để chỉ rõ từng đáp án khi báo lỗi.</summary>
        private static OptionInfo FillOptions(Dictionary<string, string?> fields, IEnumerable<(string Text, bool IsCorrect)> options)
        {
            var texts = new List<string>();
            var correct = new List<int>();
            var i = 0;
            foreach (var (text, isCorrect) in options)
            {
                i++;
                texts.Add(text ?? string.Empty);
                fields[$"Option{i}"] = text;
                if (isCorrect) correct.Add(i);
            }
            fields["CorrectOptions"] = correct.Count == 0 ? "(chưa đánh dấu đáp án đúng)" : string.Join(",", correct);
            return new OptionInfo(texts, correct);
        }

        // =====================================================================
        // LỚP 1 - RULE (deterministic)
        // =====================================================================
        private static List<ContentReviewIssue> RunRules(ReviewUnit u)
        {
            var list = new List<ContentReviewIssue>();

            void Add(string severity, string field, string reason, string? suggestion = null) =>
                list.Add(new ContentReviewIssue
                {
                    Source = "Rule",
                    Severity = severity,
                    ItemType = u.Kind.ToString(),
                    ItemId = u.Id,
                    ItemLabel = u.Label,
                    Field = field,
                    CurrentValue = Get(u, field),
                    Reason = reason,
                    Suggestion = suggestion,
                    Confidence = 1,
                });

            void Required(string field, string label, string severity = "Error")
            {
                if (IsBlank(Get(u, field)))
                    Add(severity, field, $"Thiếu {label}. Trường này không được để trống.");
            }

            void UrlIfPresent(string field, string label)
            {
                var value = Get(u, field);
                if (!IsBlank(value) &&
                    !(Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
                    Add("Error", field, $"{label} không phải URL hợp lệ (phải bắt đầu bằng http:// hoặc https://).");
            }

            void JsonIfPresent(string field, string label)
            {
                var value = Get(u, field);
                if (IsBlank(value)) return;
                try { using var _ = JsonDocument.Parse(value!); }
                catch (JsonException) { Add("Error", field, $"{label} không phải JSON hợp lệ."); }
            }

            void ExamplePair()
            {
                var sentence = Get(u, "ExampleSentence");
                var meaning = Get(u, "ExampleSentenceMeaning");
                if (!IsBlank(sentence) && !HasJapanese.IsMatch(sentence!))
                    Add("Error", "ExampleSentence", "Câu ví dụ không chứa tiếng Nhật (không có kana/kanji).");
                if (!IsBlank(sentence) && IsBlank(meaning))
                    Add("Error", "ExampleSentenceMeaning", "Có câu ví dụ nhưng thiếu bản dịch nghĩa của câu.");
                if (IsBlank(sentence) && !IsBlank(meaning))
                    Add("Error", "ExampleSentence", "Có bản dịch câu ví dụ nhưng thiếu câu ví dụ gốc.");
            }

            // Kiểm tra đáp án trắc nghiệm: số lượng, để trống, trùng nhau, số đáp án đúng
            void ChoiceRules(bool multiple)
            {
                var opts = u.Options;
                if (opts is null) return;

                if (opts.Texts.Count < 2)
                    Add("Error", "Option1", $"Câu trắc nghiệm cần ít nhất 2 đáp án, hiện chỉ có {opts.Texts.Count}.");

                for (var i = 0; i < opts.Texts.Count; i++)
                    if (IsBlank(opts.Texts[i]))
                        Add("Error", $"Option{i + 1}", $"Đáp án {i + 1} để trống.");

                var seen = new Dictionary<string, int>();
                for (var i = 0; i < opts.Texts.Count; i++)
                {
                    var norm = opts.Texts[i].Trim().ToLowerInvariant();
                    if (norm.Length == 0) continue;
                    if (seen.TryGetValue(norm, out var first))
                        Add("Error", $"Option{i + 1}", $"Đáp án {i + 1} trùng nội dung với đáp án {first}.");
                    else
                        seen[norm] = i + 1;
                }

                if (opts.CorrectIndexes.Count == 0)
                    Add("Error", "CorrectOptions", "Chưa đánh dấu đáp án đúng nào.");
                else if (!multiple && opts.CorrectIndexes.Count > 1)
                    Add("Error", "CorrectOptions",
                        $"Câu chọn 1 đáp án nhưng đang đánh dấu {opts.CorrectIndexes.Count} đáp án đúng (đáp án {string.Join(", ", opts.CorrectIndexes)}).");
                else if (multiple && opts.Texts.Count > 1 && opts.CorrectIndexes.Count == opts.Texts.Count)
                    Add("Warning", "CorrectOptions", "Tất cả đáp án đều được đánh dấu đúng, hãy kiểm tra lại.");
            }

            switch (u.Kind)
            {
                case UnitKind.Kanji:
                    {
                        var ch = Get(u, "Character")?.Trim();
                        if (IsBlank(ch))
                            Add("Error", "Character", "Thiếu chữ Kanji.");
                        else if (new System.Globalization.StringInfo(ch!).LengthInTextElements != 1 || !HasKanji.IsMatch(ch!))
                            Add("Error", "Character", $"Trường Character phải đúng 1 chữ Kanji, hiện là \"{ch}\".");

                        Required("Meaning", "nghĩa tiếng Việt");
                        Required("SinoVietnamese", "âm Hán Việt", "Warning");

                        if (!int.TryParse(Get(u, "StrokeCount"), out var strokes) || strokes < 1 || strokes > 40)
                            Add("Error", "StrokeCount", "Số nét phải là số nguyên từ 1 đến 40.");

                        var on = Get(u, "OnYomi");
                        var kun = Get(u, "KunYomi");
                        if (IsBlank(on) && IsBlank(kun))
                            Add("Error", "OnYomi", "Cần ít nhất một cách đọc (On-yomi hoặc Kun-yomi).");
                        if (!IsBlank(on) && ReadingSeparators.Split(on!.Trim()).Where(t => t.Length > 0).Any(t => !KatakanaTokens.IsMatch(t)))
                            Add("Error", "OnYomi", "On-yomi phải viết bằng Katakana (có thể dùng dấu \".\" hoặc \"-\").", "Ví dụ: ガク, ベン");
                        if (!IsBlank(kun) && ReadingSeparators.Split(kun!.Trim()).Where(t => t.Length > 0).Any(t => !HiraganaTokens.IsMatch(t)))
                            Add("Error", "KunYomi", "Kun-yomi phải viết bằng Hiragana (có thể dùng dấu \".\" hoặc \"-\").", "Ví dụ: まな.ぶ, つと.める");

                        UrlIfPresent("StrokeOrderImageUrl", "Ảnh thứ tự nét");
                        UrlIfPresent("AudioUrl", "Audio");
                        break;
                    }

                case UnitKind.Vocabulary:
                    {
                        Required("Word", "từ vựng");
                        Required("Meaning", "nghĩa tiếng Việt");

                        var word = Get(u, "Word");
                        var reading = Get(u, "Reading");
                        if (IsBlank(reading))
                            Add("Error", "Reading", "Thiếu cách đọc (Reading).");
                        else if (!KanaOnly.IsMatch(reading!.Trim()))
                            Add("Error", "Reading", "Cách đọc chỉ được gồm Hiragana/Katakana, không được chứa Kanji, chữ Latin hay ký tự khác.");

                        ExamplePair();

                        var example = Get(u, "ExampleSentence");
                        if (!IsBlank(example) && !IsBlank(word) &&
                            !example!.Contains(word!.Trim()) && (IsBlank(reading) || !example.Contains(reading!.Trim())))
                            Add("Warning", "ExampleSentence", $"Câu ví dụ không chứa từ \"{word}\" (cũng không chứa cách đọc), có thể ví dụ chưa dùng đúng từ.");

                        UrlIfPresent("AudioUrl", "Audio");
                        break;
                    }

                case UnitKind.Grammar:
                    {
                        Required("Title", "tên mẫu ngữ pháp");
                        Required("Structure", "cấu trúc");
                        Required("UsageNotes", "giải thích cách dùng", "Warning");
                        if (IsBlank(Get(u, "ExampleSentence")) && IsBlank(Get(u, "ExampleSentenceMeaning")))
                            Add("Warning", "ExampleSentence", "Mẫu ngữ pháp chưa có câu ví dụ.");
                        else
                            ExamplePair();
                        break;
                    }

                case UnitKind.Lesson:
                    {
                        Required("Title", "tiêu đề bài học");
                        Required("Description", "mô tả bài học", "Warning");
                        break;
                    }

                case UnitKind.MockTest:
                    {
                        Required("Title", "tên đề thi");
                        Required("Description", "mô tả đề thi", "Warning");
                        if (!int.TryParse(Get(u, "TimeLimitMinutes"), out var minutes) || minutes <= 0)
                            Add("Error", "TimeLimitMinutes", "Thời gian làm bài phải lớn hơn 0 phút.");
                        else if (minutes > 300)
                            Add("Warning", "TimeLimitMinutes", $"Thời gian làm bài {minutes} phút quá dài, hãy kiểm tra lại.");
                        if (u.Context.GetValueOrDefault("SectionCount") == "0")
                            Add("Error", "Title", "Đề thi chưa có phần thi (section) nào.");
                        break;
                    }

                case UnitKind.MockSection:
                    {
                        Required("Title", "tên phần thi");
                        if (u.Context.GetValueOrDefault("QuestionCount") == "0")
                            Add("Error", "Title", "Phần thi chưa có câu hỏi nào.");
                        if (u.Context.GetValueOrDefault("CertificateSectionLevelMismatch") == "true")
                            Add("Error", "Title",
                                $"Phần thi gắn với CertificateSection \"{u.Context.GetValueOrDefault("CertificateSection")}\" thuộc cấp độ khác với cấp độ của đề thi.");
                        break;
                    }

                case UnitKind.MockQuestion:
                    {
                        var type = Get(u, "QuestionType");
                        var isChoice = string.Equals(type, "SingleChoice", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(type, "MultipleChoice", StringComparison.OrdinalIgnoreCase);

                        if (IsBlank(Get(u, "QuestionText")) && IsBlank(Get(u, "AudioUrl")) && IsBlank(Get(u, "ImageUrl")))
                            Add("Error", "QuestionText", "Câu hỏi không có nội dung (không có chữ, audio hay hình ảnh).");

                        if (IsBlank(type) || !KnownQuestionTypes.Contains(type!))
                            Add("Error", "QuestionType", $"Loại câu hỏi \"{type}\" không được hỗ trợ.", "SingleChoice, MultipleChoice, Ordering, FillBlank hoặc ShortAnswer");

                        if (isChoice)
                            ChoiceRules(multiple: string.Equals(type, "MultipleChoice", StringComparison.OrdinalIgnoreCase));
                        else if (!IsBlank(type) && KnownQuestionTypes.Contains(type!))
                        {
                            if (IsBlank(Get(u, "AnswerKeyJson")))
                                Add("Error", "AnswerKeyJson", $"Câu loại {type} bắt buộc có đáp án (AnswerKeyJson).");
                        }

                        JsonIfPresent("InteractionDataJson", "InteractionDataJson");
                        JsonIfPresent("AnswerKeyJson", "AnswerKeyJson");
                        UrlIfPresent("AudioUrl", "Audio");
                        UrlIfPresent("ImageUrl", "Hình ảnh");

                        var skill = u.Context.GetValueOrDefault("SectionSkill") ?? string.Empty;
                        var isListening = skill.Contains("listen", StringComparison.OrdinalIgnoreCase) ||
                                          skill.Contains("nghe", StringComparison.OrdinalIgnoreCase) ||
                                          skill.Contains("聴解");
                        if (isListening && IsBlank(Get(u, "AudioUrl")))
                            Add("Error", "AudioUrl", $"Câu hỏi thuộc phần thi kỹ năng \"{skill}\" nhưng chưa có audio.");

                        Required("Explanation", "giải thích đáp án", "Warning");
                        break;
                    }

                case UnitKind.PracticeExercise:
                    {
                        Required("Title", "tên bài luyện tập");
                        if (u.Context.GetValueOrDefault("QuestionCount") == "0")
                            Add("Error", "Title", "Bài luyện tập chưa có câu hỏi nào.");
                        var lessonSkillId = u.Context.GetValueOrDefault("LessonSkillId");
                        var exerciseSkillId = u.Context.GetValueOrDefault("ExerciseSkillId");
                        if (lessonSkillId is not null && exerciseSkillId is not null && lessonSkillId != exerciseSkillId)
                            Add("Warning", "Title",
                                $"Kỹ năng của bài luyện tập (\"{u.Context.GetValueOrDefault("LanguageSkill")}\") khác kỹ năng của bài học (\"{u.Context.GetValueOrDefault("LessonSkill")}\").");
                        break;
                    }

                case UnitKind.PracticeQuestion:
                    {
                        Required("QuestionText", "nội dung câu hỏi");
                        ChoiceRules(multiple: false);
                        Required("Explanation", "giải thích đáp án", "Warning");
                        break;
                    }
            }

            return list;
        }

        // =====================================================================
        // LỚP 2 - AI
        // =====================================================================
        private async Task<List<ContentReviewIssue>> CheckBatchWithAiAsync(
            int userId, ReviewUnit[] batch, Dictionary<string, ReviewUnit> unitByKey, CancellationToken ct)
        {
            var prompt = BuildPrompt(batch);
            var raw = StripCodeFence(await aiClient.ReviewContentJsonAsync(userId, prompt, ct));

            using var doc = JsonDocument.Parse(raw);
            var result = new List<ContentReviewIssue>();
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("issues", out var issues) ||
                issues.ValueKind != JsonValueKind.Array)
                return result;

            var batchKeys = batch.Select(b => b.Key).ToHashSet();
            foreach (var el in issues.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;

                var key = ReadString(el, "unitKey");
                var field = ReadString(el, "field");
                var reason = ReadString(el, "reason");
                // Chống AI bịa: unitKey phải thuộc batch, field phải là trường có thật, phải có lý do
                if (!batchKeys.Contains(key) || !unitByKey.TryGetValue(key, out var unit)) continue;
                if (string.IsNullOrWhiteSpace(reason)) continue;
                var realField = unit.Fields.Keys.FirstOrDefault(k => string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
                if (realField is null) continue;

                var confidence = ReadDouble(el, "confidence", 0.6);
                if (confidence < MinReportConfidence) continue;

                var severity = ReadString(el, "severity") switch
                {
                    "Error" => "Error",
                    "Info" => "Info",
                    _ => "Warning",
                };
                if (severity == "Error" && confidence < MinErrorConfidence)
                    severity = "Warning"; // không chắc chắn thì chỉ cảnh báo, để author tự kiểm tra

                var suggestion = ReadString(el, "suggestion");
                result.Add(new ContentReviewIssue
                {
                    Source = "AI",
                    Severity = severity,
                    ItemType = unit.Kind.ToString(),
                    ItemId = unit.Id,
                    ItemLabel = unit.Label,
                    Field = realField,
                    CurrentValue = unit.Fields[realField], // lấy từ DB, không tin giá trị AI trích lại
                    Reason = reason,
                    Suggestion = string.IsNullOrWhiteSpace(suggestion) ? null : suggestion,
                    Confidence = Math.Round(confidence, 2),
                });
            }

            return result;
        }

        private static string BuildPrompt(ReviewUnit[] batch)
        {
            var payload = batch.Select(u => new
            {
                unitKey = u.Key,
                type = u.Kind.ToString(),
                jlptLevel = u.LevelCode,
                fields = u.Fields,   // CHỈ được báo lỗi trên các trường này
                context = u.Context, // chỉ để tham khảo
            });

            var sb = new StringBuilder();
            sb.AppendLine("Bạn là biên tập viên kiểm duyệt nội dung của nền tảng luyện thi JLPT cho người Việt (chuyên gia tiếng Nhật và tiếng Việt).");
            sb.AppendLine("Nhiệm vụ: kiểm tra dữ liệu do Content Author nhập, tìm lỗi SAI THẬT SỰ và giải thích rõ để author tự kiểm tra, sửa lại.");
            sb.AppendLine();
            sb.AppendLine("[QUY TẮC KIỂM TRA THEO LOẠI]");
            sb.AppendLine("- Kanji: nghĩa tiếng Việt đúng; âm Hán Việt đúng; On-yomi (Katakana)/Kun-yomi (Hiragana) đúng và đủ các cách đọc phổ biến; số nét đúng; phù hợp cấp độ jlptLevel.");
            sb.AppendLine("- Vocabulary: Reading khớp Word; nghĩa tiếng Việt đúng; câu ví dụ đúng ngữ pháp, tự nhiên, dùng đúng Word; ExampleSentenceMeaning khớp câu ví dụ; phù hợp cấp độ.");
            sb.AppendLine("- Grammar: Structure đúng quy tắc chia/ghép; UsageNotes giải thích đúng, không nhầm mẫu khác; câu ví dụ dùng ĐÚNG mẫu này; bản dịch khớp; phù hợp cấp độ.");
            sb.AppendLine("- Lesson: Title/Description có khớp với context (kỹ năng, số lượng mục liên kết) và nội dung các mục liên kết cùng gói dữ liệu; mục liên kết không quá cao/thấp so với cấp độ bài học.");
            sb.AppendLine("- MockTest / MockSection: Title/Description hợp lý với cấp độ và kỹ năng (context.LanguageSkill, context.CertificateSection); tên phần thi khớp kỹ năng.");
            sb.AppendLine("- MockQuestion và PracticeQuestion (QUAN TRỌNG NHẤT): hãy TỰ GIẢI câu hỏi rồi so với đáp án tác giả đánh dấu đúng (CorrectOptions là số thứ tự Option, bắt đầu từ 1). Báo Error nếu đáp án đánh dấu sai, có đáp án đúng khác không được đánh dấu, có nhiều hơn số đáp án đúng cho phép, hoặc không đáp án nào đúng. Báo lỗi ở trường CorrectOptions (hoặc OptionN nếu một đáp án cụ thể sai/gây mơ hồ). Cũng kiểm tra: câu hỏi mơ hồ, lỗi chính tả/ngữ pháp tiếng Nhật trong QuestionText và các Option, các đáp án nhiễu có thật sự sai, Explanation khớp đáp án đúng và giải thích chính xác, độ khó phù hợp jlptLevel. Với câu loại Ordering/FillBlank/ShortAnswer kiểm tra AnswerKeyJson có khớp InteractionDataJson và QuestionText.");
            sb.AppendLine("- Câu nghe (context.SectionSkill là nghe): bạn KHÔNG có audio/script nên KHÔNG đánh giá nội dung nghe và KHÔNG đoán đáp án; chỉ kiểm tra phần chữ (lỗi chính tả, đáp án nhiễu, giải thích tự mâu thuẫn).");
            sb.AppendLine("- PracticeExercise: Title/Description khớp bài học (context.LessonTitle) và kỹ năng.");
            sb.AppendLine();
            sb.AppendLine("[NGUYÊN TẮC BẮT BUỘC]");
            sb.AppendLine("- Chỉ báo lỗi khi bạn thực sự chắc chắn. KHÔNG báo vì khác phong cách, KHÔNG bịa. Nếu không chắc, đặt confidence thấp.");
            sb.AppendLine("- Không báo lại lỗi hình thức đơn giản (để trống, sai bảng chữ, URL sai, trùng đáp án, sai số đáp án đúng, JSON sai) — hệ thống đã kiểm tra riêng.");
            sb.AppendLine("- `unitKey` PHẢI lấy đúng từ dữ liệu; `field` PHẢI là một khóa trong `fields` của unit đó (không dùng khóa trong `context`). Mỗi lỗi chỉ một trường.");
            sb.AppendLine("- `reason`: nêu rõ VÌ SAO sai (giá trị hiện tại sai ở đâu, đáp án đúng là gì và tại sao). `suggestion`: giá trị nên sửa thành.");
            sb.AppendLine("- severity: \"Error\" = chắc chắn sai; \"Warning\" = đáng ngờ/chưa tự nhiên/lệch cấp độ; \"Info\" = góp ý nhỏ.");
            sb.AppendLine("- confidence: số từ 0 đến 1.");
            sb.AppendLine("- Viết reason và suggestion bằng TIẾNG VIỆT. Nếu không có lỗi, trả issues = [].");
            sb.AppendLine();
            sb.AppendLine("[DỮ LIỆU CẦN KIỂM TRA - JSON]");
            sb.AppendLine(JsonSerializer.Serialize(payload, JsonOptions));
            sb.AppendLine();
            sb.AppendLine("[ĐỊNH DẠNG ĐẦU RA - CHỈ TRẢ VỀ JSON HỢP LỆ, KHÔNG MARKDOWN]");
            sb.AppendLine("{\"issues\":[{\"unitKey\":string,\"field\":string,\"severity\":\"Error|Warning|Info\",\"confidence\":number,\"reason\":string,\"suggestion\":string}]}");
            return sb.ToString();
        }

        // =====================================================================
        // HELPERS
        // =====================================================================
        private static string BuildSummary(string verdict, int errors, int warnings, int checkedCount, bool truncated, string? aiNote)
        {
            var sb = new StringBuilder();
            sb.Append(verdict switch
            {
                "NeedsRevision" => $"Cần chỉnh sửa: {errors} lỗi, {warnings} cảnh báo.",
                "PassedWithWarnings" => $"Không có lỗi bắt buộc, còn {warnings} cảnh báo cần xem lại.",
                _ => "Không phát hiện vấn đề nào.",
            });
            sb.Append($" Đã kiểm tra {checkedCount} mục.");
            if (truncated) sb.Append($" Nội dung quá lớn, chỉ kiểm tra {MaxUnits} mục đầu tiên.");
            if (aiNote is not null) sb.Append(' ').Append(aiNote);
            return sb.ToString();
        }

        private static ContentAiReviewResponse ToResponse(ContentAiReview e, List<ContentReviewIssue> issues, int checkedCount) => new()
        {
            ReviewId = e.Id,
            ContentType = e.ContentType.ToString(),
            ContentId = e.ContentId,
            Verdict = e.Verdict,
            Summary = e.Summary,
            ErrorCount = e.ErrorCount,
            WarningCount = e.WarningCount,
            CheckedItemCount = checkedCount,
            ReviewedAt = e.ReviewedAt,
            Issues = issues,
        };

        private static string? Get(ReviewUnit u, string field) => u.Fields.GetValueOrDefault(field);
        private static bool IsBlank(string? s) => string.IsNullOrWhiteSpace(s);

        private static string ReadString(JsonElement el, string name) =>
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? string.Empty : string.Empty;

        private static double ReadDouble(JsonElement el, string name, double fallback) =>
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var v)
                ? Math.Clamp(v, 0, 1) : fallback;

        private static string StripCodeFence(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "{}";
            var s = raw.Trim();
            if (!s.StartsWith("```")) return s;
            var first = s.IndexOf('\n');
            var last = s.LastIndexOf("```", StringComparison.Ordinal);
            if (first >= 0 && last > first) return s[(first + 1)..last].Trim();
            return first >= 0 ? s[(first + 1)..].Trim() : s;
        }
    }
}