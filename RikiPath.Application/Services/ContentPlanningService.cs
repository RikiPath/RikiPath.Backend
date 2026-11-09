using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.ContentAuthoring;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.ContentAuthoring;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RikiPath.Application.Services
{
    /// <summary>
    /// Lớp điều phối Plan → Execute → Confirm đặt trên <see cref="IContentAuthoringService"/>.
    ///
    /// Nguyên tắc: AI chỉ làm việc "hiểu yêu cầu" (Plan) và "viết nội dung" (Execute từng việc). Mọi thứ còn lại do code
    /// quyết định: id cấp độ/kỹ năng/phần thi lấy từ danh mục thật trong DB, thứ tự phụ thuộc, giới hạn số lượng,
    /// liên kết giữa các nội dung, kiểm tra cấu trúc trước khi gửi duyệt.
    ///
    /// GIẢ ĐỊNH CẦN XÁC NHẬN (không có trong Entities.zip):
    ///  - ApiResponse có các thuộc tính IsSuccess, Result, StatusCode (HttpStatusCode), Message.
    ///  - unitOfWork có repo CertificateLevels, CertificateSections, LanguageSkills, Lessons, Kanjis, Vocabularies,
    ///    GrammarPatterns, ContentApprovalRequests; CertificateLevel có nav CertificateType (Name), SortOrder.
    ///  - ImportEntityType có các giá trị Kanji, Vocabulary, Lesson, PracticeExercise, MockTest và một giá trị cho ngữ pháp (Grammar/GrammarPattern).
    ///  - Tầng Apply (sau khi admin duyệt) xử lý các dòng của cùng BatchId THEO THỨ TỰ SourceRowNumber
    ///    (Kanji → Vocabulary → Grammar → Lesson → PracticeExercise → MockTest) và hiểu shape PayloadJson ở ConfirmPlanAsync,
    ///    đặc biệt các liên kết theo khóa tự nhiên (newKanji / newVocabulary / newGrammar / newLessonTitle).
    /// </summary>
    public class ContentPlanningService(
        IUnitOfWork unitOfWork,
        IAiContentAuthoringClient aiClient,
        IClaimService claimService,
        IContentAuthoringService authoringService,
        ILogger<ContentPlanningService> logger) : IContentPlanningService
    {
        private const int MaxPlanTasks = 8;
        private const int MaxAiAttempts = 2;
        private const int MaxContentCount = 30;
        private const int DefaultContentCount = 10;
        private const int DefaultPracticeQuestions = 5;
        private const int MaxPracticeQuestions = 20;
        private const int MaxMockSections = 6;
        private const int MaxQuestionsPerSection = 20;
        private const int MaxCatalogLessons = 100;
        private const int MaxDraftsInPrompt = 30;

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        // =====================================================================================
        // PROMPTS
        // =====================================================================================

        private const string PlanSystemPrompt = """
            Bạn là trợ lý LẬP KẾ HOẠCH cho content author của ứng dụng học tiếng Nhật (JLPT và các chứng chỉ tương tự).
            Nhiệm vụ: đọc yêu cầu tự do của author và chuyển thành danh sách công việc có cấu trúc.
            Bạn CHỈ lập kế hoạch, TUYỆT ĐỐI không sinh nội dung bài học/câu hỏi.

            Các loại công việc (type): Kanji, Vocabulary, Grammar, Lesson, PracticeExercise, MockTest.

            QUY TẮC:
            - Chỉ tạo công việc author THỰC SỰ yêu cầu. Không tự thêm việc.
            - Mỗi công việc có "prompt" riêng, tự đủ ý (chủ đề, cấp độ, lưu ý riêng của author), viết bằng tiếng Việt,
              để một trợ lý khác đọc riêng prompt đó vẫn hiểu cần làm gì.
            - Chỉ dùng id có trong DANH MỤC HỆ THỐNG. Không bịa id.
            - Kanji/Vocabulary/Grammar bắt buộc có certificateLevelId, HOẶC existingLessonId nếu author muốn bổ sung nội dung
              vào một bài học ĐÃ CÓ trong danh mục (khi đó cấp độ lấy theo bài học). Nếu kế hoạch có Lesson mới thì cấp độ
              được thừa hưởng từ Lesson đó.
            - count: số mục cần sinh (Kanji/Vocabulary/Grammar) hoặc số câu hỏi (PracticeExercise).
              Nếu author không nói rõ, dùng mặc định hợp lý (Kanji 10, Vocabulary 10, Grammar 3, PracticeExercise 5).
            - Lesson: bắt buộc certificateLevelId và languageSkillId. Tối đa 1 Lesson trong kế hoạch.
              Mọi Kanji/Vocabulary/Grammar trong cùng yêu cầu sẽ được gắn vào Lesson này.
            - PracticeExercise phải gắn với một bài học: nếu kế hoạch có Lesson thì đặt lessonTaskId = taskId của Lesson đó;
              nếu author nói tới một bài học ĐÃ CÓ trong danh mục thì đặt existingLessonId. Không đặt cả hai.
            - MockTest: bắt buộc certificateLevelId và mockSections (chọn certificateSectionId từ các phần thi CỦA ĐÚNG cấp độ đó,
              kèm languageSkillId phù hợp, questionCount mỗi phần).
            - Nếu thiếu thông tin BẮT BUỘC mà không thể suy ra từ yêu cầu hoặc danh mục (ví dụ không rõ cấp độ cho Lesson/MockTest),
              đặt clarifyingQuestion (đúng 1 câu hỏi tiếng Việt) và để tasks rỗng. Không đoán.
            - taskId đặt lần lượt "t1", "t2", ... không trùng nhau.

            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "summary": string (1-2 câu tiếng Việt tóm tắt kế hoạch),
              "clarifyingQuestion": string | null,
              "tasks": [ { "taskId": string, "type": "Kanji" | "Vocabulary" | "Grammar" | "Lesson" | "PracticeExercise" | "MockTest",
                "count": int, "prompt": string,
                "certificateLevelId": int | null, "languageSkillId": int | null,
                "existingLessonId": int | null, "lessonTaskId": string | null,
                "mockSections": [ { "certificateSectionId": int, "languageSkillId": int, "questionCount": int } ] | null } ] }
            """;

        private const string LessonMetaSystemPrompt = """
            Bạn là trợ lý hỗ trợ content author đặt tên và mô tả BÀI HỌC tiếng Nhật cho ứng dụng học JLPT.
            Bạn được cho yêu cầu của author và danh sách nội dung (Kanji/từ vựng/ngữ pháp) sẽ được dạy trong bài.
            Hãy đề xuất tiêu đề ngắn gọn, mô tả 1-3 câu nêu bài học dạy gì, và lý do ngắn gọn.
            Không đặt tiêu đề trùng với các bài học đã có (được liệt kê).
            BẮT BUỘC: viết 100% bằng TIẾNG VIỆT (từ tiếng Nhật được phép giữ nguyên).
            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "title": string, "description": string, "rationale": string }
            """;

        // =====================================================================================
        // BƯỚC 1 — PLAN
        // =====================================================================================

        public async Task<ApiResponse<ContentPlanResponse>> PlanContentAsync(
            PlanContentRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return ApiResponse<ContentPlanResponse>.Fail("Vui lòng nhập yêu cầu cho AI.");

            try
            {
                var userId = claimService.GetUserClaim().Id;
                var catalog = await LoadCatalogAsync(cancellationToken);

                var run = await RunAiWithRetryAsync<PlanParsed>(
                    userId, PlanSystemPrompt, BuildPlanUserPrompt(request.Prompt, catalog),
                    raw => TryParsePlan(raw, catalog),
                    "content plan", cancellationToken);

                if (run.QuotaMessage is not null)
                    return ApiResponse<ContentPlanResponse>.Fail(run.QuotaMessage, HttpStatusCode.TooManyRequests);
                if (run.Value is null)
                    return ApiResponse<ContentPlanResponse>.Fail(
                        $"AI lập kế hoạch không hợp lệ ({run.Error}). Vui lòng thử lại hoặc mô tả rõ hơn.", HttpStatusCode.BadGateway);

                return ApiResponse<ContentPlanResponse>.Success(new ContentPlanResponse
                {
                    Summary = run.Value.Summary,
                    ClarifyingQuestion = run.Value.Clarifying,
                    Tasks = run.Value.Tasks,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to plan content.");
                return ApiResponse<ContentPlanResponse>.Fail(
                    "Không thể lập kế hoạch lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        private sealed record CatalogLevel(int Id, string Code, string? TypeName);
        private sealed record CatalogSkill(int Id, string Name);
        private sealed record CatalogSection(int Id, string Name, int LevelId);
        private sealed record CatalogLesson(int Id, string Title, int LevelId, int SkillId);
        private sealed record Catalog(
            List<CatalogLevel> Levels, List<CatalogSkill> Skills,
            List<CatalogSection> Sections, List<CatalogLesson> Lessons);

        private async Task<Catalog> LoadCatalogAsync(CancellationToken cancellationToken)
        {
            // TODO: xác nhận tên repo và các thuộc tính (CertificateType.Name, SortOrder, CertificateSection.Name...).
            var levels = (await unitOfWork.CertificateLevels.Query()
                    .Where(l => !l.IsDeleted)
                    .OrderBy(l => l.SortOrder)
                    .Select(l => new { l.Id, l.Code, TypeName = l.CertificateType.Name })
                    .ToListAsync(cancellationToken))
                .Select(l => new CatalogLevel(l.Id, l.Code, l.TypeName)).ToList();

            var skills = (await unitOfWork.LanguageSkills.Query()
                    .Where(s => !s.IsDeleted)
                    .Select(s => new { s.Id, s.Name })
                    .ToListAsync(cancellationToken))
                .Select(s => new CatalogSkill(s.Id, s.Name)).ToList();

            var sections = (await unitOfWork.CertificateSections.Query()
                    .Where(s => !s.IsDeleted)
                    .Select(s => new { s.Id, s.Name, s.CertificateLevelId })
                    .ToListAsync(cancellationToken))
                .Select(s => new CatalogSection(s.Id, s.Name, s.CertificateLevelId)).ToList();

            var lessons = (await unitOfWork.Lessons.Query()
                    .Where(l => !l.IsDeleted)
                    .OrderByDescending(l => l.Id)
                    .Take(MaxCatalogLessons)
                    .Select(l => new { l.Id, l.Title, l.CertificateLevelId, l.LanguageSkillId })
                    .ToListAsync(cancellationToken))
                .Select(l => new CatalogLesson(l.Id, l.Title, l.CertificateLevelId, l.LanguageSkillId)).ToList();

            return new Catalog(levels, skills, sections, lessons);
        }

        private static string BuildPlanUserPrompt(string authorPrompt, Catalog catalog)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[YÊU CẦU CỦA CONTENT AUTHOR]: {authorPrompt.Trim()}");

            sb.AppendLine("\n[DANH MỤC HỆ THỐNG — chỉ được dùng id trong danh mục này]");
            sb.AppendLine("Cấp độ:");
            foreach (var l in catalog.Levels)
                sb.AppendLine($"- id={l.Id}: {l.TypeName} {l.Code}");

            sb.AppendLine("Kỹ năng:");
            foreach (var s in catalog.Skills)
                sb.AppendLine($"- id={s.Id}: {s.Name}");

            sb.AppendLine("Phần thi (mỗi phần thuộc đúng một cấp độ):");
            foreach (var s in catalog.Sections)
                sb.AppendLine($"- id={s.Id}: \"{s.Name}\" (thuộc cấp độ id={s.LevelId})");

            if (catalog.Lessons.Count > 0)
            {
                sb.AppendLine("Bài học đã có (mới nhất trước):");
                foreach (var l in catalog.Lessons)
                    sb.AppendLine($"- id={l.Id}: \"{l.Title}\" (cấp độ id={l.LevelId}, kỹ năng id={l.SkillId})");
            }

            return sb.ToString();
        }

        private sealed record PlanRawSection(int CertificateSectionId, int LanguageSkillId, int? QuestionCount);
        private sealed record PlanRawTask(
            string? TaskId, string? Type, int? Count, string? Prompt,
            int? CertificateLevelId, int? LanguageSkillId, int? ExistingLessonId, string? LessonTaskId,
            List<PlanRawSection>? MockSections);
        private sealed record PlanRaw(string? Summary, string? ClarifyingQuestion, List<PlanRawTask>? Tasks);
        private sealed record PlanParsed(string Summary, string? Clarifying, List<ContentPlanTask> Tasks);

        private static (PlanParsed? Value, string? Error) TryParsePlan(string rawJson, Catalog catalog)
        {
            PlanRaw? raw;
            try
            {
                raw = JsonSerializer.Deserialize<PlanRaw>(rawJson, JsonOpts);
            }
            catch (JsonException)
            {
                return (null, "JSON sai định dạng (có thể bị ngắt giữa chừng)");
            }

            if (raw is null)
                return (null, "không đọc được kế hoạch");

            var summary = raw.Summary?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(raw.ClarifyingQuestion) && (raw.Tasks is null || raw.Tasks.Count == 0))
                return (new PlanParsed(summary, raw.ClarifyingQuestion.Trim(), new List<ContentPlanTask>()), null);

            if (raw.Tasks is null || raw.Tasks.Count == 0)
                return (null, "kế hoạch không có công việc nào và cũng không có câu hỏi làm rõ");

            var tasks = new List<ContentPlanTask>();
            foreach (var t in raw.Tasks)
            {
                if (!Enum.TryParse<ContentTaskType>(t.Type, ignoreCase: true, out var type) || !Enum.IsDefined(type))
                    return (null, $"loại công việc không hợp lệ: {t.Type}");

                tasks.Add(new ContentPlanTask
                {
                    TaskId = t.TaskId?.Trim() ?? string.Empty,
                    Type = type,
                    Prompt = t.Prompt?.Trim() ?? string.Empty,
                    Count = t.Count ?? 0,
                    CertificateLevelId = t.CertificateLevelId,
                    LanguageSkillId = t.LanguageSkillId,
                    ExistingLessonId = t.ExistingLessonId,
                    LessonTaskId = string.IsNullOrWhiteSpace(t.LessonTaskId) ? null : t.LessonTaskId.Trim(),
                    MockSections = t.MockSections?.Select(s => new MockTestSectionPlan
                    {
                        CertificateSectionId = s.CertificateSectionId,
                        LanguageSkillId = s.LanguageSkillId,
                        QuestionCount = s.QuestionCount ?? 0,
                    }).ToList(),
                });
            }

            var error = ValidateTasks(tasks, catalog);
            if (error is not null)
                return (null, error);

            return (new PlanParsed(summary, null, tasks.OrderBy(t => (int)t.Type).ToList()), null);
        }

        /// <summary>
        /// Kiểm tra kế hoạch bằng code (dùng cho cả Plan lẫn Execute vì author có thể đã sửa tay).
        /// Chuẩn hóa luôn các giá trị (clamp số lượng, suy ra cấp độ/kỹ năng từ bài học trong kế hoạch).
        /// Trả về thông báo lỗi tiếng Việt, hoặc null nếu hợp lệ.
        /// </summary>
        private static string? ValidateTasks(List<ContentPlanTask> tasks, Catalog catalog)
        {
            if (tasks.Count == 0)
                return "kế hoạch không có công việc nào";
            if (tasks.Count > MaxPlanTasks)
                return $"kế hoạch có quá nhiều công việc ({tasks.Count}), tối đa {MaxPlanTasks}";
            if (tasks.Any(t => string.IsNullOrWhiteSpace(t.TaskId)))
                return "có công việc thiếu taskId";
            if (tasks.Select(t => t.TaskId).Distinct().Count() != tasks.Count)
                return "có taskId bị trùng nhau";
            if (tasks.Count(t => t.Type == ContentTaskType.Lesson) > 1)
                return "mỗi kế hoạch chỉ được có tối đa 1 bài học";

            var lessonTask = tasks.FirstOrDefault(t => t.Type == ContentTaskType.Lesson);

            // Lesson được xử lý trước để các Practice gắn vào nó thừa hưởng cấp độ/kỹ năng.
            foreach (var t in tasks.OrderBy(t => t.Type == ContentTaskType.Lesson ? 0 : 1))
            {
                var label = $"công việc {t.TaskId} ({t.Type})";

                if (!Enum.IsDefined(t.Type))
                    return $"{label}: loại công việc không hợp lệ";
                if (string.IsNullOrWhiteSpace(t.Prompt))
                    return $"{label}: thiếu yêu cầu chi tiết (prompt)";
                if (t.CertificateLevelId.HasValue && catalog.Levels.All(l => l.Id != t.CertificateLevelId.Value))
                    return $"{label}: cấp độ id={t.CertificateLevelId} không tồn tại";
                if (t.LanguageSkillId.HasValue && catalog.Skills.All(s => s.Id != t.LanguageSkillId.Value))
                    return $"{label}: kỹ năng id={t.LanguageSkillId} không tồn tại";

                switch (t.Type)
                {
                    case ContentTaskType.Kanji:
                    case ContentTaskType.Vocabulary:
                    case ContentTaskType.Grammar:
                        t.Count = Math.Clamp(t.Count <= 0 ? DefaultContentCount : t.Count, 1, MaxContentCount);
                        if (t.ExistingLessonId.HasValue)
                        {
                            // Bổ sung cho bài học ĐÃ CÓ: cấp độ lấy theo chính bài học đó.
                            var existingLesson = catalog.Lessons.FirstOrDefault(l => l.Id == t.ExistingLessonId.Value);
                            if (existingLesson is not null)
                            {
                                if (t.CertificateLevelId.HasValue && t.CertificateLevelId.Value != existingLesson.LevelId)
                                    return $"{label}: cấp độ không khớp với bài học id={existingLesson.Id}";
                                t.CertificateLevelId = existingLesson.LevelId;
                            }
                        }
                        else
                        {
                            // Nội dung mới của gói: thừa hưởng cấp độ từ Lesson trong kế hoạch (nếu có).
                            t.CertificateLevelId ??= lessonTask?.CertificateLevelId;
                        }
                        if (!t.CertificateLevelId.HasValue && !t.ExistingLessonId.HasValue)
                            return $"{label}: cần xác định cấp độ hoặc chọn một bài học";
                        break;

                    case ContentTaskType.Lesson:
                        if (!t.CertificateLevelId.HasValue) return $"{label}: cần xác định cấp độ";
                        if (!t.LanguageSkillId.HasValue) return $"{label}: cần xác định kỹ năng";
                        t.Count = 1;
                        break;

                    case ContentTaskType.PracticeExercise:
                        t.Count = Math.Clamp(t.Count <= 0 ? DefaultPracticeQuestions : t.Count, 1, MaxPracticeQuestions);
                        if (t.LessonTaskId is not null && t.ExistingLessonId.HasValue)
                            return $"{label}: chỉ được chọn một trong hai (bài học trong kế hoạch hoặc bài học đã có)";
                        if (t.LessonTaskId is not null)
                        {
                            if (lessonTask is null || lessonTask.TaskId != t.LessonTaskId)
                                return $"{label}: tham chiếu tới bài học không có trong kế hoạch";
                            t.LanguageSkillId ??= lessonTask.LanguageSkillId;
                            t.CertificateLevelId ??= lessonTask.CertificateLevelId;
                        }
                        else if (!t.ExistingLessonId.HasValue)
                        {
                            return $"{label}: bài luyện tập phải gắn với một bài học (trong kế hoạch hoặc bài học đã có)";
                        }
                        break;

                    case ContentTaskType.MockTest:
                        if (!t.CertificateLevelId.HasValue) return $"{label}: cần xác định cấp độ";
                        if (t.MockSections is null || t.MockSections.Count == 0)
                            return $"{label}: cần chọn ít nhất một phần thi";
                        if (t.MockSections.Count > MaxMockSections)
                            return $"{label}: tối đa {MaxMockSections} phần thi";
                        foreach (var s in t.MockSections)
                        {
                            var section = catalog.Sections.FirstOrDefault(x => x.Id == s.CertificateSectionId);
                            if (section is null || section.LevelId != t.CertificateLevelId.Value)
                                return $"{label}: phần thi id={s.CertificateSectionId} không tồn tại hoặc không thuộc cấp độ đã chọn";
                            if (catalog.Skills.All(x => x.Id != s.LanguageSkillId))
                                return $"{label}: kỹ năng id={s.LanguageSkillId} của phần thi không tồn tại";
                            s.QuestionCount = Math.Clamp(s.QuestionCount <= 0 ? 5 : s.QuestionCount, 1, MaxQuestionsPerSection);
                        }
                        break;
                }
            }

            return null;
        }

        // =====================================================================================
        // BƯỚC 2 — EXECUTE
        // =====================================================================================

        public async Task<ApiResponse<ExecuteContentPlanResponse>> ExecutePlanAsync(
            ExecuteContentPlanRequest request, CancellationToken cancellationToken)
        {
            if (request.Tasks is null || request.Tasks.Count == 0)
                return ApiResponse<ExecuteContentPlanResponse>.Fail("Kế hoạch trống, không có gì để chạy.");

            try
            {
                var userId = claimService.GetUserClaim().Id;
                var catalog = await LoadCatalogAsync(cancellationToken);

                var error = ValidateTasks(request.Tasks, catalog);
                if (error is not null)
                    return ApiResponse<ExecuteContentPlanResponse>.Fail($"Kế hoạch không hợp lệ: {error}.");

                var ordered = request.Tasks.OrderBy(t => (int)t.Type).ToList();

                var results = new List<ContentTaskResult>();
                var kanjiDrafts = new List<KanjiSuggestionItem>();
                var vocabularyDrafts = new List<VocabularySuggestionItem>();
                var grammarDrafts = new List<GrammarSuggestionItem>();
                var lessonResults = new Dictionary<string, ContentTaskResult>();
                var aborted = false;

                foreach (var task in ordered)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = new ContentTaskResult
                    {
                        TaskId = task.TaskId,
                        Type = task.Type,
                        CertificateLevelId = task.CertificateLevelId,
                        ExistingLessonId = task.ExistingLessonId,
                    };
                    results.Add(result);

                    if (aborted)
                    {
                        result.Status = ContentTaskStatus.Skipped;
                        result.Error = "Đã dừng do hết hạn mức gọi AI.";
                        continue;
                    }

                    void Fail(int status, string? message)
                    {
                        result.Status = ContentTaskStatus.Failed;
                        result.Error = string.IsNullOrWhiteSpace(message) ? "Không tạo được nội dung cho công việc này." : message;
                        if (status == (int)HttpStatusCode.TooManyRequests) aborted = true;
                    }

                    switch (task.Type)
                    {
                        case ContentTaskType.Kanji:
                            {
                                var r = await authoringService.SuggestKanjiAsync(
                                    new SuggestKanjiRequest
                                    {
                                        Prompt = task.Prompt,
                                        MaxItems = task.Count,
                                        LessonId = task.ExistingLessonId,
                                        CertificateLevelId = task.CertificateLevelId,
                                    }, cancellationToken);
                                if (r.IsSuccess && r.Result is not null)
                                {
                                    result.Status = ContentTaskStatus.Succeeded;
                                    result.Kanji = r.Result.Items;
                                    if (task.ExistingLessonId is null) kanjiDrafts.AddRange(r.Result.Items);
                                }
                                else Fail(Convert.ToInt32(r.StatusCode), GetErrorMessage(r));
                                break;
                            }

                        case ContentTaskType.Vocabulary:
                            {
                                var r = await authoringService.SuggestVocabularyAsync(
                                    new SuggestVocabularyRequest
                                    {
                                        Prompt = task.Prompt,
                                        MaxItems = task.Count,
                                        LessonId = task.ExistingLessonId,
                                        CertificateLevelId = task.CertificateLevelId,
                                    }, cancellationToken);
                                if (r.IsSuccess && r.Result is not null)
                                {
                                    result.Status = ContentTaskStatus.Succeeded;
                                    result.Vocabulary = r.Result.Items;
                                    if (task.ExistingLessonId is null) vocabularyDrafts.AddRange(r.Result.Items);
                                }
                                else Fail(Convert.ToInt32(r.StatusCode), GetErrorMessage(r));
                                break;
                            }

                        case ContentTaskType.Grammar:
                            {
                                var r = await authoringService.SuggestGrammarAsync(
                                    new SuggestGrammarRequest
                                    {
                                        Prompt = task.Prompt,
                                        MaxItems = task.Count,
                                        LessonId = task.ExistingLessonId,
                                        CertificateLevelId = task.CertificateLevelId,
                                    }, cancellationToken);
                                if (r.IsSuccess && r.Result is not null)
                                {
                                    result.Status = ContentTaskStatus.Succeeded;
                                    result.Grammar = r.Result.Items;
                                    if (task.ExistingLessonId is null) grammarDrafts.AddRange(r.Result.Items);
                                }
                                else Fail(Convert.ToInt32(r.StatusCode), GetErrorMessage(r));
                                break;
                            }

                        case ContentTaskType.Lesson:
                            {
                                var hasDrafts = kanjiDrafts.Count + vocabularyDrafts.Count + grammarDrafts.Count > 0;
                                if (hasDrafts)
                                {
                                    // Bài học gắn đúng các nội dung vừa sinh trong cùng kế hoạch — code gắn, AI chỉ đặt tên/mô tả.
                                    var built = await BuildLessonFromDraftsAsync(
                                        userId, task, catalog, kanjiDrafts, vocabularyDrafts, grammarDrafts, cancellationToken);
                                    if (built.Lesson is not null)
                                    {
                                        result.Status = ContentTaskStatus.Succeeded;
                                        result.Lesson = built.Lesson;
                                        lessonResults[task.TaskId] = result;
                                    }
                                    else Fail(built.Quota ? (int)HttpStatusCode.TooManyRequests : (int)HttpStatusCode.BadGateway, built.Error);
                                }
                                else
                                {
                                    // Không có nội dung mới: để AI chọn nội dung ĐÃ CÓ trong hệ thống.
                                    var r = await authoringService.SuggestLessonAsync(new SuggestLessonRequest
                                    {
                                        Prompt = task.Prompt,
                                        CertificateLevelId = task.CertificateLevelId!.Value,
                                        LanguageSkillId = task.LanguageSkillId!.Value,
                                    }, cancellationToken);
                                    if (r.IsSuccess && r.Result is not null)
                                    {
                                        result.Status = ContentTaskStatus.Succeeded;
                                        result.Lesson = r.Result;
                                        lessonResults[task.TaskId] = result;
                                    }
                                    else Fail(Convert.ToInt32(r.StatusCode), GetErrorMessage(r));
                                }
                                break;
                            }

                        case ContentTaskType.PracticeExercise:
                            {
                                if (task.LessonTaskId is not null)
                                {
                                    if (!lessonResults.TryGetValue(task.LessonTaskId, out var lessonResult) ||
                                        lessonResult.Status != ContentTaskStatus.Succeeded || lessonResult.Lesson is null)
                                    {
                                        result.Status = ContentTaskStatus.Skipped;
                                        result.Error = "Bỏ qua vì bài học liên quan chưa tạo được.";
                                        break;
                                    }

                                    var built = await BuildPracticeForPlannedLessonAsync(
                                        userId, task, lessonResult.Lesson, catalog,
                                        kanjiDrafts, vocabularyDrafts, grammarDrafts, cancellationToken);
                                    if (built.Draft is not null)
                                    {
                                        result.Status = ContentTaskStatus.Succeeded;
                                        result.PracticeExercise = built.Draft;
                                    }
                                    else Fail(built.Quota ? (int)HttpStatusCode.TooManyRequests : (int)HttpStatusCode.BadGateway, built.Error);
                                }
                                else
                                {
                                    var r = await authoringService.SuggestPracticeExerciseAsync(new SuggestPracticeExerciseRequest
                                    {
                                        Prompt = task.Prompt,
                                        LessonId = task.ExistingLessonId!.Value,
                                        LanguageSkillId = task.LanguageSkillId,
                                        QuestionCount = task.Count,
                                    }, cancellationToken);
                                    if (r.IsSuccess && r.Result is not null)
                                    {
                                        result.Status = ContentTaskStatus.Succeeded;
                                        result.PracticeExercise = new PlanPracticeExerciseDraft
                                        {
                                            ExistingLessonId = r.Result.LessonId,
                                            LanguageSkillId = r.Result.LanguageSkillId,
                                            Title = r.Result.Title,
                                            Description = r.Result.Description,
                                            Questions = r.Result.Questions,
                                        };
                                    }
                                    else Fail(Convert.ToInt32(r.StatusCode), GetErrorMessage(r));
                                }
                                break;
                            }

                        case ContentTaskType.MockTest:
                            {
                                var r = await authoringService.SuggestMockTestAsync(new SuggestMockTestRequest
                                {
                                    Prompt = task.Prompt,
                                    CertificateLevelId = task.CertificateLevelId!.Value,
                                    Sections = task.MockSections!,
                                }, cancellationToken);
                                if (r.IsSuccess && r.Result is not null)
                                {
                                    result.Status = ContentTaskStatus.Succeeded;
                                    result.MockTest = r.Result;
                                }
                                else Fail(Convert.ToInt32(r.StatusCode), GetErrorMessage(r));
                                break;
                            }
                    }
                }

                return ApiResponse<ExecuteContentPlanResponse>.Success(new ExecuteContentPlanResponse { Results = results });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute content plan.");
                return ApiResponse<ExecuteContentPlanResponse>.Fail(
                    "Không thể chạy kế hoạch lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        private sealed record LessonMetaRaw(string? Title, string? Description, string? Rationale);
        private sealed record LessonMetaParsed(string Title, string? Description, string? Rationale);

        private async Task<(SuggestLessonResponse? Lesson, string? Error, bool Quota)> BuildLessonFromDraftsAsync(
            int userId, ContentPlanTask task, Catalog catalog,
            List<KanjiSuggestionItem> kanjiDrafts, List<VocabularySuggestionItem> vocabularyDrafts,
            List<GrammarSuggestionItem> grammarDrafts, CancellationToken cancellationToken)
        {
            var level = catalog.Levels.First(l => l.Id == task.CertificateLevelId);
            var skill = catalog.Skills.First(s => s.Id == task.LanguageSkillId);

            var existingLessons = await unitOfWork.Lessons.Query()
                .Where(l => !l.IsDeleted && l.CertificateLevelId == level.Id && l.LanguageSkillId == skill.Id)
                .Select(l => new { l.Id, l.Title, l.SortOrder })
                .ToListAsync(cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine($"[YÊU CẦU CỦA CONTENT AUTHOR]: {task.Prompt}");
            sb.AppendLine($"- Cấp độ: {level.TypeName} {level.Code}; kỹ năng: {skill.Name}");
            AppendDraftContext(sb, kanjiDrafts, vocabularyDrafts, grammarDrafts);
            if (existingLessons.Count > 0)
            {
                sb.AppendLine("\n[CÁC BÀI HỌC ĐÃ CÓ — không đặt tiêu đề trùng]:");
                sb.AppendLine(string.Join("; ", existingLessons.Select(l => l.Title).Take(100)));
            }

            var run = await RunAiWithRetryAsync<LessonMetaParsed>(
                userId, LessonMetaSystemPrompt, sb.ToString(),
                raw =>
                {
                    LessonMetaRaw? parsed;
                    try { parsed = JsonSerializer.Deserialize<LessonMetaRaw>(raw, JsonOpts); }
                    catch (JsonException) { return (null, "JSON sai định dạng (có thể bị ngắt giữa chừng)"); }

                    if (parsed is null || string.IsNullOrWhiteSpace(parsed.Title))
                        return (null, "thiếu tiêu đề bài học");
                    if (ContentAuthoringService.LooksEnglish(parsed.Title) ||
                        ContentAuthoringService.LooksEnglish(parsed.Description) ||
                        ContentAuthoringService.LooksEnglish(parsed.Rationale))
                        return (null, "tiêu đề/mô tả lẫn tiếng Anh, phải viết hoàn toàn bằng tiếng Việt");

                    return (new LessonMetaParsed(parsed.Title.Trim(), parsed.Description?.Trim(), parsed.Rationale?.Trim()), null);
                },
                "lesson meta", cancellationToken);

            if (run.QuotaMessage is not null) return (null, run.QuotaMessage, true);
            if (run.Value is null) return (null, $"AI trả về kết quả không hợp lệ ({run.Error})", false);

            var duplicate = existingLessons.FirstOrDefault(l =>
                ContentAuthoringService.NormalizeKey(l.Title) == ContentAuthoringService.NormalizeKey(run.Value.Title));

            // Nội dung trùng với bản có sẵn trong DB -> liên kết tới Id thật; nội dung mới -> đánh dấu "IsInPlan".
            var lesson = new SuggestLessonResponse
            {
                CertificateLevelId = level.Id,
                LanguageSkillId = skill.Id,
                Title = run.Value.Title,
                Description = run.Value.Description,
                Rationale = run.Value.Rationale,
                SortOrder = existingLessons.Count == 0 ? 1 : existingLessons.Max(l => l.SortOrder) + 1,
                IsDuplicateTitle = duplicate is not null,
                ExistingLessonId = duplicate?.Id,
                Kanji = kanjiDrafts
                    .DistinctBy(k => k.Character)
                    .Select(k => new LessonLinkedItem
                    {
                        Key = k.Character,
                        ExistingId = k.IsDuplicate ? k.ExistingKanjiId : null,
                        IsInPlan = !k.IsDuplicate,
                    }).ToList(),
                Vocabulary = vocabularyDrafts
                    .DistinctBy(v => $"{v.Word}|{v.Reading}")
                    .Select(v => new LessonLinkedItem
                    {
                        Key = v.Word,
                        Reading = v.Reading,
                        ExistingId = v.IsDuplicate ? v.ExistingVocabularyId : null,
                        IsInPlan = !v.IsDuplicate,
                    }).ToList(),
                Grammar = grammarDrafts
                    .DistinctBy(g => ContentAuthoringService.NormalizeKey(g.Title))
                    .Select(g => new LessonLinkedItem
                    {
                        Key = g.Title,
                        ExistingId = g.IsDuplicate ? g.ExistingGrammarPatternId : null,
                        IsInPlan = !g.IsDuplicate,
                    }).ToList(),
            };

            return (lesson, null, false);
        }

        private async Task<(PlanPracticeExerciseDraft? Draft, string? Error, bool Quota)> BuildPracticeForPlannedLessonAsync(
            int userId, ContentPlanTask task, SuggestLessonResponse lesson, Catalog catalog,
            List<KanjiSuggestionItem> kanjiDrafts, List<VocabularySuggestionItem> vocabularyDrafts,
            List<GrammarSuggestionItem> grammarDrafts, CancellationToken cancellationToken)
        {
            var skillId = task.LanguageSkillId ?? lesson.LanguageSkillId;
            var skill = catalog.Skills.FirstOrDefault(s => s.Id == skillId);
            if (skill is null) return (null, "không tìm thấy kỹ năng", false);

            var level = catalog.Levels.FirstOrDefault(l => l.Id == lesson.CertificateLevelId);
            var isListening = ContentAuthoringService.IsListeningSkill(skill.Name);
            var count = task.Count;

            var sb = new StringBuilder();
            sb.AppendLine($"[YÊU CẦU CỦA CONTENT AUTHOR]: {task.Prompt}");
            sb.AppendLine($"- Soạn đúng {count} câu hỏi trắc nghiệm cho bài luyện tập.");
            sb.AppendLine($"- Kỹ năng: {skill.Name}{(isListening ? " (PHẦN NGHE: mỗi câu phải có audioScript)" : "")}");
            if (level is not null) sb.AppendLine($"- Cấp độ: {level.Code}");
            sb.AppendLine("- Cần cả \"title\" (tiêu đề bài luyện tập) và \"description\" (mô tả ngắn) bằng tiếng Việt.");
            sb.AppendLine("\n[BÀI HỌC]");
            sb.AppendLine($"- Tiêu đề: {lesson.Title}");
            if (!string.IsNullOrWhiteSpace(lesson.Description))
                sb.AppendLine($"- Mô tả: {lesson.Description}");
            AppendDraftContext(sb, kanjiDrafts, vocabularyDrafts, grammarDrafts);

            var run = await RunAiWithRetryAsync<ContentAuthoringService.ChoiceParsed>(
                userId, ContentAuthoringService.ChoiceQuestionSystemPrompt, sb.ToString(),
                raw => ContentAuthoringService.TryParseChoiceResponse(raw, count, isListening, requireTitle: true),
                "practice exercise (planned lesson)", cancellationToken);

            if (run.QuotaMessage is not null) return (null, run.QuotaMessage, true);
            if (run.Value is null) return (null, $"AI trả về kết quả không hợp lệ ({run.Error})", false);

            foreach (var q in run.Value.Questions) ContentAuthoringService.ShuffleOptions(q);

            return (new PlanPracticeExerciseDraft
            {
                NewLessonTitle = lesson.Title,
                LanguageSkillId = skill.Id,
                Title = run.Value.Title ?? string.Empty,
                Description = run.Value.Description,
                Questions = run.Value.Questions,
            }, null, false);
        }

        private static void AppendDraftContext(
            StringBuilder sb, List<KanjiSuggestionItem> kanji,
            List<VocabularySuggestionItem> vocabulary, List<GrammarSuggestionItem> grammar)
        {
            if (kanji.Count + vocabulary.Count + grammar.Count == 0) return;

            sb.AppendLine("\n[NỘI DUNG SẼ ĐƯỢC DẠY TRONG BÀI HỌC — bám sát các mục này]");
            if (kanji.Count > 0)
                sb.AppendLine("Kanji: " + string.Join(", ", kanji.Take(MaxDraftsInPrompt)
                    .Select(k => $"{k.Character}({k.OnYomi}/{k.KunYomi}: {k.Meaning})")));
            if (vocabulary.Count > 0)
                sb.AppendLine("Từ vựng: " + string.Join(", ", vocabulary.Take(MaxDraftsInPrompt)
                    .Select(v => $"{v.Word}[{v.Reading}]({v.Meaning})")));
            if (grammar.Count > 0)
                sb.AppendLine("Ngữ pháp: " + string.Join("; ", grammar.Take(MaxDraftsInPrompt)
                    .Select(g => $"{g.Title}: {g.Structure}")));
        }

        // =====================================================================================
        // BƯỚC 3 — CONFIRM (một BatchId cho cả gói)
        // =====================================================================================

        private sealed record PlanRow(ImportEntityType EntityType, string RowKey, object Payload, bool IsExisting, int? TargetId);

        public async Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmPlanAsync(
            ConfirmContentPlanRequest request, CancellationToken cancellationToken)
        {
            var kanjiGroups = request.KanjiGroups ?? new();
            var vocabularyGroups = request.VocabularyGroups ?? new();
            var grammarGroups = request.GrammarGroups ?? new();
            var lessons = request.Lessons ?? new();
            var practices = request.PracticeExercises ?? new();
            var mockTests = request.MockTests ?? new();

            var kanjiItems = kanjiGroups.SelectMany(g => g.Items ?? new()).ToList();
            var vocabularyItems = vocabularyGroups.SelectMany(g => g.Items ?? new()).ToList();
            var grammarItems = grammarGroups.SelectMany(g => g.Items ?? new()).ToList();

            if (kanjiItems.Count + vocabularyItems.Count + grammarItems.Count +
                lessons.Count + practices.Count + mockTests.Count == 0)
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Không có nội dung nào để gửi duyệt.");

            // ---- Kiểm tra dữ liệu nội dung (giống các hàm Confirm riêng lẻ) ----
            if (kanjiItems.Any(i => string.IsNullOrWhiteSpace(i.Character)))
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Có Kanji thiếu ký tự (character).");
            if (vocabularyItems.Any(i => string.IsNullOrWhiteSpace(i.Word) ||
                                         string.IsNullOrWhiteSpace(i.Reading) ||
                                         string.IsNullOrWhiteSpace(i.Meaning)))
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Có từ vựng thiếu từ, cách đọc hoặc nghĩa.");
            if (grammarItems.Any(i => string.IsNullOrWhiteSpace(i.Title) || string.IsNullOrWhiteSpace(i.Structure)))
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Có mẫu ngữ pháp thiếu tiêu đề hoặc cấu trúc.");

            // ---- Kiểm tra liên kết nội bộ trong gói ----
            var kanjiKeys = kanjiItems.Select(i => i.Character).ToHashSet();
            var vocabularyKeys = vocabularyItems.Select(i => $"{i.Word}|{i.Reading}").ToHashSet();
            var grammarKeys = grammarItems.Select(i => ContentAuthoringService.NormalizeKey(i.Title)).ToHashSet();
            var lessonTitles = lessons.Select(l => ContentAuthoringService.NormalizeKey(l.Title)).ToHashSet();

            foreach (var lesson in lessons)
            {
                if (string.IsNullOrWhiteSpace(lesson.Title))
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có bài học thiếu tiêu đề.");

                var missingKanji = (lesson.NewKanji ?? new()).FirstOrDefault(c => !kanjiKeys.Contains(c));
                if (missingKanji is not null)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail(
                        $"Bài học \"{lesson.Title}\" liên kết Kanji {missingKanji} không có trong danh sách gửi duyệt.");

                var missingVocab = (lesson.NewVocabulary ?? new()).FirstOrDefault(v => !vocabularyKeys.Contains($"{v.Word}|{v.Reading}"));
                if (missingVocab is not null)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail(
                        $"Bài học \"{lesson.Title}\" liên kết từ vựng {missingVocab.Word} không có trong danh sách gửi duyệt.");

                var missingGrammar = (lesson.NewGrammar ?? new()).FirstOrDefault(g => !grammarKeys.Contains(ContentAuthoringService.NormalizeKey(g)));
                if (missingGrammar is not null)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail(
                        $"Bài học \"{lesson.Title}\" liên kết ngữ pháp {missingGrammar} không có trong danh sách gửi duyệt.");
            }

            for (var i = 0; i < practices.Count; i++)
            {
                var p = practices[i];
                var label = $"Bài luyện tập \"{p.Title}\"";

                if (string.IsNullOrWhiteSpace(p.Title))
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có bài luyện tập thiếu tiêu đề.");
                if (p.ExistingLessonId.HasValue == !string.IsNullOrWhiteSpace(p.NewLessonTitle))
                    return ApiResponse<SubmitContentApprovalResponse>.Fail(
                        $"{label}: phải chọn đúng một trong hai (bài học đã có hoặc bài học mới trong gói).");
                if (!string.IsNullOrWhiteSpace(p.NewLessonTitle) &&
                    !lessonTitles.Contains(ContentAuthoringService.NormalizeKey(p.NewLessonTitle)))
                    return ApiResponse<SubmitContentApprovalResponse>.Fail(
                        $"{label}: bài học \"{p.NewLessonTitle}\" không có trong gói gửi duyệt.");
                if (p.Questions is null || p.Questions.Count == 0)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail($"{label}: chưa có câu hỏi nào.");

                var issues = ContentAuthoringService.CollectChoiceQuestionIssues(p.Questions, requireAudioScript: false);
                if (issues.Count > 0)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail($"{label}: {string.Join(" ", issues.Take(5))}");
            }

            foreach (var test in mockTests)
            {
                if (string.IsNullOrWhiteSpace(test.Title))
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có đề thi thiếu tiêu đề.");
                if (test.TimeLimitMinutes <= 0)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail($"Đề thi \"{test.Title}\": thời gian làm bài phải lớn hơn 0.");
                if (test.Sections is null || test.Sections.Count == 0 ||
                    test.Sections.Any(s => s.Questions is null || s.Questions.Count == 0))
                    return ApiResponse<SubmitContentApprovalResponse>.Fail(
                        $"Đề thi \"{test.Title}\" phải có ít nhất một phần thi và mỗi phần có câu hỏi.");

                for (var i = 0; i < test.Sections.Count; i++)
                {
                    var issues = ContentAuthoringService.CollectChoiceQuestionIssues(test.Sections[i].Questions, requireAudioScript: false);
                    if (issues.Count > 0)
                        return ApiResponse<SubmitContentApprovalResponse>.Fail(
                            $"Đề thi \"{test.Title}\" - phần thi #{i + 1}: {string.Join(" ", issues.Take(5))}");
                }
            }

            try
            {
                var userId = claimService.GetUserClaim().Id;

                // ---- Kiểm tra các Id tham chiếu có thật trong DB ----
                var levelIds = kanjiGroups.Select(g => g.CertificateLevelId)
                    .Concat(vocabularyGroups.Select(g => g.CertificateLevelId))
                    .Concat(grammarGroups.Select(g => g.CertificateLevelId))
                    .Concat(lessons.Select(l => (int?)l.CertificateLevelId))
                    .Concat(mockTests.Select(t => (int?)t.CertificateLevelId))
                    .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
                if (levelIds.Count > 0 &&
                    await unitOfWork.CertificateLevels.Query().CountAsync(l => !l.IsDeleted && levelIds.Contains(l.Id), cancellationToken) != levelIds.Count)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có cấp độ chứng chỉ không tồn tại.");

                var skillIds = lessons.Select(l => l.LanguageSkillId)
                    .Concat(practices.Select(p => p.LanguageSkillId))
                    .Concat(mockTests.SelectMany(t => t.Sections.Select(s => s.LanguageSkillId)))
                    .Distinct().ToList();
                if (skillIds.Count > 0 &&
                    await unitOfWork.LanguageSkills.Query().CountAsync(s => !s.IsDeleted && skillIds.Contains(s.Id), cancellationToken) != skillIds.Count)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có kỹ năng không tồn tại.");

                foreach (var test in mockTests)
                {
                    var sectionIds = test.Sections.Select(s => s.CertificateSectionId).Distinct().ToList();
                    var validSections = await unitOfWork.CertificateSections.Query()
                        .CountAsync(s => !s.IsDeleted && s.CertificateLevelId == test.CertificateLevelId && sectionIds.Contains(s.Id), cancellationToken);
                    if (validSections != sectionIds.Count)
                        return ApiResponse<SubmitContentApprovalResponse>.Fail(
                            $"Đề thi \"{test.Title}\": có phần thi không tồn tại hoặc không thuộc cấp độ đã chọn.");
                }

                var existingLessonIds = practices.Where(p => p.ExistingLessonId.HasValue)
                    .Select(p => p.ExistingLessonId!.Value)
                    .Concat(kanjiGroups.Where(g => g.LessonId.HasValue).Select(g => g.LessonId!.Value))
                    .Concat(vocabularyGroups.Where(g => g.LessonId.HasValue).Select(g => g.LessonId!.Value))
                    .Concat(grammarGroups.Where(g => g.LessonId.HasValue).Select(g => g.LessonId!.Value))
                    .Distinct().ToList();
                if (existingLessonIds.Count > 0 &&
                    await unitOfWork.Lessons.Query().CountAsync(l => !l.IsDeleted && existingLessonIds.Contains(l.Id), cancellationToken) != existingLessonIds.Count)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có bài học được gắn thêm nội dung không tồn tại.");

                var kanjiIds = lessons.SelectMany(l => l.KanjiIds ?? new()).Distinct().ToList();
                if (kanjiIds.Count > 0 &&
                    await unitOfWork.Kanjis.Query().CountAsync(k => !k.IsDeleted && kanjiIds.Contains(k.Id), cancellationToken) != kanjiIds.Count)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có Kanji liên kết không tồn tại.");
                var vocabularyIds = lessons.SelectMany(l => l.VocabularyIds ?? new()).Distinct().ToList();
                if (vocabularyIds.Count > 0 &&
                    await unitOfWork.Vocabularies.Query().CountAsync(v => !v.IsDeleted && vocabularyIds.Contains(v.Id), cancellationToken) != vocabularyIds.Count)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có từ vựng liên kết không tồn tại.");
                var grammarIds = lessons.SelectMany(l => l.GrammarPatternIds ?? new()).Distinct().ToList();
                if (grammarIds.Count > 0 &&
                    await unitOfWork.GrammarPatterns.Query().CountAsync(g => !g.IsDeleted && grammarIds.Contains(g.Id), cancellationToken) != grammarIds.Count)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có mẫu ngữ pháp liên kết không tồn tại.");

                var grammarEntityType = ContentAuthoringService.GrammarEntityType;
                if (grammarItems.Count > 0 && !grammarEntityType.HasValue)
                {
                    logger.LogError("ImportEntityType chưa có giá trị cho ngữ pháp (GrammarPattern/Grammar).");
                    return ApiResponse<SubmitContentApprovalResponse>.Fail(
                        "Hệ thống chưa cấu hình loại nội dung ngữ pháp, vui lòng liên hệ quản trị viên.",
                        HttpStatusCode.InternalServerError);
                }

                // ---- Dựng các dòng theo THỨ TỰ phụ thuộc: Kanji → Vocabulary → Grammar → Lesson → Practice → MockTest ----
                // TODO: xác nhận tên enum ImportEntityType.
                var planRows = new List<PlanRow>();

                foreach (var group in kanjiGroups)
                    foreach (var item in group.Items ?? new())
                        planRows.Add(new PlanRow(ImportEntityType.Kanji, item.Character, new
                        {
                            character = item.Character,
                            meaning = item.Meaning,
                            sinoVietnamese = item.SinoVietnamese,
                            onYomi = item.OnYomi,
                            kunYomi = item.KunYomi,
                            strokeCount = item.StrokeCount,
                            lessonId = group.LessonId,
                            certificateLevelId = group.CertificateLevelId,
                        }, item.IsDuplicate, item.ExistingKanjiId));

                foreach (var group in vocabularyGroups)
                    foreach (var item in group.Items ?? new())
                        planRows.Add(new PlanRow(ImportEntityType.Vocabulary, item.Word, new
                        {
                            word = item.Word,
                            reading = item.Reading,
                            meaning = item.Meaning,
                            exampleSentence = item.ExampleSentence,
                            exampleSentenceMeaning = item.ExampleSentenceMeaning,
                            lessonId = group.LessonId,
                            certificateLevelId = group.CertificateLevelId,
                        }, item.IsDuplicate, item.ExistingVocabularyId));

                foreach (var group in grammarGroups)
                    foreach (var item in group.Items ?? new())
                        planRows.Add(new PlanRow(grammarEntityType!.Value, item.Title, new
                        {
                            title = item.Title,
                            structure = item.Structure,
                            usageNotes = item.UsageNotes,
                            exampleSentence = item.ExampleSentence,
                            exampleSentenceMeaning = item.ExampleSentenceMeaning,
                            lessonId = group.LessonId,
                            certificateLevelId = group.CertificateLevelId,
                        }, item.IsDuplicate, item.ExistingGrammarPatternId));

                foreach (var lesson in lessons)
                    planRows.Add(new PlanRow(ImportEntityType.Lesson, lesson.Title.Trim(), new
                    {
                        title = lesson.Title.Trim(),
                        description = lesson.Description?.Trim(),
                        sortOrder = lesson.SortOrder,
                        certificateLevelId = lesson.CertificateLevelId,
                        languageSkillId = lesson.LanguageSkillId,
                        kanjiIds = (lesson.KanjiIds ?? new()).Distinct().ToList(),
                        vocabularyIds = (lesson.VocabularyIds ?? new()).Distinct().ToList(),
                        grammarPatternIds = (lesson.GrammarPatternIds ?? new()).Distinct().ToList(),
                        newKanji = (lesson.NewKanji ?? new()).Distinct().ToList(),
                        newVocabulary = lesson.NewVocabulary ?? new(),
                        newGrammar = (lesson.NewGrammar ?? new()).Distinct().ToList(),
                    }, false, null));

                foreach (var p in practices)
                    planRows.Add(new PlanRow(ImportEntityType.PracticeExercise, p.Title.Trim(), new
                    {
                        lessonId = p.ExistingLessonId,
                        newLessonTitle = string.IsNullOrWhiteSpace(p.NewLessonTitle) ? null : p.NewLessonTitle.Trim(),
                        languageSkillId = p.LanguageSkillId,
                        title = p.Title.Trim(),
                        description = p.Description?.Trim(),
                        questions = p.Questions,
                    }, false, null));

                foreach (var test in mockTests)
                    planRows.Add(new PlanRow(ImportEntityType.MockTest, test.Title.Trim(), test, false, null));

                var batchId = Guid.NewGuid();
                var now = DateTime.UtcNow;
                var sortOrder = 1;

                var rows = planRows.Select(r => new ContentApprovalRequest
                {
                    BatchId = batchId,
                    EntityType = r.EntityType,
                    // TODO: xác nhận lại đúng tên giá trị enum ImportRowAction (New/Modified).
                    Action = r.IsExisting ? ImportRowAction.Modified : ImportRowAction.New,
                    TargetEntityId = r.TargetId,
                    RowKey = r.RowKey,
                    PayloadJson = JsonSerializer.Serialize(r.Payload, JsonOpts),
                    SourceRowNumber = sortOrder++,
                    SourceFileName = "AI Content Assistant (Plan)",
                    // TODO: xác nhận lại đúng tên giá trị enum ContentApprovalStatus mặc định khi author vừa gửi.
                    Status = ContentApprovalStatus.Draft,
                    SubmittedByUserId = userId,
                    SubmittedAt = now,
                }).ToList();

                // TODO: nếu repo không có AddRangeAsync, đổi thành vòng lặp AddAsync.
                await unitOfWork.ContentApprovalRequests.AddRangeAsync(rows, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<SubmitContentApprovalResponse>.Success(new SubmitContentApprovalResponse
                {
                    BatchId = batchId,
                    SubmittedCount = rows.Count,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to submit content plan for approval.");
                return ApiResponse<SubmitContentApprovalResponse>.Fail(
                    "Không thể gửi duyệt lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        // =====================================================================================
        // SHARED
        // =====================================================================================

        /// <summary>
        /// Lấy thông báo lỗi từ ApiResponse. ApiResponse không có thuộc tính "Message" nên dò theo tên các thuộc tính
        /// thường gặp; nếu bạn biết đúng tên thuộc tính thì thay hàm này bằng truy cập trực tiếp.
        /// </summary>
        private static string? GetErrorMessage(object response)
        {
            var type = response.GetType();
            foreach (var name in new[] { "Message", "ErrorMessage", "Error", "Detail", "Title", "Errors" })
            {
                object? value;
                try { value = type.GetProperty(name)?.GetValue(response); }
                catch { continue; }

                switch (value)
                {
                    case string text when !string.IsNullOrWhiteSpace(text):
                        return text;
                    case System.Collections.Generic.IEnumerable<string> list:
                        var joined = string.Join(" ", list.Where(x => !string.IsNullOrWhiteSpace(x)));
                        if (!string.IsNullOrWhiteSpace(joined)) return joined;
                        break;
                }
            }
            return null;
        }

        private sealed record AiRunResult<T>(T? Value, string? Error, string? QuotaMessage) where T : class;

        /// <summary>Gọi AI + parse/validate, thử lại tối đa MaxAiAttempts lần, kèm lý do lần trước bị từ chối.</summary>
        private async Task<AiRunResult<T>> RunAiWithRetryAsync<T>(
            int userId, string systemPrompt, string userPrompt,
            Func<string, (T? Value, string? Error)> parseAndValidate,
            string label, CancellationToken cancellationToken) where T : class
        {
            string? lastError = null;

            for (var attempt = 1; attempt <= MaxAiAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentPrompt = lastError is null
                    ? userPrompt
                    : $"{userPrompt}\n\n[LƯU Ý] Lần trả lời trước bị từ chối vì: {lastError}. Hãy sửa lại.";

                string rawJson;
                try
                {
                    rawJson = ContentAuthoringService.StripCodeFence(await aiClient.GenerateContentJsonAsync(
                        userId, systemPrompt, currentPrompt, cancellationToken));
                }
                catch (AiQuotaExceededException ex)
                {
                    return new AiRunResult<T>(null, null, ex.Message);
                }

                var (value, error) = parseAndValidate(rawJson);
                if (value is not null && error is null)
                    return new AiRunResult<T>(value, null, null);

                lastError = error ?? "không đọc được kết quả";
                logger.LogWarning("AI {Label} rejected (attempt {Attempt}/{Max}): {Reason}. UserId={UserId}",
                    label, attempt, MaxAiAttempts, lastError, userId);
            }

            return new AiRunResult<T>(null, lastError, null);
        }
    }
}