using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Models;
using RikiPath.Application.Requests.LearningPath;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.PlacementTests;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RikiPath.Application.Services
{
    public class LearningPathService(
        IUnitOfWork unitOfWork,
        IAiLearningPathClient aiClient,
        IClaimService claimService,
        ILogger<LearningPathService> logger) : ILearningPathService
    {
        // ===== Constants =====
        private const int MaxPlanDays = 180;          // Tổng số ngày ôn tối đa khi gap kỹ năng lớn (>=3 cấp)
        private const int ShortPlanDays = 30;          // Số ngày lập lịch chi tiết tối đa cho AI mỗi lần (tránh phình token)
        private const int MaxAiAttempts = 2;           // Retry khi AI trả JSON sai / lẫn tiếng Anh / chọn bài không hợp lệ
        private const int MaxNotebookWords = 50;
        private const int MaxLessonProgressItems = 30;
        private const int MaxCatalogLessons = 200;     // Số bài học tối đa đưa vào prompt (tổng cả 3 kỹ năng)
        private const double MinimumPracticePassPercent = 70;
        private const double CompletedPercent = 100;
        private static readonly JsonSerializerOptions StoredPathJsonOptions =
            new(JsonSerializerDefaults.Web);

        public Func<DateTime> UtcNowProvider { get; init; } = static () => DateTime.UtcNow;

        /// <summary>Một bài học trong DB, gắn với 1 trong 3 nhóm kỹ năng thi JLPT và mức độ của bài học đó.</summary>
        private sealed record CatalogLesson(int Id, string Title, string LevelCode, double ProgressPercent, SkillGroup Group);

        /// <summary>
        /// 3 nhóm kỹ năng theo đúng cấu trúc thi JLPT thật:
        /// Kiến thức ngôn ngữ (Từ vựng + Ngữ pháp + Hán tự gộp lại), Đọc hiểu, Nghe hiểu.
        /// </summary>
        private enum SkillGroup { LanguageKnowledge, Reading, Listening }

        /// <summary>Kết quả đánh giá năng lực của người học cho MỘT nhóm kỹ năng, dựa trên bài Placement gần nhất.</summary>
        private sealed record SkillAssessment(
            SkillGroup Group,
            string Label,
            bool HasData,
            double ScorePercent,
            int TestLevelRank,
            string TestLevelCode,
            int EstimatedLevelRank,
            string EstimatedLevelCode,
            int TargetLevelRank,
            string TargetLevelCode,
            int GapLevels,
            bool MeetsTarget);

        // =====================================================================
        // MAIN FLOW
        // AI Personalized Learning Path requires a real completed Placement result.
        // Standard Roadmap is the separate, non-AI path for learners who skip Placement.
        // -> Hệ thống (C#) TỰ TÍNH mức năng lực từng kỹ năng, số ngày cần ôn, ngày thi khả thi.
        //    AI chỉ viết tóm tắt + chọn bài học (từ catalog đã lọc sẵn theo đúng kỹ năng/mức) cho từng ngày.
        // =====================================================================
        public async Task<ApiResponse<LearningPathResponse>> GenerateLearningPathAsync(
            GenerateLearningPathRequest request,
            CancellationToken cancellationToken)
            => await GenerateInternalAsync(request?.TargetExamDate, null, isPreview: false, cancellationToken);

        public async Task<ApiResponse<LearningPathResponse>> PreviewLearningPathAsync(
            PreviewLearningPathRequest request, CancellationToken cancellationToken)
            => await GenerateInternalAsync(request?.TargetExamDate, request, isPreview: true, cancellationToken);

        private async Task<ApiResponse<LearningPathResponse>> GenerateInternalAsync(
            DateTime? requestedExamDate,
            PreviewLearningPathRequest? preview,
            bool isPreview,
            CancellationToken cancellationToken)
        {
            // ---------- 1. Lấy user và mục tiêu học tập ----------
            int userId = 0;
            UserAccount? user;

            try
            {
                userId = claimService.GetUserClaim().Id;
                user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to load user. UserId={UserId}", userId);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể tải thông tin người dùng.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }

            if (user is null)
                return ApiResponse<LearningPathResponse>.NotFound($"Không tìm thấy user Id = {userId}.");

            var targetLevelId = preview?.TargetJlptLevelId ?? user.TargetCertificateLevelId;
            var startLevelId = preview?.StartingJlptLevelId ??
                user.StartingCertificateLevelId ?? user.TargetCertificateLevelId;
            if (!startLevelId.HasValue || !targetLevelId.HasValue)
                return ApiResponse<LearningPathResponse>.Fail(
                    "Bạn cần chọn cấp độ bắt đầu và mục tiêu JLPT.", HttpStatusCode.BadRequest,
                    errorCode: "LEARNING_PATH_GOAL_REQUIRED");

            var visibleLevels = await unitOfWork.CertificateLevels.GetLearnerVisibleJlptLevelsAsync(cancellationToken);
            var starting = visibleLevels.FirstOrDefault(level => level.Id == startLevelId.Value);
            var target = visibleLevels.FirstOrDefault(level => level.Id == targetLevelId.Value);
            if (starting is null || target is null || !JlptCurriculum.IsValidSpan(starting, target))
                return ApiResponse<LearningPathResponse>.Fail(
                    "Lộ trình chỉ hỗ trợ các cặp cấp độ bắt đầu/mục tiêu mà hệ thống đã cấu hình.",
                    HttpStatusCode.BadRequest, errorCode: "LEARNING_PATH_LEVEL_UNSUPPORTED");

            // Standard Roadmap remains available without Placement; an existing AI path
            // must only be replaced through preview and explicit confirmation.
            if (!isPreview && await unitOfWork.RecommendedLearningPaths.GetActiveByUserIdAsync(
                    userId, cancellationToken) is not null)
                return ApiResponse<LearningPathResponse>.Fail(
                    "Đã có lộ trình hoạt động; hãy xem trước rồi xác nhận thay đổi.",
                    HttpStatusCode.Conflict, errorCode: "PATH_PREVIEW_REQUIRED");

            var certName = $"JLPT {target.Code}";
            var examDate = requestedExamDate ?? user.TargetExamDate;
            var utcNow = BusinessDateHelper.EnsureUtc(UtcNowProvider());
            if (examDate.HasValue && !BusinessDateHelper.IsFutureExamDate(examDate.Value, utcNow))
            {
                return ApiResponse<LearningPathResponse>.Fail(
                    "Ngày dự định thi phải sau hôm nay. Vui lòng cập nhật ngày thi để tạo lộ trình.",
                    HttpStatusCode.BadRequest);
            }

            DateTime today = BusinessDateHelper.GetVietnamToday(utcNow);
            DateTime? normalizedExamDate = examDate.HasValue
                ? BusinessDateHelper.NormalizeExamDate(examDate.Value)
                : null;
            int daysUntilExam = normalizedExamDate.HasValue
                ? BusinessDateHelper.GetDaysUntilExam(normalizedExamDate.Value, utcNow)
                : 0;

            // ---------- 2. Bắt buộc phải có kết quả Placement để đánh giá riêng từng kỹ năng ----------
            PlacementTestResultResponse? placementForPlan;
            try
            {
                placementForPlan = await LoadLatestPlacementResultAsync(userId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to load latest Placement result. UserId={UserId}", userId);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể tải kết quả đánh giá năng lực gần nhất.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }

            if (placementForPlan is null)
                return ApiResponse<LearningPathResponse>.Fail(
                    "Bạn cần hoàn thành bài Placement Test hợp lệ trước khi tạo lộ trình AI.",
                    HttpStatusCode.BadRequest, errorCode: "LEARNING_PATH_PLACEMENT_REQUIRED");

            // ---------- 3. Đánh giá RIÊNG từng kỹ năng (Kiến thức ngôn ngữ / Đọc hiểu / Nghe hiểu) ----------
            var targetRank = JlptCurriculum.Rank(target.Code);
            var codeByRank = visibleLevels
                .GroupBy(level => JlptCurriculum.Rank(level.Code))
                .ToDictionary(group => group.Key, group => group.First().Code);

            var assessments = ComputeSkillAssessments(placementForPlan, targetRank, target.Code, codeByRank);

            // Số ngày ôn cần thiết + ngày thi khả thi hay không: TÍNH BẰNG C#, không để AI tự suy luận lại.
            var recommendedStudyDays = ComputeRecommendedStudyDays(assessments);
            bool? isExamDateRealistic = normalizedExamDate.HasValue
                ? daysUntilExam >= recommendedStudyDays
                : null;
            DateTime? suggestedExamDate = normalizedExamDate.HasValue && isExamDateRealistic == false
                ? today.AddDays(recommendedStudyDays + 7) // +1 tuần đệm như yêu cầu
                : null;

            // Lịch chi tiết ngày-qua-ngày chỉ cần lập cho tối đa ShortPlanDays ngày gần nhất,
            // KHÔNG bị cắt ngắn theo daysUntilExam nữa (tránh lệch số ngày khuyến nghị vs lịch chi tiết).
            int planDays = Math.Max(1, Math.Min(recommendedStudyDays, ShortPlanDays));

            var horizon = recommendedStudyDays <= 45 ? "short_term" : "long_term";

            // ---------- 4. Tải catalog bài học THEO TỪNG KỸ NĂNG, đúng khoảng mức cần ôn ----------
            string promptText;
            Dictionary<int, CatalogLesson> catalogById;
            List<CatalogLesson> catalog;
            try
            {
                var learnerData = await LoadLearnerDataAsync(userId, visibleLevels, assessments);
                catalog = learnerData.Catalog;

                if (catalog.Count == 0)
                {
                    return ApiResponse<LearningPathResponse>.Fail(
                        $"Hiện chưa có bài học nào phù hợp để ôn tập cho {certName} (hoặc bạn đã hoàn thành tất cả).",
                        HttpStatusCode.BadRequest, errorCode: "LEARNING_PATH_NO_ELIGIBLE_LESSONS");
                }

                catalogById = catalog.ToDictionary(c => c.Id);

                // DEBUG: xác nhận catalog thực tế theo từng kỹ năng trước khi gửi AI.
                logger.LogWarning(
                    "LearningPath catalog loaded. UserId={UserId}, CatalogCount={Count}, ByGroup={ByGroup}",
                    userId, catalog.Count,
                    string.Join(" | ", catalog.GroupBy(c => c.Group).Select(g => $"{g.Key}:{g.Count()}")));

                promptText = BuildPrompt(
                    assessments, catalog, learnerData.LessonProgress, learnerData.NotebookWords,
                    certName, normalizedExamDate, today, daysUntilExam, planDays,
                    recommendedStudyDays, isExamDateRealistic, suggestedExamDate,
                    user.DailyStudyMinutes, user.StudyTimePreference);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to build prompt context. UserId={UserId}", userId);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể tải dữ liệu học tập của bạn.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }

            // ---------- 5. Gọi AI (retry khi sai định dạng / lẫn tiếng Anh / chọn bài không hợp lệ) ----------
            AiLearningPathResult? aiResult = null;
            string? lastError = null;

            try
            {
                for (int attempt = 1; attempt <= MaxAiAttempts; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string currentPrompt = lastError is null
                        ? promptText
                        : $"{promptText}\n\n[LƯU Ý QUAN TRỌNG] Lần trả lời trước bị từ chối vì: {lastError}. Hãy sửa lại và trả về ĐÚNG {planDays} ngày, 100% tiếng Việt.";

                    string rawJson = StripCodeFence(
                        await aiClient.GenerateLearningPathJsonAsync(userId, currentPrompt, cancellationToken));

                    try
                    {
                        var parsed = ParseAiResponse(rawJson);
                        int dropped = NormalizeLessons(parsed, catalogById);
                        if (dropped > 0)
                            logger.LogWarning("AI referenced {Dropped} unknown lesson ids. UserId={UserId}", dropped, userId);

                        lastError = ValidateAiResult(parsed, planDays, catalogById.Count);
                        if (lastError is null)
                        {
                            aiResult = parsed;
                            break;
                        }
                    }
                    catch (JsonException ex)
                    {
                        lastError = "JSON sai định dạng (có thể do bị ngắt giữa chừng)";
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

            // ---------- 6. Ghép nội dung: số liệu + nhận xét do C# tính (chuẩn, tiếng Việt), AI chỉ góp summary/lịch ----------
            var (strengths, weaknesses) = BuildSkillNarrative(assessments);
            var feasibility = BuildFeasibilityText(
                assessments, target.Code, normalizedExamDate.HasValue, daysUntilExam,
                recommendedStudyDays, isExamDateRealistic, suggestedExamDate);

            // ---------- 7. Save DRAFT or first ACTIVE in a learner-row-locked transaction ----------
            try
            {
                var generatedAt = BusinessDateHelper.EnsureUtc(UtcNowProvider());
                var response = new LearningPathResponse
                {
                    UserId = userId,
                    GeneratedAt = generatedAt,
                    Status = isPreview ? "DRAFT" : "ACTIVE",
                    Horizon = horizon,
                    PlanDurationDays = planDays,
                    TargetExamDate = normalizedExamDate,
                    DaysUntilExam = normalizedExamDate.HasValue ? daysUntilExam : null,
                    HasTargetExamDate = normalizedExamDate.HasValue,
                    StartingJlptLevelId = starting.Id,
                    TargetJlptLevelId = target.Id,
                    PreviewExpiresAt = isPreview ? generatedAt.AddHours(24) : null,
                    ConfirmedAt = isPreview ? null : generatedAt,
                    Summary = aiResult.Summary,
                    Strengths = strengths,
                    Weaknesses = weaknesses,
                    Feasibility = feasibility,
                    IsExamDateRealistic = isExamDateRealistic,
                    RecommendedStudyDays = recommendedStudyDays,
                    SuggestedExamDate = suggestedExamDate,
                    FocusTopics = aiResult.FocusTopics,
                    DailySchedules = aiResult.DailySchedules,
                };
                RefreshDayAvailability(response);
                ApiResponse<LearningPathResponse>? conflict = null;
                await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
                {
                    var lockedUser = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(
                        userId, transactionCancellationToken);
                    if (lockedUser is null)
                    {
                        conflict = ApiResponse<LearningPathResponse>.NotFound("Không tìm thấy người dùng.");
                        return;
                    }
                    var current = await unitOfWork.RecommendedLearningPaths.GetActiveByUserIdAsync(
                        userId, transactionCancellationToken);
                    if ((!isPreview && current is not null) ||
                        (!isPreview && (lockedUser.StartingCertificateLevelId ?? lockedUser.TargetCertificateLevelId) != starting.Id) ||
                        lockedUser.TargetCertificateLevelId != target.Id && !isPreview)
                    {
                        conflict = ApiResponse<LearningPathResponse>.Fail(
                            "Hồ sơ hoặc lộ trình đã thay đổi; vui lòng tải lại.",
                            HttpStatusCode.Conflict, errorCode: "PATH_PREVIEW_REQUIRED");
                        return;
                    }

                    var suggestion = new RecommendedLearningPath
                    {
                        UserId = userId,
                        GeneratedAt = generatedAt,
                        Version = await unitOfWork.RecommendedLearningPaths.GetNextVersionAsync(
                            userId, transactionCancellationToken),
                        Status = isPreview ? LearningPathStatus.Draft : LearningPathStatus.Active,
                        StartingCertificateLevelId = starting.Id,
                        TargetCertificateLevelId = target.Id,
                        BaseActivePathId = current?.Id,
                        PreviewExpiresAt = response.PreviewExpiresAt,
                        ConfirmedAt = response.ConfirmedAt,
                        SuggestionJson = JsonSerializer.Serialize(response, StoredPathJsonOptions),
                        Summary = aiResult.Summary,
                        IsViewed = false,
                    };
                    await unitOfWork.RecommendedLearningPaths.AddAsync(suggestion, transactionCancellationToken);
                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                    response.PathId = suggestion.Id;
                    response.Version = suggestion.Version;
                    response.BaseActivePathId = current?.Id;
                }, cancellationToken);

                if (conflict is not null)
                    return conflict;

                return ApiResponse<LearningPathResponse>.Success(response);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to save learning path. UserId={UserId}", userId);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Đã tạo lộ trình nhưng lưu trữ thất bại, vui lòng thử lại.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<LearningPathResponse?>> GetLatestLearningPathAsync(
            CancellationToken cancellationToken)
        {
            var userId = 0;
            try
            {
                userId = claimService.GetUserClaim().Id;
                var stored = await unitOfWork.RecommendedLearningPaths.GetLatestByUserIdAsync(
                    userId,
                    cancellationToken);
                if (stored is null)
                    return ApiResponse<LearningPathResponse?>.Success(null);
                var response = await RestoreStoredPathAsync(stored, cancellationToken);
                return ApiResponse<LearningPathResponse?>.Success(response);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restore latest learning path. UserId={UserId}", userId);
                return ApiResponse<LearningPathResponse?>.Fail(
                    "Không thể tải lộ trình học gần nhất.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public Task<ApiResponse<LearningPathResponse>> StartActiveDayAsync(
            int day, CancellationToken cancellationToken)
            => UpdateActiveDayAsync(day, complete: false, cancellationToken);

        public Task<ApiResponse<LearningPathResponse>> CompleteActiveDayAsync(
            int day, CancellationToken cancellationToken)
            => UpdateActiveDayAsync(day, complete: true, cancellationToken);

        private async Task<ApiResponse<LearningPathResponse>> UpdateActiveDayAsync(
            int day, bool complete, CancellationToken cancellationToken)
        {
            if (day <= 0)
                return ApiResponse<LearningPathResponse>.Fail(
                    "Ngày trong lộ trình không hợp lệ.", HttpStatusCode.BadRequest);

            var userId = 0;
            try
            {
                userId = claimService.GetUserClaim().Id;
                var stored = await unitOfWork.RecommendedLearningPaths.GetActiveByUserIdAsync(
                    userId, cancellationToken);
                if (stored is null)
                    return ApiResponse<LearningPathResponse>.NotFound("Bạn chưa có lộ trình đang học.");

                var response = await RestoreStoredPathAsync(stored, cancellationToken);
                var daySchedule = response.DailySchedules.FirstOrDefault(item => item.Day == day);
                if (daySchedule is null)
                    return ApiResponse<LearningPathResponse>.NotFound($"Không tìm thấy ngày {day} trong lộ trình.");

                RefreshDayAvailability(response);
                if (complete)
                {
                    if (daySchedule.IsCompleted)
                        return ApiResponse<LearningPathResponse>.Success(response);
                    if (!daySchedule.IsStarted || !daySchedule.CanStart)
                        return ApiResponse<LearningPathResponse>.Fail(
                            "Hãy bắt đầu ngày học này trước khi đánh dấu hoàn thành.",
                            HttpStatusCode.Conflict, errorCode: "LEARNING_PATH_DAY_NOT_STARTED");

                    var hasPassedExercise = daySchedule.LessonIds.Count > 0 &&
                        await unitOfWork.PracticeExerciseAttempts.Query()
                            .Join(unitOfWork.PracticeExercises.Query(),
                                attempt => attempt.PracticeExerciseId,
                                exercise => exercise.Id,
                                (attempt, exercise) => new { attempt, exercise })
                            .AnyAsync(item =>
                                !item.attempt.IsDeleted &&
                                item.attempt.UserId == userId &&
                                item.attempt.IsCompleted &&
                                daySchedule.StartedAt.HasValue &&
                                item.attempt.StartedAt >= daySchedule.StartedAt.Value &&
                                item.attempt.ScorePercent >= MinimumPracticePassPercent &&
                                !item.exercise.IsDeleted &&
                                daySchedule.LessonIds.Contains(item.exercise.LessonId),
                                cancellationToken);
                    if (!hasPassedExercise)
                        return ApiResponse<LearningPathResponse>.Fail(
                            $"Hãy hoàn thành một Practice Exercise thuộc lesson của ngày này với điểm tối thiểu {MinimumPracticePassPercent:0}% để mở ngày tiếp theo.",
                            HttpStatusCode.Conflict, errorCode: "LEARNING_PATH_PRACTICE_PASS_REQUIRED");

                    daySchedule.IsCompleted = true;
                    daySchedule.CompletedAt = BusinessDateHelper.EnsureUtc(UtcNowProvider());
                }
                else
                {
                    if (!daySchedule.CanStart)
                        return ApiResponse<LearningPathResponse>.Fail(
                            "Hãy hoàn thành ngày trước đó để mở ngày học này.",
                            HttpStatusCode.Conflict, errorCode: "LEARNING_PATH_DAY_LOCKED");

                    daySchedule.IsStarted = true;
                    daySchedule.StartedAt ??= BusinessDateHelper.EnsureUtc(UtcNowProvider());
                }

                RefreshDayAvailability(response);
                stored.SuggestionJson = JsonSerializer.Serialize(response, StoredPathJsonOptions);
                unitOfWork.RecommendedLearningPaths.Update(stored);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<LearningPathResponse>.Success(response);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update learning path day. UserId={UserId}, Day={Day}, Complete={Complete}",
                    userId, day, complete);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể cập nhật tiến độ ngày học.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        private static void RefreshDayAvailability(LearningPathResponse response)
        {
            response.DailySchedules ??= [];
            var schedules = response.DailySchedules.OrderBy(item => item.Day).ToList();
            var previousDayCompleted = true;
            for (var index = 0; index < schedules.Count; index++)
            {
                var schedule = schedules[index];
                schedule.IsCompleted |= schedule.CompletedAt.HasValue;
                schedule.IsStarted |= schedule.StartedAt.HasValue;
                schedule.CanStart = !schedule.IsCompleted && previousDayCompleted;
                previousDayCompleted = schedule.IsCompleted;
            }
        }

        public async Task<ApiResponse<IReadOnlyList<LearningPathResponse>>> GetHistoryAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var stored = await unitOfWork.RecommendedLearningPaths.GetByUserIdAsync(userId);
                var history = new List<LearningPathResponse>();
                foreach (var item in stored.Where(item => item.Status is LearningPathStatus.Active or LearningPathStatus.Superseded)
                             .OrderByDescending(item => item.Version).ThenByDescending(item => item.Id))
                    history.Add(await RestoreStoredPathAsync(item, cancellationToken));
                return ApiResponse<IReadOnlyList<LearningPathResponse>>.Success(history);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load path history");
                return ApiResponse<IReadOnlyList<LearningPathResponse>>.Fail(
                    "Không thể tải lịch sử lộ trình.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<LearningPathResponse>> ConfirmPreviewAsync(
            int previewId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                ApiResponse<LearningPathResponse>? outcome = null;
                await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    var user = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, ct);
                    if (user is null)
                    {
                        outcome = ApiResponse<LearningPathResponse>.NotFound("Không tìm thấy người dùng.");
                        return;
                    }
                    var draft = await unitOfWork.RecommendedLearningPaths.GetOwnedByIdAsync(userId, previewId, ct);
                    if (draft is null)
                    {
                        outcome = ApiResponse<LearningPathResponse>.NotFound("Không tìm thấy bản xem trước.");
                        return;
                    }
                    if (draft.ConfirmedAt.HasValue && draft.Status is LearningPathStatus.Active or LearningPathStatus.Superseded)
                    {
                        outcome = ApiResponse<LearningPathResponse>.Success(
                            await RestoreStoredPathAsync(draft, ct));
                        return;
                    }
                    if (draft.Status != LearningPathStatus.Draft ||
                        !draft.PreviewExpiresAt.HasValue ||
                        BusinessDateHelper.EnsureUtc(UtcNowProvider()) >= draft.PreviewExpiresAt.Value)
                    {
                        outcome = ApiResponse<LearningPathResponse>.Fail(
                            "Bản xem trước đã hết hiệu lực; vui lòng tạo lại.",
                            HttpStatusCode.Conflict, errorCode: "PATH_PREVIEW_STALE");
                        return;
                    }
                    var current = await unitOfWork.RecommendedLearningPaths.GetActiveByUserIdAsync(userId, ct);
                    if (current?.Id != draft.BaseActivePathId)
                    {
                        outcome = ApiResponse<LearningPathResponse>.Fail(
                            "Lộ trình hiện tại đã thay đổi; vui lòng xem trước lại.",
                            HttpStatusCode.Conflict, errorCode: "PATH_PREVIEW_STALE");
                        return;
                    }

                    if (current is not null)
                    {
                        current.Status = LearningPathStatus.Superseded;
                        unitOfWork.RecommendedLearningPaths.Update(current);
                        await unitOfWork.SaveChangesAsync(ct);
                    }
                    draft.Status = LearningPathStatus.Active;
                    draft.ConfirmedAt = BusinessDateHelper.EnsureUtc(UtcNowProvider());
                    draft.PreviewExpiresAt = null;
                    unitOfWork.RecommendedLearningPaths.Update(draft);
                    user.StartingCertificateLevelId = draft.StartingCertificateLevelId;
                    user.TargetCertificateLevelId = draft.TargetCertificateLevelId;
                    unitOfWork.UserAccounts.Update(user);
                    await unitOfWork.SaveChangesAsync(ct);
                    outcome = ApiResponse<LearningPathResponse>.Success(
                        await RestoreStoredPathAsync(draft, ct));
                }, cancellationToken);
                return outcome!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to confirm learning path preview. PreviewId={PreviewId}", previewId);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể kích hoạt lộ trình; lộ trình trước vẫn được giữ.",
                    HttpStatusCode.Conflict, errorCode: "PATH_ACTIVATION_FAILED");
            }
        }

        public async Task<ApiResponse<LearningPathResponse>> CancelPreviewAsync(
            int previewId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var draft = await unitOfWork.RecommendedLearningPaths.GetOwnedByIdAsync(
                    userId, previewId, cancellationToken);
                if (draft is null)
                    return ApiResponse<LearningPathResponse>.NotFound("Không tìm thấy bản xem trước.");
                if (draft.Status is LearningPathStatus.Active or LearningPathStatus.Superseded)
                    return ApiResponse<LearningPathResponse>.Fail(
                        "Không thể hủy lộ trình đã xác nhận.", HttpStatusCode.Conflict,
                        errorCode: "PATH_ALREADY_CONFIRMED");
                if (draft.Status == LearningPathStatus.Draft)
                {
                    draft.Status = LearningPathStatus.Cancelled;
                    unitOfWork.RecommendedLearningPaths.Update(draft);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                return ApiResponse<LearningPathResponse>.Success(
                    await RestoreStoredPathAsync(draft, cancellationToken));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to cancel learning path preview. PreviewId={PreviewId}", previewId);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể hủy bản xem trước.", HttpStatusCode.InternalServerError);
            }
        }

        private async Task<LearningPathResponse> RestoreStoredPathAsync(
            RecommendedLearningPath stored, CancellationToken cancellationToken)
        {
            var response = JsonSerializer.Deserialize<LearningPathResponse>(stored.SuggestionJson, StoredPathJsonOptions);
            if (response is null || response.UserId == 0 || response.GeneratedAt == default)
            {
                // Phục hồi dữ liệu cũ (trước khi migrate sang schema mới): stored.SuggestionJson khi đó
                // chỉ là JSON thô do AI trả về (schema cũ), không phải LearningPathResponse đầy đủ.
                // Các trường Strengths/Weaknesses/Feasibility không khôi phục được từ bản ghi quá cũ này.
                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(stored.UserId, cancellationToken);
                if (user?.TargetCertificateLevelId is null)
                    throw new InvalidOperationException("Legacy path has no target level.");
                var lessons = await unitOfWork.Lessons.GetByCertificateLevelAsync(
                    user.TargetCertificateLevelId.Value, cancellationToken);
                var catalog = lessons.ToDictionary(lesson => lesson.Id,
                    lesson => new CatalogLesson(lesson.Id, lesson.Title, string.Empty, 0, SkillGroup.LanguageKnowledge));
                var legacy = ParseAiResponse(stored.SuggestionJson);
                NormalizeLessons(legacy, catalog);
                response = new LearningPathResponse
                {
                    Summary = legacy.Summary,
                    Strengths = new List<string>(),
                    Weaknesses = new List<string>(),
                    Feasibility = string.Empty,
                    FocusTopics = legacy.FocusTopics,
                    DailySchedules = legacy.DailySchedules,
                };
            }
            response.UserId = stored.UserId;
            response.PathId = stored.Id;
            response.Version = stored.Version;
            response.Status = stored.Status.ToString().ToUpperInvariant();
            response.GeneratedAt = stored.GeneratedAt;
            response.StartingJlptLevelId = stored.StartingCertificateLevelId;
            response.TargetJlptLevelId = stored.TargetCertificateLevelId;
            response.BaseActivePathId = stored.BaseActivePathId;
            response.PreviewExpiresAt = stored.PreviewExpiresAt;
            response.ConfirmedAt = stored.ConfirmedAt;
            RefreshDayAvailability(response);
            return response;
        }

        // =====================================================================
        // ĐÁNH GIÁ TỪNG KỸ NĂNG (xác định bằng công thức cố định, không qua AI)
        // =====================================================================

        private static string SkillGroupLabel(SkillGroup group) => group switch
        {
            SkillGroup.Reading => "Kỹ năng đọc hiểu",
            SkillGroup.Listening => "Kỹ năng nghe hiểu",
            _ => "Kiến thức ngôn ngữ (Từ vựng - Ngữ pháp - Hán tự)",
        };

        /// <summary>
        /// Phân loại một LanguageSkill (dữ liệu do admin cấu hình, ví dụ "Từ vựng", "Ngữ pháp", "Hán tự",
        /// "Đọc hiểu", "Nghe hiểu") vào 1 trong 3 nhóm kỹ năng thi JLPT thật. Dò theo từ khóa (không phân
        /// biệt hoa/thường) để không phụ thuộc cứng vào đúng chuỗi ký tự trong DB.
        /// </summary>
        private static SkillGroup ClassifySkillGroup(string? skillName)
        {
            var name = (skillName ?? string.Empty).ToLowerInvariant();
            if (name.Contains("đọc") || name.Contains("doc") || name.Contains("reading"))
                return SkillGroup.Reading;
            if (name.Contains("nghe") || name.Contains("listening"))
                return SkillGroup.Listening;
            return SkillGroup.LanguageKnowledge; // Từ vựng, Ngữ pháp, Hán tự/Kanji, ...
        }

        /// <summary>
        /// Ước tính mức JLPT hiện tại của từng kỹ năng dựa trên % điểm phần thi tương ứng trong bài
        /// Placement gần nhất (đề thi ở mức nào thì ước tính không thể vượt quá mức đó, vì chưa được
        /// kiểm chứng ở mức cao hơn):
        ///   điểm >= 80%  -> giữ nguyên mức đề thi
        ///   điểm 60-79%  -> thấp hơn đề thi 1 cấp
        ///   điểm 40-59%  -> thấp hơn đề thi 2 cấp
        ///   điểm &lt; 40%    -> thấp hơn đề thi 3 cấp
        /// </summary>
        private static List<SkillAssessment> ComputeSkillAssessments(
            PlacementTestResultResponse placement,
            int targetRank,
            string targetCode,
            Dictionary<int, string> codeByRank)
        {
            var testLevelRank = JlptCurriculum.Rank(placement.AssessedJlptLevelName);
            var testLevelCode = placement.AssessedJlptLevelName;

            var scoreByGroup = placement.Sections
                .GroupBy(section => ClassifySkillGroup(
                    string.IsNullOrWhiteSpace(section.LanguageSkillName) ? section.SectionTitle : section.LanguageSkillName))
                .ToDictionary(group => group.Key, group => group.Average(section => section.Score));

            var result = new List<SkillAssessment>();
            foreach (var group in new[] { SkillGroup.LanguageKnowledge, SkillGroup.Reading, SkillGroup.Listening })
            {
                var hasData = scoreByGroup.TryGetValue(group, out var scorePercent);
                int estimatedRank;
                if (!hasData)
                {
                    // Chưa có dữ liệu của phần thi này trong bài Placement -> ước tính thận trọng:
                    // thấp hơn mức đề thi 1 cấp, để lộ trình vẫn ôn đủ mà không bỏ sót.
                    estimatedRank = Math.Max(1, testLevelRank - 1);
                    scorePercent = 0;
                }
                else if (scorePercent >= 80) estimatedRank = testLevelRank;
                else if (scorePercent >= 60) estimatedRank = Math.Max(1, testLevelRank - 1);
                else if (scorePercent >= 40) estimatedRank = Math.Max(1, testLevelRank - 2);
                else estimatedRank = Math.Max(1, testLevelRank - 3);

                estimatedRank = Math.Min(estimatedRank, testLevelRank);
                var estimatedCode = codeByRank.GetValueOrDefault(estimatedRank, testLevelCode);
                var gap = Math.Max(0, targetRank - estimatedRank);

                result.Add(new SkillAssessment(
                    group, SkillGroupLabel(group), hasData, scorePercent,
                    testLevelRank, testLevelCode, estimatedRank, estimatedCode,
                    targetRank, targetCode, gap, gap <= 0));
            }
            return result;
        }

        /// <summary>Số ngày ôn khuyến nghị = tra theo gap (số cấp độ còn thiếu) của kỹ năng YẾU NHẤT.</summary>
        private static readonly Dictionary<int, int> StudyDaysByGapLevel = new()
        {
            [0] = 14,   // Đã đạt cả 3 kỹ năng -> chỉ cần ôn củng cố nhẹ
            [1] = 60,
            [2] = 120,
            // gap >= 3 -> MaxPlanDays (180)
        };

        private static int ComputeRecommendedStudyDays(IReadOnlyList<SkillAssessment> assessments)
        {
            var maxGap = assessments.Count == 0 ? 0 : assessments.Max(a => a.GapLevels);
            return StudyDaysByGapLevel.TryGetValue(maxGap, out var days) ? days : MaxPlanDays;
        }

        /// <summary>Điểm mạnh / điểm yếu theo từng kỹ năng — viết sẵn bằng tiếng Việt, không qua AI.</summary>
        private static (List<string> Strengths, List<string> Weaknesses) BuildSkillNarrative(
            IReadOnlyList<SkillAssessment> assessments)
        {
            var strengths = new List<string>();
            var weaknesses = new List<string>();
            foreach (var a in assessments)
            {
                if (!a.HasData)
                {
                    weaknesses.Add($"{a.Label}: chưa có dữ liệu từ bài đánh giá năng lực; hệ thống tạm ước tính ở mức {a.EstimatedLevelCode} để lên kế hoạch ôn tập an toàn.");
                    continue;
                }
                if (a.MeetsTarget)
                    strengths.Add($"{a.Label}: đạt {a.ScorePercent:0}% ở đề {a.TestLevelCode} — đã đạt năng lực tương đương mục tiêu {a.TargetLevelCode}.");
                else
                    weaknesses.Add($"{a.Label}: đạt {a.ScorePercent:0}% ở đề {a.TestLevelCode} — ước tính năng lực hiện tại khoảng {a.EstimatedLevelCode}, còn cách mục tiêu {a.TargetLevelCode} khoảng {a.GapLevels} cấp độ.");
            }
            return (strengths, weaknesses);
        }

        /// <summary>Đoạn đánh giá khả thi + đề xuất ngày thi — viết sẵn bằng tiếng Việt, không qua AI.</summary>
        private static string BuildFeasibilityText(
            IReadOnlyList<SkillAssessment> assessments,
            string targetCode,
            bool hasExamDate,
            int daysUntilExam,
            int recommendedStudyDays,
            bool? isExamDateRealistic,
            DateTime? suggestedExamDate)
        {
            var weakest = assessments.Where(a => a.GapLevels > 0).OrderByDescending(a => a.GapLevels).FirstOrDefault();

            if (!hasExamDate)
                return weakest is null
                    ? $"Bạn đã đạt năng lực tương đương {targetCode} ở cả 3 phần thi; chỉ cần ôn tập củng cố khoảng {recommendedStudyDays} ngày trước khi thi."
                    : $"Ước tính cần khoảng {recommendedStudyDays} ngày ôn tập để đạt {targetCode}, trọng tâm là {weakest.Label.ToLowerInvariant()} (hiện ở mức {weakest.EstimatedLevelCode}).";

            if (isExamDateRealistic == true)
                return weakest is null
                    ? $"Với tiến độ hiện tại, bạn đã đạt năng lực {targetCode} ở cả 3 phần thi và có thể giữ phong độ trong {daysUntilExam} ngày tới."
                    : $"Trong {daysUntilExam} ngày tới, bạn có đủ thời gian để cải thiện {weakest.Label.ToLowerInvariant()} (hiện ở mức {weakest.EstimatedLevelCode}) và đạt mục tiêu {targetCode} nếu ôn tập đều đặn.";

            var suggestedText = suggestedExamDate.HasValue ? suggestedExamDate.Value.ToString("dd/MM/yyyy") : "chưa xác định";
            var extraDays = Math.Max(0, recommendedStudyDays - daysUntilExam) + 7;
            return weakest is null
                ? $"Còn {daysUntilExam} ngày là không đủ an toàn dù năng lực hiện tại đã gần đạt {targetCode}; nên lùi ngày thi thêm khoảng {extraDays} ngày, dự kiến thi vào {suggestedText}."
                : $"Còn {daysUntilExam} ngày khó đạt {targetCode} vì {weakest.Label.ToLowerInvariant()} đang ở mức {weakest.EstimatedLevelCode}, cần khoảng {recommendedStudyDays} ngày để cải thiện. Nên lùi ngày thi thêm khoảng {extraDays} ngày, dự kiến thi vào {suggestedText}.";
        }

        // =====================================================================
        // DỮ LIỆU TỪ DB: catalog bài học theo TỪNG KỸ NĂNG + đúng khoảng mức cần ôn, tiến độ, sổ tay
        // =====================================================================
        private async Task<(List<CatalogLesson> Catalog, List<(string Title, int ProgressPercent)> LessonProgress, List<string> NotebookWords)> LoadLearnerDataAsync(
            int userId,
            IReadOnlyList<CertificateLevel> allLevels,
            IReadOnlyList<SkillAssessment> assessments)
        {
            var lessonProgresses = await unitOfWork.LessonProgresses.GetByUserAsync(userId) ?? [];
            var notebookEntries = await unitOfWork.LearnerNoteEntries.GetByUserAsync(userId, vocabularyListId: null) ?? [];

            // CẦN: unitOfWork.LanguageSkills (repository cho bảng LanguageSkill) với 1 method lấy toàn bộ.
            // Nếu IUnitOfWork/ILanguageSkillRepository của bạn đặt tên khác GetAllAsync(), đổi lại dòng dưới.
            var languageSkills = await unitOfWork.LanguageSkills.GetAllAsync();
            var skillGroupById = languageSkills.ToDictionary(skill => skill.Id, skill => ClassifySkillGroup(skill.Name));

            var progressByLesson = lessonProgresses
                .GroupBy(p => p.LessonId)
                .ToDictionary(g => g.Key, g => g.Max(p => Convert.ToDouble(p.ProgressPercent)));

            var catalog = new List<CatalogLesson>();
            foreach (var assessment in assessments)
            {
                var lowRank = Math.Min(assessment.EstimatedLevelRank, assessment.TargetLevelRank);
                var highRank = assessment.TargetLevelRank;
                var levelsForGroup = allLevels
                    .Where(level => JlptCurriculum.Rank(level.Code) >= lowRank && JlptCurriculum.Rank(level.Code) <= highRank)
                    .OrderBy(level => JlptCurriculum.Rank(level.Code))
                    .ToList();

                foreach (var level in levelsForGroup)
                {
                    var levelLessons = await unitOfWork.Lessons.GetByCertificateLevelAsync(level.Id);
                    var matchingLessons = levelLessons.Where(lesson =>
                        skillGroupById.GetValueOrDefault(lesson.LanguageSkillId, SkillGroup.LanguageKnowledge) == assessment.Group);

                    catalog.AddRange(matchingLessons.Select(lesson => new CatalogLesson(
                        lesson.Id, lesson.Title, level.Code,
                        progressByLesson.GetValueOrDefault(lesson.Id), assessment.Group)));
                }
            }

            catalog = catalog
                .Where(c => c.ProgressPercent < CompletedPercent)
                .DistinctBy(c => c.Id)
                .Take(MaxCatalogLessons)
                .ToList();

            var progressList = lessonProgresses
                .Take(MaxLessonProgressItems)
                .Select(p => (p.Lesson?.Title ?? string.Empty, p.ProgressPercent))
                .ToList();
            var notebookWords = notebookEntries
                .Select(n => n.Vocabulary?.Word ?? n.Kanji?.Character ?? n.ManualWord ?? string.Empty)
                .Where(w => !string.IsNullOrEmpty(w))
                .Distinct()
                .Take(MaxNotebookWords)
                .ToList();

            return (catalog, progressList, notebookWords);
        }

        private async Task<PlacementTestResultResponse?> LoadLatestPlacementResultAsync(int userId)
        {
            var attempts = await unitOfWork.MockTestAttempts.GetPlacementByUserAsync(userId) ?? [];
            foreach (var candidate in attempts
                .Where(attempt => !attempt.IsDeleted && attempt.UserId == userId &&
                                  attempt.AttemptKind == AttemptKind.Placement &&
                                  attempt.IsCompleted && attempt.TotalScore.HasValue)
                .OrderByDescending(attempt => attempt.SubmittedAt ?? attempt.StartedAt)
                .ThenByDescending(attempt => attempt.Id))
            {
                var result = await TryGetStoredPlacementResultAsync(candidate, userId);
                if (result is not null)
                    return result;
            }

            var latestCompleted = await unitOfWork.MockTestAttempts.GetLatestCompletedPlacementByUserAsync(userId);
            return latestCompleted is
            {
                IsDeleted: false, AttemptKind: AttemptKind.Placement,
                IsCompleted: true, TotalScore: not null
            }
                ? await TryGetStoredPlacementResultAsync(latestCompleted, userId)
                : null;
        }

        private async Task<PlacementTestResultResponse?> TryGetStoredPlacementResultAsync(
            MockTestAttempt attempt, int userId)
        {
            if (!attempt.SubmittedAt.HasValue || attempt.TotalScore is not double totalScore ||
                totalScore is < 0 or > 100)
                return PracticeTestService.TryGetCompletedResultFromSnapshot(attempt.QuestionSnapshotJson);

            var sections = await unitOfWork.MockTestSectionResults.GetByAttemptIdAsync(attempt.Id);
            if (sections.Count == 0)
                return PracticeTestService.TryGetCompletedResultFromSnapshot(attempt.QuestionSnapshotJson);
            if (sections.Any(section => section.TotalCount <= 0 ||
                    section.CorrectCount < 0 || section.CorrectCount > section.TotalCount ||
                    section.ScorePercent is < 0 or > 100))
                return null;

            var learner = await unitOfWork.UserAccounts.GetByIdAsync(userId);
            var assessedLevelId = learner?.AssessedCertificateLevelId;
            var level = assessedLevelId.HasValue
                ? await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(assessedLevelId.Value)
                : null;
            if (level is null || !IsSupportedJlptCode(level.Code))
                return null;

            var totalQuestions = sections.Sum(section => section.TotalCount);
            var correctCount = sections.Sum(section => section.CorrectCount);
            if (totalQuestions == 0)
                return null;

            return new PlacementTestResultResponse
            {
                AttemptId = attempt.Id,
                AttemptType = "placement",
                Status = "completed",
                StartedAt = attempt.StartedAt,
                SubmittedAt = attempt.SubmittedAt,
                DurationSeconds = Math.Max(0, (int)(attempt.SubmittedAt.Value - attempt.StartedAt).TotalSeconds),
                TotalScore = totalScore,
                CorrectCount = correctCount,
                TotalQuestions = totalQuestions,
                AssessedJlptLevelId = level.Id,
                AssessedJlptLevelName = level.Code,
                RecommendedTargetJlptLevelId = level.Id,
                RecommendedTargetJlptLevelName = level.Code,
                LevelBreakdowns =
                [
                    new PlacementLevelBreakdownItem
                    {
                        JlptLevelId = level.Id,
                        JlptLevelName = level.Code,
                        CorrectCount = correctCount,
                        TotalCount = totalQuestions,
                        ScorePercent = Math.Round(correctCount * 100d / totalQuestions, 2),
                    }
                ],
                Sections = sections.Select(section => new SectionResultItem
                {
                    MockTestSectionId = section.MockTestSectionId,
                    SectionTitle = section.MockTestSection?.Title ?? string.Empty,
                    LanguageSkillName = section.MockTestSection?.LanguageSkill?.Name ?? string.Empty,
                    CorrectCount = section.CorrectCount,
                    TotalCount = section.TotalCount,
                    Score = section.ScorePercent,
                }).ToList(),
            };
        }

        /// <summary>Chấp nhận N1-N5 (trước đây hard-code chỉ N5/N4/N3, chặn nhầm N2/N1).</summary>
        private static bool IsSupportedJlptCode(string? code)
            => Regex.IsMatch((code ?? string.Empty).Trim().ToUpperInvariant(), "^N[1-5]$");

        // =====================================================================
        // PROMPT — ép buộc tiếng Việt, cung cấp sẵn số liệu đã tính, AI chỉ viết summary + chọn lịch học
        // =====================================================================
        private static string BuildPrompt(
            IReadOnlyList<SkillAssessment> assessments,
            List<CatalogLesson> catalog,
            List<(string Title, int ProgressPercent)> lessonProgress,
            List<string> notebookWords,
            string certName,
            DateTime? examDate,
            DateTime today,
            int daysUntilExam,
            int planDays,
            int recommendedStudyDays,
            bool? isExamDateRealistic,
            DateTime? suggestedExamDate,
            int dailyStudyMinutes,
            string? studyTimePreference)
        {
            var sb = new StringBuilder();
            sb.AppendLine("BẮT BUỘC: Toàn bộ nội dung trả về (summary, focusTopics, title và focus của từng ngày) PHẢI viết 100% bằng TIẾNG VIỆT. Không được dùng tiếng Anh ở bất kỳ trường nào, kể cả một từ.");

            sb.AppendLine("\n[KẾT QUẢ ĐÁNH GIÁ NĂNG LỰC THEO TỪNG KỸ NĂNG — hệ thống đã tính sẵn, AI KHÔNG được tự suy luận lại các con số này]");
            foreach (var a in assessments)
            {
                sb.AppendLine(a.HasData
                    ? $"- {a.Label}: đạt {a.ScorePercent:0}% ở đề {a.TestLevelCode} → ước tính năng lực hiện tại ở mức {a.EstimatedLevelCode}; mục tiêu {a.TargetLevelCode}; {(a.MeetsTarget ? "ĐÃ ĐẠT mục tiêu" : $"còn thiếu {a.GapLevels} cấp độ")}."
                    : $"- {a.Label}: chưa có dữ liệu bài đánh giá, tạm ước tính ở mức {a.EstimatedLevelCode}; mục tiêu {a.TargetLevelCode}.");
            }
            sb.AppendLine($"- Chứng chỉ mục tiêu: {certName}");
            sb.AppendLine($"- Hệ thống đã tính: tổng số ngày ôn tập cần thiết = {recommendedStudyDays} ngày.");
            if (examDate.HasValue)
            {
                sb.AppendLine($"- Ngày dự định thi: {examDate.Value:dd/MM/yyyy} (còn {daysUntilExam} ngày).");
                sb.AppendLine(isExamDateRealistic == true
                    ? "- Hệ thống đánh giá: ngày thi này KHẢ THI."
                    : $"- Hệ thống đánh giá: ngày thi này KHÔNG khả thi; ngày thi gợi ý mới là {suggestedExamDate:dd/MM/yyyy}.");
            }
            else
            {
                sb.AppendLine("- Người học chưa chọn ngày thi cụ thể.");
            }
            sb.AppendLine($"- Thời gian học mỗi ngày: {dailyStudyMinutes} phút (số bài mỗi ngày phải vừa khung thời gian này).");
            if (!string.IsNullOrWhiteSpace(studyTimePreference))
                sb.AppendLine($"- Khung giờ học ưa thích: {studyTimePreference}.");

            if (lessonProgress.Count > 0)
            {
                sb.AppendLine("\n[TIẾN ĐỘ HỌC HIỆN TẠI CỦA NGƯỜI HỌC]");
                foreach (var (title, percent) in lessonProgress)
                    sb.AppendLine($"- {title}: {percent:0}%");
            }
            if (notebookWords.Count > 0)
                sb.AppendLine($"\n[TỪ TRONG SỔ TAY CÁ NHÂN]: {string.Join(", ", notebookWords)}");

            sb.AppendLine("\n[DANH SÁCH BÀI HỌC CÓ SẴN TRONG HỆ THỐNG, CHIA THEO TỪNG KỸ NĂNG — CHỈ ĐƯỢC CHỌN BÀI TRONG DANH SÁCH NÀY]");
            foreach (var group in catalog.GroupBy(c => c.Group))
            {
                sb.AppendLine($"-- {SkillGroupLabel(group.Key)} --");
                foreach (var lesson in group)
                    sb.AppendLine($"  id={lesson.Id} | JLPT {lesson.LevelCode} | {lesson.Title} | tiến độ hiện tại {lesson.ProgressPercent:0}%");
            }

            sb.AppendLine("\n[YÊU CẦU LẬP LỊCH ÔN LUYỆN]");
            sb.AppendLine($"- Lập đúng {planDays} ngày (day = 1 đến {planDays}).");
            sb.AppendLine("- Mỗi ngày chọn bài học bằng id TỪ DANH SÁCH TRÊN. TUYỆT ĐỐI không tự bịa id hay bài học không có trong danh sách.");
            sb.AppendLine("- Ưu tiên phân bổ nhiều ngày hơn cho kỹ năng còn thiếu nhiều cấp độ nhất; kỹ năng đã đạt mục tiêu chỉ cần ôn củng cố nhẹ, không cần dàn trải nhiều.");
            sb.AppendLine("- Mỗi ngày có thể kết hợp bài học của nhiều kỹ năng khác nhau (ví dụ vừa ôn từ vựng/ngữ pháp vừa luyện đọc hiểu trong cùng 1 ngày) để việc ôn tập toàn diện.");
            sb.AppendLine($"- Hệ thống hiện có {catalog.Count} bài học khả dụng. BẮT BUỘC đa dạng hóa, KHÔNG lặp lại chỉ 1-2 bài cho toàn bộ lộ trình nếu có nhiều bài khả dụng hơn.");

            sb.AppendLine("\n[ĐỊNH DẠNG ĐẦU RA — CHỈ TRẢ VỀ JSON HỢP LỆ, KHÔNG KÈM GIẢI THÍCH HAY MARKDOWN]");
            sb.AppendLine("Các khóa bắt buộc:");
            sb.AppendLine("  summary (string, tiếng Việt, 2-4 câu tóm tắt năng lực từng kỹ năng dựa trên dữ liệu đã cho ở trên, không tự bịa số liệu khác),");
            sb.AppendLine("  focusTopics (string[], tiếng Việt),");
            sb.AppendLine($"  dailySchedules (mảng đúng {planDays} phần tử: day:int, title:string tiếng Việt, focus:string tiếng Việt, lessonIds:int[]).");

            return sb.ToString();
        }

        // =====================================================================
        // AI RESPONSE: parse + normalize + validate (schema rút gọn: AI không còn tự tính số liệu)
        // =====================================================================
        private sealed record AiLearningPathResult(
            string Summary,
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
                            LessonIds = ReadIntList(dayEl, "lessonIds"),
                            Lessons = new List<string>(),
                        });
                    }
                }

                return new AiLearningPathResult(
                    Summary: ReadString(root, "summary"),
                    FocusTopics: ReadStringList(root, "focusTopics") ?? new List<string>(),
                    DailySchedules: dailySchedules);
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            {
                throw new JsonException("Cấu trúc JSON từ AI không đúng kiểu dữ liệu mong đợi.", ex);
            }
        }

        /// <summary>
        /// Giữ lại đúng các bài học có thật trong DB, loại id AI tự bịa, gán tiêu đề thật và chuẩn hóa stt ngày.
        /// </summary>
        private static int NormalizeLessons(AiLearningPathResult result, Dictionary<int, CatalogLesson> catalogById)
        {
            int dropped = 0;
            int dayCounter = 1;

            foreach (var day in result.DailySchedules)
            {
                day.Day = dayCounter++;

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

        // Các từ nối tiếng Anh thông dụng — nếu một đoạn văn xuất hiện nhiều từ này, khả năng cao
        // AI đã trả lời bằng tiếng Anh (hoặc lẫn tiếng Anh), cần từ chối và yêu cầu AI viết lại.
        private static readonly string[] EnglishTellSigns =
        {
            " the ", " and ", " you ", " your ", " is ", " are ", " with ", " this ", " that ",
            " for ", " have ", " has ", " will ", " would ", " should ", " on ", " in ", " at ",
            " to ", " of ", " however ", " given ", " per ", " level ", " week ", " study ",
        };

        private static bool LooksEnglish(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var padded = " " + text.ToLowerInvariant() + " ";
            return EnglishTellSigns.Count(sign => padded.Contains(sign)) >= 3;
        }

        /// <summary>Trả null nếu hợp lệ, ngược lại trả lý do bị từ chối (để AI tự sửa ở lần retry).</summary>
        private static string? ValidateAiResult(AiLearningPathResult result, int expectedDays, int catalogSize)
        {
            if (string.IsNullOrWhiteSpace(result.Summary))
                return "thiếu nhận xét tổng quan";
            if (LooksEnglish(result.Summary))
                return "phần summary lẫn tiếng Anh, phải viết hoàn toàn bằng tiếng Việt";
            if (result.FocusTopics.Any(LooksEnglish))
                return "focusTopics lẫn tiếng Anh, phải viết hoàn toàn bằng tiếng Việt";

            if (result.DailySchedules.Count == 0 || Math.Abs(result.DailySchedules.Count - expectedDays) > 3)
                return $"số ngày ({result.DailySchedules.Count}) khác yêu cầu ({expectedDays})";

            if (result.DailySchedules.Any(d => LooksEnglish(d.Title) || LooksEnglish(d.Focus)))
                return "tiêu đề/mô tả một số ngày lẫn tiếng Anh, phải viết hoàn toàn bằng tiếng Việt";

            if (result.DailySchedules.All(d => d.LessonIds is null || d.LessonIds.Count == 0))
                return "không chọn được bài học hợp lệ nào từ hệ thống";

            // Chặn trường hợp AI lặp lại đúng 1 (hoặc rất ít) bài cho toàn bộ lộ trình
            // dù catalog thực tế có nhiều bài học khả dụng hơn.
            if (catalogSize > 3)
            {
                var distinctLessonsUsed = result.DailySchedules
                    .SelectMany(d => d.LessonIds ?? new List<int>())
                    .Distinct()
                    .Count();
                var minExpectedDistinct = Math.Min(catalogSize, Math.Max(3, expectedDays / 5));
                if (distinctLessonsUsed < minExpectedDistinct)
                    return $"chỉ dùng {distinctLessonsUsed} bài học khác nhau dù hệ thống có {catalogSize} bài khả dụng; cần đa dạng hóa bài học";
            }

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
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var s = raw.Trim();
            if (!s.StartsWith("```")) return s;

            int firstNewLine = s.IndexOf('\n');
            int lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
            {
                return s[(firstNewLine + 1)..lastFence].Trim();
            }
            if (firstNewLine >= 0)
            {
                return s[(firstNewLine + 1)..].Trim();
            }
            return s;
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
            return new List<string>();
#endif
        }
    }
}