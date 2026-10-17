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
        IClaimService claimService) : ILearningPathService
    {
        public async Task<ApiResponse<LearningPathResponse>> GenerateLearningPathAsync(
            GenerateLearningPathRequest request,
            CancellationToken cancellationToken)
        {
            int userId = 0;
            UserAccount? user;

            try
            {
                userId = claimService.GetUserClaim().Id;
                user = await unitOfWork.UserAccounts.GetByIdAsync(userId);
            }
            catch (Exception ex)
            {
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể tải thông tin người dùng.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }

            if (user is null)
                return ApiResponse<LearningPathResponse>.NotFound($"Không tìm thấy user Id = {userId}.");

            string promptText;
            try
            {
                int durationDays = request.DurationDays <= 0 ? 7 : request.DurationDays;

                // Xây dựng các yêu cầu tùy chọn nếu người dùng tích chọn
                var optionalComponents = new List<string>();
                if (request.IncludeVocabulary) optionalComponents.Add("Từ vựng (Vocabularies)");
                if (request.IncludeKanji) optionalComponents.Add("Chữ Hán (Kanjis)");
                if (request.IncludeGrammar) optionalComponents.Add("Cấu trúc ngữ pháp (Grammars)");

                string optionalRequirementsText = optionalComponents.Count > 0
                    ? $"- BẮT BUỘC phân bổ chi tiết các mục sau vào danh sách từng ngày: {string.Join(", ", optionalComponents)}."
                    : "- KHÔNG CẦN chia chi tiết từ vựng/kanji/ngữ pháp riêng lẻ (để trống hoặc null các mảng này nếu không được yêu cầu).";

                if (request.IsBeginner)
                {
                    var kanaList = await unitOfWork.KanaCharacters.GetAllAsync();

                    var hiraganaList = kanaList
                        .Where(k => k.Type == Domain.Enums.KanaCharacterType.Hiragana)
                        .Select(k => $"{k.Character} ({k.Romaji})")
                        .ToList();

                    var katakanaList = kanaList
                        .Where(k => k.Type == Domain.Enums.KanaCharacterType.Katakana)
                        .Select(k => $"{k.Character} ({k.Romaji})")
                        .ToList();

                    promptText = $"""
                        [THÔNG TIN BẮT BUỘC CHON NGƯỜI MỚI BẮT ĐẦU (isBeginner = true)]
                        - Thiết kế lộ trình chi tiết trong {durationDays} NÀY.
                        - Mục tiêu: Học bảng chữ cái Kana, nhớ mặt chữ, quy tắc nét vẽ (stroke order) và từ vỡ lòng.
                        - Hiragana trong hệ thống: {string.Join(", ", hiraganaList)}
                        - Katakana trong hệ thống: {string.Join(", ", katakanaList)}

                        [YÊU CẦU ĐỀ XUẤT LỘ TRÌNH THEO NGÀY]
                        Lên kế hoạch cụ thể từng ngày từ Ngày 1 đến Ngày {durationDays}:
                        1. Nhóm chữ cái cần học và quy tắc nét vẽ trong ngày.
                        2. Phản xạ âm Romaji và bài tập luyện viết.
                        {optionalRequirementsText}
                        """;
                }
                else
                {
                    var promptContext = await BuildPromptContextAsync(userId, user, request.TargetCertification);

                    var sb = new StringBuilder();
                    sb.AppendLine(promptContext.ToPromptText());
                    sb.AppendLine($"\n[YÊU CẦU ĐỀ XUẤT LỘ TRÌNH THEO NGÀY]");
                    sb.AppendLine($"- Hãy chia lộ trình thành đúng {durationDays} NGÀY liên tục (từ day = 1 đến day = {durationDays}).");
                    sb.AppendLine(optionalRequirementsText);

                    promptText = sb.ToString();
                }

                string rawJson = await aiClient.GenerateLearningPathJsonAsync(userId, promptText, cancellationToken);

                var aiResult = ParseAiResponse(rawJson);

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

                var result = new LearningPathResponse
                {
                    UserId = userId,
                    GeneratedAt = suggestion.GeneratedAt,
                    Summary = aiResult.Summary,
                    FocusTopics = aiResult.FocusTopics,
                    DailySchedules = aiResult.DailySchedules,
                };

                return ApiResponse<LearningPathResponse>.Success(result);
            }
            catch (AiQuotaExceededException ex)
            {
                return ApiResponse<LearningPathResponse>.Fail(ex.Message, HttpStatusCode.TooManyRequests, errors: BuildDebugErrors(ex));
            }
            catch (JsonException ex)
            {
                return ApiResponse<LearningPathResponse>.Fail("Không thể đọc kết quả từ AI (sai định dạng JSON).", HttpStatusCode.BadGateway, errors: BuildDebugErrors(ex));
            }
            catch (Exception ex)
            {
                return ApiResponse<LearningPathResponse>.Fail("Dịch vụ AI hiện không phản hồi hoặc lưu trữ thất bại, vui lòng thử lại sau.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        private async Task<LearningPathPromptContext> BuildPromptContextAsync(int userId, UserAccount user, string? targetCertification)
        {
            var lessonProgresses = await unitOfWork.LessonProgresses.GetByUserAsync(userId);
            var notebookEntries = await unitOfWork.LearnerNoteEntries.GetByUserAsync(userId, vocabularyListId: null);
            var attempts = await unitOfWork.MockTestAttempts.GetByUserAsync(userId);

            var latestAttempt = attempts.FirstOrDefault(a => a.IsCompleted);
            IReadOnlyDictionary<string, double>? skillBreakdown = null;
            if (latestAttempt is not null)
            {
                var withBreakdown = await unitOfWork.MockTestAttempts.GetWithBreakdownAsync(latestAttempt.Id);
                skillBreakdown = withBreakdown?.SectionResults
                    .ToDictionary(r => r.MockTestSection.Title, r => r.ScorePercent);
            }

            string targetLevel = !string.IsNullOrEmpty(targetCertification)
                ? targetCertification
                : "JLPT N3";

            return new LearningPathPromptContext(
                targetJlptLevel: targetLevel,
                lessonProgress: lessonProgresses.Select(p => (p.Lesson.Title, p.ProgressPercent)).ToList(),
                notebookWords: notebookEntries
                    .Select(n => n.Vocabulary?.Word ?? n.Kanji?.Character ?? n.ManualWord ?? string.Empty)
                    .Where(w => !string.IsNullOrEmpty(w))
                    .ToList(),
                latestTestScorePercent: latestAttempt?.TotalScore,
                latestTestLanguageSkillBreakdown: skillBreakdown);
        }

        private static (string Summary, List<string> FocusTopics, List<DailyScheduleResponse> DailySchedules) ParseAiResponse(string rawJson)
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? string.Empty : string.Empty;

            var focusTopics = root.TryGetProperty("focusTopics", out var topics)
                ? topics.EnumerateArray().Select(t => t.GetString() ?? string.Empty).ToList()
                : new List<string>();

            var dailySchedules = new List<DailyScheduleResponse>();

            if (root.TryGetProperty("dailySchedules", out var schedules) && schedules.ValueKind == JsonValueKind.Array)
            {
                foreach (var dayEl in schedules.EnumerateArray())
                {
                    var item = new DailyScheduleResponse
                    {
                        Day = dayEl.TryGetProperty("day", out var d) ? d.GetInt32() : 0,
                        Title = dayEl.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                        Focus = dayEl.TryGetProperty("focus", out var f) ? f.GetString() ?? string.Empty : string.Empty,
                        Lessons = dayEl.TryGetProperty("lessons", out var l)
                            ? l.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
                            : new List<string>()
                    };

                    if (dayEl.TryGetProperty("vocabularies", out var v) && v.ValueKind == JsonValueKind.Array)
                        item.Vocabularies = v.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList();

                    if (dayEl.TryGetProperty("kanjis", out var k) && k.ValueKind == JsonValueKind.Array)
                        item.Kanjis = k.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList();

                    if (dayEl.TryGetProperty("grammars", out var g) && g.ValueKind == JsonValueKind.Array)
                        item.Grammars = g.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList();

                    dailySchedules.Add(item);
                }
            }

            return (summary, focusTopics, dailySchedules);
        }

        private static List<string> BuildDebugErrors(Exception ex)
        {
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
#if DEBUG
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
#endif
            return errors;
        }
    }
}