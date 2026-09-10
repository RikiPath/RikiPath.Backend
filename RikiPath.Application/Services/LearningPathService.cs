using RikiPath.Domain.Entities;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Models;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;
using System.Net;
using System.Text.Json;

namespace RikiPath.Application.Services
{
    public class LearningPathService(
    IUnitOfWork unitOfWork,
    IAiLearningPathClient aiClient) : ILearningPathService
    {
        public async Task<ApiResponse<LearningPathResponse>> GenerateLearningPathAsync(int userId, CancellationToken cancellationToken)
        {
            UserAccount? user;
            try
            {
                user = await unitOfWork.UserAccounts.GetByIdAsync(userId);
            }
            catch (Exception ex)
            {
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể tải thông tin người dùng.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }

            if (user is null)
                return ApiResponse<LearningPathResponse>.NotFound($"Không tìm thấy user Id = {userId}.");

            string rawJson;
            try
            {
                var promptContext = await BuildPromptContextAsync(userId, user);
                // userId truyền vào đây để AiLearningPathClient kiểm tra/ghi nhận quota theo
                // IAiUsageQuotaService (giới hạn số lượt gọi + token/ngày, chống spam).
                rawJson = await aiClient.GenerateLearningPathJsonAsync(userId, promptContext.ToPromptText(), cancellationToken);
            }
            catch (AiQuotaExceededException ex)
            {
                // Vượt quota AI/ngày -> 429, khác với lỗi provider (502), để FE hiển thị đúng thông
                // báo "đã dùng hết lượt hôm nay" thay vì "AI đang lỗi, thử lại sau".
                return ApiResponse<LearningPathResponse>.Fail(
                    ex.Message,
                    HttpStatusCode.TooManyRequests,
                    errors: BuildDebugErrors(ex));
            }
            catch (Exception ex)
            {
                // Lỗi gọi AI provider (mất mạng, timeout, JSON không hợp lệ sau retry...) -> 502,
                // không phải lỗi của user
                return ApiResponse<LearningPathResponse>.Fail(
                    "Dịch vụ AI hiện không phản hồi, vui lòng thử lại sau.",
                    HttpStatusCode.BadGateway,
                    errors: BuildDebugErrors(ex));
            }

            (string Summary, List<string> FocusTopics, List<SuggestedLessonResponse> SuggestedLessons) aiResult;
            try
            {
                aiResult = ParseAiResponse(rawJson);
            }
            catch (Exception ex)
            {
                // AI trả về JSON sai schema -> vẫn là lỗi (không giả vờ thành công với dữ liệu rỗng)
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể đọc kết quả từ AI (sai định dạng JSON).",
                    HttpStatusCode.BadGateway,
                    errors: BuildDebugErrors(ex));
            }

            try
            {
                var suggestion = new LearningPathSuggestion
                {
                    UserId = userId,
                    GeneratedAt = DateTime.UtcNow,
                    SuggestionJson = rawJson,
                    Summary = aiResult.Summary,
                    IsViewed = false,
                };
                await unitOfWork.LearningPathSuggestions.AddAsync(suggestion);
                await unitOfWork.SaveChangesAsync();

                var result = new LearningPathResponse
                {
                    UserId = userId,
                    GeneratedAt = suggestion.GeneratedAt,
                    Summary = aiResult.Summary,
                    FocusTopics = aiResult.FocusTopics,
                    SuggestedLessons = aiResult.SuggestedLessons,
                };

                return ApiResponse<LearningPathResponse>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<LearningPathResponse>.Fail(
                    "Đã tạo được gợi ý nhưng lưu vào hệ thống thất bại.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        private async Task<LearningPathPromptContext> BuildPromptContextAsync(int userId, UserAccount user)
        {
            var lessonProgresses = await unitOfWork.LessonProgresses.GetByUserAsync(userId);
            var notebookEntries = await unitOfWork.VocabularyNoteEntries.GetByUserAsync(userId, vocabularyListId: null);
            var attempts = await unitOfWork.PracticeTestAttempts.GetByUserAsync(userId);

            var latestAttempt = attempts.FirstOrDefault(a => a.IsCompleted);
            IReadOnlyDictionary<string, double>? skillBreakdown = null;
            if (latestAttempt is not null)
            {
                var withBreakdown = await unitOfWork.PracticeTestAttempts.GetWithBreakdownAsync(latestAttempt.Id);
                skillBreakdown = withBreakdown?.SectionResults
                    .ToDictionary(r => r.PracticeTestSection.Title, r => r.ScorePercent);
            }

            return new LearningPathPromptContext(
                targetJlptLevel: user.TargetJlptLevel?.Name ?? "chưa xác định",
                lessonProgress: lessonProgresses.Select(p => (p.Lesson.Title, p.ProgressPercent)).ToList(),
                notebookWords: notebookEntries
                    .Select(n => n.VocabularyEntry?.Word ?? n.KanjiEntry?.Character ?? n.ManualWord ?? string.Empty)
                    .Where(w => !string.IsNullOrEmpty(w))
                    .ToList(),
                latestTestScorePercent: latestAttempt?.TotalScore,
                latestTestSkillBreakdown: skillBreakdown);
        }

        private static (string Summary, List<string> FocusTopics, List<SuggestedLessonResponse> SuggestedLessons) ParseAiResponse(string rawJson)
        {
            using var doc = JsonDocument.Parse(rawJson); // ném JsonException nếu sai format -> catch ở nơi gọi
            var root = doc.RootElement;

            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? string.Empty : string.Empty;

            var focusTopics = root.TryGetProperty("focusTopics", out var topics)
                ? topics.EnumerateArray().Select(t => t.GetString() ?? string.Empty).ToList()
                : new List<string>();

            var suggestedLessons = root.TryGetProperty("suggestedLessons", out var lessons)
                ? lessons.EnumerateArray().Select(l => new SuggestedLessonResponse
                {
                    Title = l.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                    Reason = l.TryGetProperty("reason", out var r) ? r.GetString() ?? string.Empty : string.Empty,
                }).ToList()
                : new List<SuggestedLessonResponse>();

            return (summary, focusTopics, suggestedLessons);
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