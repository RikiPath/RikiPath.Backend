using Microsoft.Extensions.Logging;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Models;
using RikiPath.Application.Requests.LearningPath;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;
using RikiPath.Domain.Entities;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RikiPath.Application.Services
{
    public class LearningPathService(
        IUnitOfWork unitOfWork,
        IAiLearningPathClient aiClient,
        IClaimService claimService,
        ILogger<LearningPathService> logger) : ILearningPathService
    {
        // ===== Constants =====
        private const int MaxPlanDays = 90;           // Số ngày chi tiết tối đa AI sinh trong 1 lần
        private const int MaxAiAttempts = 2;          // Retry khi AI trả JSON sai / thiếu ngày / chọn bài không hợp lệ
        private const int MaxNotebookWords = 50;
        private const int MaxLessonProgressItems = 30;
        private const int MaxCatalogLessons = 200;    // Số bài học tối đa đưa vào prompt
        private const double CompletedPercent = 100;
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        /// <summary>Một bài học trong DB thuộc cấp độ mục tiêu, kèm tiến độ của người học.</summary>
        private sealed record CatalogLesson(int Id, string Title, double ProgressPercent);

        // =====================================================================
        // MAIN FLOW
        // Đăng ký -> chọn certificate type -> level -> ngày thi -> làm bài đánh giá
        // -> AI phân tích DỮ LIỆU TRONG DB (bài đánh giá, tiến độ, notebook, danh mục bài học)
        //    + ngày thi => nhận xét + lộ trình dùng đúng bài học có trong hệ thống
        // =====================================================================
        public async Task<ApiResponse<LearningPathResponse>> GenerateLearningPathAsync(
            GenerateLearningPathRequest request,
            CancellationToken cancellationToken)
        {
            // ---------- 1. Lấy user ----------
            int userId = 0;
            UserAccount? user;

            try
            {
                userId = claimService.GetUserClaim().Id;
                user = await unitOfWork.UserAccounts.GetByIdAsync(userId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to load user. UserId={UserId}", userId);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể tải thông tin người dùng.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }

            if (user is null)
                return ApiResponse<LearningPathResponse>.NotFound($"Không tìm thấy user Id = {userId}.");

            // ---------- 2. Mục tiêu học tập lấy từ HỒ SƠ user ----------
            var (certName, examDate) = ResolveLearningGoal(user);

            if (string.IsNullOrWhiteSpace(certName) || examDate is null || user.TargetCertificateLevelId is null)
            {
                return ApiResponse<LearningPathResponse>.Fail(
                    "Bạn cần chọn chứng chỉ, cấp độ và ngày dự định thi trước khi tạo lộ trình.",
                    HttpStatusCode.BadRequest);
            }

            DateTime today = (DateTime.UtcNow + VietnamOffset).Date;
            int daysUntilExam = (examDate.Value.Date - today).Days;

            if (daysUntilExam < 1)
            {
                return ApiResponse<LearningPathResponse>.Fail(
                    "Ngày dự định thi phải sau hôm nay. Vui lòng cập nhật ngày thi để tạo lộ trình.",
                    HttpStatusCode.BadRequest);
            }

            int planDays = Math.Min(daysUntilExam, MaxPlanDays);

            // ---------- 3. Đọc dữ liệu từ DB + kiểm tra đã làm bài đánh giá ----------
            string promptText;
            Dictionary<int, CatalogLesson> catalogById;
            try
            {
                var (promptContext, catalog) = await LoadLearnerDataAsync(
                    userId, user.TargetCertificateLevelId.Value, certName);

                if (!promptContext.LatestTestScorePercent.HasValue)
                {
                    return ApiResponse<LearningPathResponse>.Fail(
                        "Bạn cần hoàn thành bài kiểm tra đánh giá năng lực đầu vào trước khi hệ thống tạo lộ trình.",
                        HttpStatusCode.BadRequest);
                }

                if (catalog.Count == 0)
                {
                    return ApiResponse<LearningPathResponse>.Fail(
                        $"Hiện chưa có bài học nào cho cấp độ {certName} (hoặc bạn đã hoàn thành tất cả).",
                        HttpStatusCode.BadRequest);
                }

                catalogById = catalog.ToDictionary(c => c.Id);

                promptText = BuildPrompt(promptContext, catalog, certName, examDate.Value, today,
                    daysUntilExam, planDays, user.DailyStudyMinutes, user.StudyTimePreference);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to build prompt context. UserId={UserId}", userId);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể tải dữ liệu học tập của bạn.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }

            // ---------- 4. Gọi AI (retry khi sai định dạng / thiếu ngày / bài học không hợp lệ) ----------
            string rawJson = string.Empty;
            AiLearningPathResult? aiResult = null;
            string? lastError = null;

            try
            {
                for (int attempt = 1; attempt <= MaxAiAttempts; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    rawJson = StripCodeFence(
                        await aiClient.GenerateLearningPathJsonAsync(userId, promptText, cancellationToken));

                    try
                    {
                        var parsed = ParseAiResponse(rawJson);

                        // Chỉ giữ bài học có thật trong DB (chống AI bịa id), rồi mới validate
                        int dropped = NormalizeLessons(parsed, catalogById);
                        if (dropped > 0)
                            logger.LogWarning("AI referenced {Dropped} unknown lesson ids. UserId={UserId}", dropped, userId);

                        lastError = ValidateAiResult(parsed, planDays);
                        if (lastError is null)
                        {
                            aiResult = parsed;
                            break;
                        }
                    }
                    catch (JsonException ex)
                    {
                        lastError = "JSON sai định dạng";
                        logger.LogWarning(ex, "AI returned invalid JSON (attempt {Attempt}/{Max}). UserId={UserId}",
                            attempt, MaxAiAttempts, userId);
                    }

                    logger.LogWarning("AI result rejected (attempt {Attempt}/{Max}): {Reason}. UserId={UserId}",
                        attempt, MaxAiAttempts, lastError, userId);
                }
            }
            catch (AiQuotaExceededException ex)
            {
                return ApiResponse<LearningPathResponse>.Fail(ex.Message, HttpStatusCode.TooManyRequests, errors: BuildDebugErrors(ex));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "AI service failed. UserId={UserId}", userId);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Dịch vụ AI hiện không phản hồi, vui lòng thử lại sau.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }

            if (aiResult is null)
            {
                return ApiResponse<LearningPathResponse>.Fail(
                    $"AI trả về kết quả không hợp lệ ({lastError}). Vui lòng thử lại.", HttpStatusCode.BadGateway);
            }

            // ---------- 5. Lưu DB ----------
            try
            {
                var suggestion = new RecommendedLearningPath
                {
                    UserId = userId,
                    GeneratedAt = DateTime.UtcNow,
                    SuggestionJson = rawJson,
                    Summary = aiResult.Summary,
                    IsViewed = false,
                };
                await unitOfWork.RecommendedLearningPaths.AddAsync(suggestion);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<LearningPathResponse>.Success(new LearningPathResponse
                {
                    UserId = userId,
                    GeneratedAt = suggestion.GeneratedAt,
                    Summary = aiResult.Summary,
                    Strengths = aiResult.Strengths,
                    Weaknesses = aiResult.Weaknesses,
                    Feasibility = aiResult.Feasibility,
                    FocusTopics = aiResult.FocusTopics,
                    DailySchedules = aiResult.DailySchedules,
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to save learning path. UserId={UserId}", userId);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Đã tạo lộ trình nhưng lưu trữ thất bại, vui lòng thử lại.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        // =====================================================================
        // GOAL
        // YÊU CẦU: GetByIdAsync phải Include(TargetCertificateLevel).ThenInclude(CertificateType)
        // =====================================================================
        private static (string? CertName, DateTime? ExamDate) ResolveLearningGoal(UserAccount user)
        {
            var level = user.TargetCertificateLevel;

            string? typeName = level?.CertificateType?.Name;
            string? levelCode = level?.Code;

            string? certName = string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(levelCode)
                ? null
                : $"{typeName.Trim()} {levelCode.Trim()}";

            return (certName, user.TargetExamDate);
        }

        // =====================================================================
        // DỮ LIỆU TỪ DB: bài đánh giá + tiến độ + notebook + DANH MỤC BÀI HỌC của cấp độ mục tiêu
        // =====================================================================
        private async Task<(LearningPathPromptContext Context, List<CatalogLesson> Catalog)> LoadLearnerDataAsync(
            int userId, int certificateLevelId, string targetCertification)
        {
            var lessonProgresses = await unitOfWork.LessonProgresses.GetByUserAsync(userId);
            var notebookEntries = await unitOfWork.LearnerNoteEntries.GetByUserAsync(userId, vocabularyListId: null);
            var attempts = await unitOfWork.MockTestAttempts.GetByUserAsync(userId);

            // LƯU Ý: giả định repository trả attempts mới nhất trước.
            // Nếu phân biệt bài "đánh giá đầu vào" với mock test thường, lọc thêm tại đây.
            var latestAttempt = attempts.FirstOrDefault(a => a.IsCompleted);

            IReadOnlyDictionary<string, double>? skillBreakdown = null;
            if (latestAttempt is not null)
            {
                var withBreakdown = await unitOfWork.MockTestAttempts.GetWithBreakdownAsync(latestAttempt.Id);
                skillBreakdown = withBreakdown?.SectionResults
                    .GroupBy(r => r.MockTestSection.Title)
                    .ToDictionary(g => g.Key, g => g.Average(r => r.ScorePercent));
            }

            // ---- Danh mục bài học của cấp độ mục tiêu (nguồn duy nhất AI được phép chọn) ----
            // TODO: chỉnh tên repository/method cho khớp project; CHỈ lấy bài đã duyệt/published.
            var lessons = await unitOfWork.Lessons.GetByCertificateLevelAsync(certificateLevelId);

            var progressByLesson = lessonProgresses
                .GroupBy(p => p.LessonId)
                .ToDictionary(g => g.Key, g => g.Max(p => Convert.ToDouble(p.ProgressPercent)));

            // Bài đã hoàn thành 100% không đưa vào danh mục (tiết kiệm token, tránh học lại)
            var catalog = lessons
                .Select(l => new CatalogLesson(l.Id, l.Title, progressByLesson.GetValueOrDefault(l.Id)))
                .Where(c => c.ProgressPercent < CompletedPercent)
                .Take(MaxCatalogLessons)
                .ToList();

            var context = new LearningPathPromptContext(
                targetJlptLevel: targetCertification,
                lessonProgress: lessonProgresses
                    .Take(MaxLessonProgressItems)
                    .Select(p => (p.Lesson.Title, p.ProgressPercent))
                    .ToList(),
                notebookWords: notebookEntries
                    .Select(n => n.Vocabulary?.Word ?? n.Kanji?.Character ?? n.ManualWord ?? string.Empty)
                    .Where(w => !string.IsNullOrEmpty(w))
                    .Distinct()
                    .Take(MaxNotebookWords)
                    .ToList(),
                latestTestScorePercent: latestAttempt?.TotalScore,
                latestTestLanguageSkillBreakdown: skillBreakdown);

            return (context, catalog);
        }

        // =====================================================================
        // PROMPT
        // =====================================================================
        private static string BuildPrompt(
            LearningPathPromptContext promptContext,
            List<CatalogLesson> catalog,
            string certName,
            DateTime examDate,
            DateTime today,
            int daysUntilExam,
            int planDays,
            int dailyStudyMinutes,
            string? studyTimePreference)
        {
            string strategy = daysUntilExam switch
            {
                <= 30 => "CẤP TỐC (≤ 30 ngày): tập trung nội dung cốt lõi, dạng bài hay ra; luyện đề sớm và nhiều.",
                <= 90 => "CÂN BẰNG (31–90 ngày): vừa lấp lỗ hổng kỹ năng yếu, vừa luyện đề tăng dần về cuối.",
                _ => "NỀN TẢNG DÀI HẠN (> 90 ngày): xây nền vững từng kỹ năng, tăng cường độ dần, luyện đề ở giai đoạn cuối."
            };

            var sb = new StringBuilder();
            sb.AppendLine("[THÔNG TIN NGƯỜI HỌC VÀ KẾT QUẢ ĐÁNH GIÁ NĂNG LỰC (DỮ LIỆU THẬT TRONG HỆ THỐNG)]");
            sb.AppendLine(promptContext.ToPromptText());
            sb.AppendLine($"- Chứng chỉ mục tiêu: {certName}");
            sb.AppendLine($"- Hôm nay: {today:yyyy-MM-dd}");
            sb.AppendLine($"- Ngày dự định thi: {examDate:yyyy-MM-dd} (còn {daysUntilExam} ngày)");
            sb.AppendLine($"- Chiến lược ôn luyện: {strategy}");
            sb.AppendLine($"- Thời gian học mỗi ngày: {dailyStudyMinutes} phút (số bài mỗi ngày PHẢI vừa khung thời gian này).");
            if (!string.IsNullOrWhiteSpace(studyTimePreference))
                sb.AppendLine($"- Khung giờ học ưa thích: {studyTimePreference}.");

            sb.AppendLine("\n[DANH SÁCH BÀI HỌC CÓ SẴN TRONG HỆ THỐNG - CHỈ ĐƯỢC CHỌN BÀI TRONG DANH SÁCH NÀY]");
            foreach (var l in catalog)
                sb.AppendLine($"- id={l.Id} | {l.Title} | tiến độ hiện tại {l.ProgressPercent:0}%");

            sb.AppendLine("\n[YÊU CẦU 1 - NHẬN XÉT NĂNG LỰC]");
            sb.AppendLine("- Dựa trên điểm bài đánh giá và điểm từng kỹ năng, nêu điểm mạnh và điểm yếu cụ thể của người học.");
            sb.AppendLine($"- Đánh giá mức độ khả thi đạt {certName} trong {daysUntilExam} ngày còn lại (thực tế, không phóng đại), kèm điều kiện cần để đạt.");

            sb.AppendLine("\n[YÊU CẦU 2 - LỘ TRÌNH ÔN LUYỆN THEO NGÀY]");
            sb.AppendLine($"- Chia lộ trình thành ĐÚNG {planDays} NGÀY liên tục (day = 1 đến day = {planDays}), không bỏ sót, không trùng.");
            if (planDays < daysUntilExam)
                sb.AppendLine($"- Đây là GIAI ĐOẠN 1 ({planDays}/{daysUntilExam} ngày). Trong summary nêu định hướng cho {daysUntilExam - planDays} ngày còn lại.");
            sb.AppendLine("- Mỗi ngày chọn các bài học từ DANH SÁCH ở trên bằng id. TUYỆT ĐỐI không tự bịa bài học hay id không có trong danh sách.");
            sb.AppendLine("- Ưu tiên bài học thuộc kỹ năng/phần mà người học yếu nhất theo kết quả đánh giá; bài đang học dở (tiến độ > 0%) nên hoàn thành trước.");
            sb.AppendLine("- Nếu người học có từ trong sổ tay (notebook), có thể dành một số ngày/phần ôn lại các từ đó trong 'focus'.");
            sb.AppendLine("- Những ngày cuối trước kỳ thi: giảm nội dung mới, ưu tiên ôn lại và luyện đề.");

            sb.AppendLine("\n[ĐỊNH DẠNG ĐẦU RA]");
            sb.AppendLine("- Chỉ trả về JSON hợp lệ, KHÔNG kèm giải thích hay markdown, với các khóa:");
            sb.AppendLine("  summary (string - nhận xét tổng quan), strengths (string[]), weaknesses (string[]), feasibility (string),");
            sb.AppendLine("  focusTopics (string[]), dailySchedules (mảng: day:int, title:string, focus:string, lessonIds:int[] - id lấy từ danh sách bài học).");

            return sb.ToString();
        }

        // =====================================================================
        // AI RESPONSE: parse + normalize + validate
        // =====================================================================
        private sealed record AiLearningPathResult(
            string Summary,
            List<string> Strengths,
            List<string> Weaknesses,
            string Feasibility,
            List<string> FocusTopics,
            List<DailyScheduleResponse> DailySchedules);

        private static AiLearningPathResult ParseAiResponse(string rawJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    throw new JsonException("Root JSON phải là object.");

                var dailySchedules = new List<DailyScheduleResponse>();

                if (root.TryGetProperty("dailySchedules", out var schedules) && schedules.ValueKind == JsonValueKind.Array)
                {
                    foreach (var dayEl in schedules.EnumerateArray())
                    {
                        if (dayEl.ValueKind != JsonValueKind.Object) continue;

                        dailySchedules.Add(new DailyScheduleResponse
                        {
                            Day = ReadInt(dayEl, "day"),
                            Title = ReadString(dayEl, "title"),
                            Focus = ReadString(dayEl, "focus"),
                            LessonIds = ReadIntList(dayEl, "lessonIds"),   // id thô từ AI, sẽ được lọc ở NormalizeLessons
                            Lessons = new List<string>(),                   // tiêu đề bài học, điền ở NormalizeLessons
                        });
                    }
                }

                return new AiLearningPathResult(
                    Summary: ReadString(root, "summary"),
                    Strengths: ReadStringList(root, "strengths") ?? new List<string>(),
                    Weaknesses: ReadStringList(root, "weaknesses") ?? new List<string>(),
                    Feasibility: ReadString(root, "feasibility"),
                    FocusTopics: ReadStringList(root, "focusTopics") ?? new List<string>(),
                    DailySchedules: dailySchedules);
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            {
                throw new JsonException("Cấu trúc JSON từ AI không đúng kiểu dữ liệu mong đợi.", ex);
            }
        }

        /// <summary>
        /// Giữ lại đúng các bài học có trong DB, loại id AI tự bịa, gán tiêu đề thật.
        /// Trả về số id bị loại.
        /// </summary>
        private static int NormalizeLessons(AiLearningPathResult result, Dictionary<int, CatalogLesson> catalogById)
        {
            int dropped = 0;

            foreach (var day in result.DailySchedules)
            {
                var validIds = new List<int>();
                var titles = new List<string>();

                foreach (var id in (day.LessonIds ?? new List<int>()).Distinct())
                {
                    if (catalogById.TryGetValue(id, out var lesson))
                    {
                        validIds.Add(lesson.Id);
                        titles.Add(lesson.Title);
                    }
                    else
                    {
                        dropped++;
                    }
                }

                day.LessonIds = validIds;
                day.Lessons = titles;
            }

            return dropped;
        }

        /// <summary>Trả null nếu hợp lệ, ngược lại trả lý do bị từ chối.</summary>
        private static string? ValidateAiResult(AiLearningPathResult result, int expectedDays)
        {
            if (string.IsNullOrWhiteSpace(result.Summary))
                return "thiếu nhận xét tổng quan";

            if (result.DailySchedules.Count != expectedDays)
                return $"số ngày ({result.DailySchedules.Count}) khác yêu cầu ({expectedDays})";

            var days = result.DailySchedules.Select(d => d.Day).OrderBy(d => d).ToList();
            for (int i = 0; i < days.Count; i++)
            {
                if (days[i] != i + 1)
                    return "số thứ tự ngày không liên tục từ 1";
            }

            if (result.DailySchedules.All(d => d.LessonIds is null || d.LessonIds.Count == 0))
                return "không chọn được bài học hợp lệ nào từ hệ thống";

            return null;
        }

        // ----- JSON helpers: an toàn khi AI trả sai kiểu -----
        private static string ReadString(JsonElement el, string name) =>
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() ?? string.Empty
                : string.Empty;

        private static int ReadInt(JsonElement el, string name) =>
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v)
                ? v
                : 0;

        private static List<string>? ReadStringList(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
                return null;

            return p.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? string.Empty)
                .ToList();
        }

        private static List<int> ReadIntList(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
                return new List<int>();

            var list = new List<int>();
            foreach (var x in p.EnumerateArray())
            {
                if (x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out var v))
                    list.Add(v);
            }
            return list;
        }

        /// <summary>AI đôi khi bọc JSON trong ```json ... ```; bỏ lớp bọc đó.</summary>
        private static string StripCodeFence(string raw)
        {
            var s = raw.Trim();
            if (!s.StartsWith("```")) return s;

            int firstNewLine = s.IndexOf('\n');
            int lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
            return firstNewLine >= 0 && lastFence > firstNewLine
                ? s[(firstNewLine + 1)..lastFence].Trim()
                : s;
        }

        private static List<string> BuildDebugErrors(Exception ex)
        {
#if DEBUG
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
            return errors;
#else
            // Production: không lộ chi tiết exception ra client (đã có log phía server)
            return new List<string>();
#endif
        }
    }
}