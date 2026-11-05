using Microsoft.Extensions.Logging;
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

namespace RikiPath.Application.Services
{
    public class LearningPathService(
        IUnitOfWork unitOfWork,
        IAiLearningPathClient aiClient,
        IClaimService claimService,
        ILogger<LearningPathService> logger) : ILearningPathService
    {
        // ===== Constants =====
        private const int MaxPlanDays = 180;          // Kế hoạch dài hạn ít nhất 6 tháng
        private const int ShortPlanDays = 30;
        private const int MaxAiAttempts = 2;          // Retry khi AI trả JSON sai / thiếu ngày / chọn bài không hợp lệ
        private const int MaxNotebookWords = 50;
        private const int MaxLessonProgressItems = 30;
        private const int MaxCatalogLessons = 200;    // Số bài học tối đa đưa vào prompt
        private const double CompletedPercent = 100;
        private static readonly JsonSerializerOptions StoredPathJsonOptions =
            new(JsonSerializerDefaults.Web);

        public Func<DateTime> UtcNowProvider { get; init; } = static () => DateTime.UtcNow;

        /// <summary>Một bài học trong DB thuộc cấp độ mục tiêu, kèm tiến độ của người học.</summary>
        private sealed record CatalogLesson(int Id, string Title, string LevelCode, double ProgressPercent);

        // =====================================================================
        // MAIN FLOW
        // AI Personalized Learning Path requires a real completed Placement result.
        // Standard Roadmap is the separate, non-AI path for learners who skip Placement.
        // -> AI phân tích DỮ LIỆU TRONG DB (bài đánh giá, tiến độ, notebook, danh mục bài học)
        //    + ngày thi => nhận xét + lộ trình dùng đúng bài học có trong hệ thống
        // =====================================================================
        public async Task<ApiResponse<LearningPathResponse>> GenerateLearningPathAsync(
            GenerateLearningPathRequest request,
            CancellationToken cancellationToken)
            => await GenerateInternalAsync(request?.Horizon, request?.TargetExamDate, null, isPreview: false, cancellationToken);

        public async Task<ApiResponse<LearningPathResponse>> PreviewLearningPathAsync(
            PreviewLearningPathRequest request, CancellationToken cancellationToken)
            => await GenerateInternalAsync(request?.Horizon, request?.TargetExamDate, request, isPreview: true, cancellationToken);

        private async Task<ApiResponse<LearningPathResponse>> GenerateInternalAsync(
            string? requestedHorizon,
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
                    "Lộ trình chỉ hỗ trợ cấp độ bắt đầu đến mục tiêu theo thứ tự N5 → N4 → N3.",
                    HttpStatusCode.BadRequest, errorCode: "LEARNING_PATH_LEVEL_UNSUPPORTED");

            // Include the full foundation below the target. Placement level decides
            // whether the learner can skip earlier levels, but the AI can only select
            // from this real DB catalog, ordered N5 -> N4 -> N3.
            var spanLevels = visibleLevels
                .Where(level => JlptCurriculum.Rank(level.Code) > 0
                    && JlptCurriculum.Rank(level.Code) <= JlptCurriculum.Rank(target.Code))
                .OrderBy(level => JlptCurriculum.Rank(level.Code))
                .ToList();

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
            // A real Placement result and the target gap determine whether foundations are needed.
            // The selected AI model evaluates date feasibility; the server applies hard safety rules.
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
            var skillScores = placementForPlan?.Sections
                .Select(section => new
                {
                    Name = string.IsNullOrWhiteSpace(section.LanguageSkillName) ? section.SectionTitle : section.LanguageSkillName,
                    section.ScorePercent,
                })
                .Where(section => !string.IsNullOrWhiteSpace(section.Name))
                .GroupBy(section => section.Name)
                .ToDictionary(group => group.Key, group => group.Average(section => section.ScorePercent));
            var targetLevelGap = placementForPlan is null ? 0 : Math.Max(0,
                JlptCurriculum.Rank(target.Code) - JlptCurriculum.Rank(placementForPlan.AssessedJlptLevelName));
            var catalogStartRank = JlptCurriculum.Rank(starting.Code);
            var assessedRank = JlptCurriculum.Rank(placementForPlan?.AssessedJlptLevelName);
            if (assessedRank > 0)
                catalogStartRank = Math.Min(catalogStartRank, assessedRank);
            spanLevels = spanLevels
                .Where(level => JlptCurriculum.Rank(level.Code) >= catalogStartRank)
                .ToList();
            var needsFoundation = placementForPlan is null ||
                placementForPlan.TotalScore < 50 ||
                (skillScores?.Values.Any(score => score < 30) ?? true) ||
                targetLevelGap > 0;
            var horizon = normalizedExamDate.HasValue
                ? "long_term"
                : needsFoundation ? "long_term" : NormalizeHorizon(requestedHorizon, daysUntilExam);
            int planDays = normalizedExamDate.HasValue
                ? MaxPlanDays
                : horizon == "short_term" ? ShortPlanDays : MaxPlanDays;

            // ---------- 3. Valid owned Placement is mandatory for AI generation/preview ----------
            string promptText;
            Dictionary<int, CatalogLesson> catalogById;
            LearningPathPromptContext promptContext;
            try
            {
                var learnerData = await LoadLearnerDataAsync(userId, spanLevels, certName, placementForPlan);
                promptContext = learnerData.Context;
                var catalog = learnerData.Catalog;

                if (!promptContext.LatestTestScorePercent.HasValue)
                    return ApiResponse<LearningPathResponse>.Fail(
                        "Bạn cần hoàn thành bài Placement Test hợp lệ trước khi tạo lộ trình AI.",
                        HttpStatusCode.BadRequest, errorCode: "LEARNING_PATH_PLACEMENT_REQUIRED");

                if (catalog.Count == 0)
                {
                    return ApiResponse<LearningPathResponse>.Fail(
                        $"Hiện chưa có bài học nào cho cấp độ {certName} (hoặc bạn đã hoàn thành tất cả).",
                        HttpStatusCode.BadRequest, errorCode: "LEARNING_PATH_NO_ELIGIBLE_LESSONS");
                }

                catalogById = catalog.ToDictionary(c => c.Id);

                promptText = BuildPrompt(promptContext, catalog, $"JLPT {starting.Code} → {target.Code}", normalizedExamDate, today,
                    daysUntilExam, planDays, horizon, user.DailyStudyMinutes, user.StudyTimePreference);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to build prompt context. UserId={UserId}", userId);
                return ApiResponse<LearningPathResponse>.Fail(
                    "Không thể tải dữ liệu học tập của bạn.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }

            // ---------- 4. Gọi AI (retry khi sai định dạng / thiếu ngày / bài học không hợp lệ) ----------
            AiLearningPathResult? aiResult = null;
            string? lastError = null;

            try
            {
                for (int attempt = 1; attempt <= MaxAiAttempts; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Lần retry: nói rõ lý do lần trước bị từ chối để AI sửa
                    string currentPrompt = lastError is null
                        ? promptText
                        : $"{promptText}\n\n[LƯU Ý] Lần trả lời trước bị từ chối vì: {lastError}. Hãy sửa đúng lỗi này.";

                    string rawJson = StripCodeFence(
                        await aiClient.GenerateLearningPathJsonAsync(userId, currentPrompt, cancellationToken));

                    try
                    {
                        var parsed = ParseAiResponse(rawJson);

                        var isExamDateRealistic = AssessExamDateFeasibility(
                            parsed, promptContext, daysUntilExam, normalizedExamDate.HasValue);
                        var minRecommendedDays = GetMinimumRecommendedStudyDays(promptContext);
                        parsed = parsed with
                        {
                            RecommendedStudyDays = Math.Max(parsed.RecommendedStudyDays, minRecommendedDays),
                        };

                        // Chỉ giữ bài học có thật trong DB (chống AI bịa id), rồi mới validate
                        int dropped = NormalizeLessons(parsed, catalogById);
                        if (dropped > 0)
                            logger.LogWarning("AI referenced {Dropped} unknown lesson ids. UserId={UserId}", dropped, userId);

                        horizon = normalizedExamDate.HasValue
                            ? isExamDateRealistic == true && daysUntilExam <= 60 ? "short_term" : "long_term"
                            : horizon;
                        planDays = normalizedExamDate.HasValue && isExamDateRealistic == true
                            ? Math.Min(daysUntilExam, MaxPlanDays)
                            : normalizedExamDate.HasValue ? MaxPlanDays : horizon == "short_term" ? ShortPlanDays : MaxPlanDays;
                        parsed = parsed with { IsExamDateRealistic = isExamDateRealistic, Horizon = horizon };

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

            // ---------- 5. Save DRAFT or first ACTIVE in a learner-row-locked transaction ----------
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
                    Strengths = aiResult.Strengths,
                    Weaknesses = aiResult.Weaknesses,
                    Feasibility = aiResult.Feasibility,
                    IsExamDateRealistic = aiResult.IsExamDateRealistic,
                    RecommendedStudyDays = aiResult.RecommendedStudyDays,
                    SuggestedExamDate = normalizedExamDate.HasValue && aiResult.IsExamDateRealistic == false
                        ? today.AddDays(Math.Max(Math.Max(aiResult.RecommendedStudyDays, MaxPlanDays), daysUntilExam + 1))
                        : null,
                    FocusTopics = aiResult.FocusTopics,
                    DailySchedules = aiResult.DailySchedules,
                };
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

                    // Under the learner row lock, retire the old plan before activating
                    // the new one. A transaction rollback restores the old ACTIVE plan.
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
                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(stored.UserId, cancellationToken);
                if (user?.TargetCertificateLevelId is null)
                    throw new InvalidOperationException("Legacy path has no target level.");
                var lessons = await unitOfWork.Lessons.GetByCertificateLevelAsync(
                    user.TargetCertificateLevelId.Value, cancellationToken);
                var catalog = lessons.ToDictionary(lesson => lesson.Id,
                    lesson => new CatalogLesson(lesson.Id, lesson.Title, string.Empty, 0));
                var legacy = ParseAiResponse(stored.SuggestionJson);
                NormalizeLessons(legacy, catalog);
                response = new LearningPathResponse
                {
                    Summary = legacy.Summary,
                    Strengths = legacy.Strengths,
                    Weaknesses = legacy.Weaknesses,
                    Feasibility = legacy.Feasibility,
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
            return response;
        }

        // =====================================================================
        // DỮ LIỆU TỪ DB: bài đánh giá + tiến độ + notebook + DANH MỤC BÀI HỌC của cấp độ mục tiêu
        // =====================================================================
        private async Task<(LearningPathPromptContext Context, List<CatalogLesson> Catalog)> LoadLearnerDataAsync(
            int userId, IReadOnlyList<CertificateLevel> levels, string targetCertification,
            PlacementTestResultResponse? immutableResult)
        {
            var lessonProgresses = await unitOfWork.LessonProgresses.GetByUserAsync(userId) ?? [];
            var notebookEntries = await unitOfWork.LearnerNoteEntries.GetByUserAsync(userId, vocabularyListId: null) ?? [];

            IReadOnlyDictionary<string, double>? skillBreakdown = null;
            if (immutableResult?.Sections is { Count: > 0 } sections)
            {
                var sectionScores = sections
                    .Select(section => new
                    {
                        Name = string.IsNullOrWhiteSpace(section.LanguageSkillName)
                            ? section.SectionTitle
                            : section.LanguageSkillName,
                        section.ScorePercent,
                    })
                    .Where(section => !string.IsNullOrWhiteSpace(section.Name))
                    .GroupBy(section => section.Name)
                    .ToDictionary(group => group.Key, group => group.Average(section => section.ScorePercent));
                skillBreakdown = sectionScores.Count > 0 ? sectionScores : null;
            }

            // Accept the immutable result snapshot, or the persisted section score rows
            // written by the legacy submit flow. Never rebuild scores from mutable banks.
            // ---- Danh mục bài học của cấp độ mục tiêu (nguồn duy nhất AI được phép chọn) ----
            var catalogWithLevels = new List<CatalogLesson>();
            foreach (var level in levels)
            {
                var levelLessons = await unitOfWork.Lessons.GetByCertificateLevelAsync(level.Id);
                catalogWithLevels.AddRange(levelLessons.Select(lesson => new CatalogLesson(
                    lesson.Id, lesson.Title, level.Code, 0)));
            }

            var progressByLesson = lessonProgresses
                .GroupBy(p => p.LessonId)
                .ToDictionary(g => g.Key, g => g.Max(p => Convert.ToDouble(p.ProgressPercent)));

            // Bài đã hoàn thành 100% không đưa vào danh mục (tiết kiệm token, tránh học lại)
            var catalog = catalogWithLevels
                .Select(lesson => lesson with
                {
                    ProgressPercent = progressByLesson.GetValueOrDefault(lesson.Id),
                })
                .Where(c => c.ProgressPercent < CompletedPercent)
                .DistinctBy(c => c.Id)
                .Take(MaxCatalogLessons)
                .ToList();

            var context = new LearningPathPromptContext(
                targetJlptLevel: targetCertification.Replace("JLPT ", string.Empty, StringComparison.OrdinalIgnoreCase),
                assessedJlptLevel: immutableResult?.AssessedJlptLevelName ?? "chưa xác định",
                lessonProgress: lessonProgresses
                    .Take(MaxLessonProgressItems)
                    .Select(p => (p.Lesson?.Title ?? string.Empty, p.ProgressPercent))
                    .ToList(),
                notebookWords: notebookEntries
                    .Select(n => n.Vocabulary?.Word ?? n.Kanji?.Character ?? n.ManualWord ?? string.Empty)
                    .Where(w => !string.IsNullOrEmpty(w))
                    .Distinct()
                    .Take(MaxNotebookWords)
                    .ToList(),
                latestTestScorePercent: immutableResult?.TotalScore,
                latestTestLanguageSkillBreakdown: skillBreakdown);

            return (context, catalog);
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
            return latestCompleted is { IsDeleted: false, AttemptKind: AttemptKind.Placement,
                IsCompleted: true, TotalScore: not null }
                ? await TryGetStoredPlacementResultAsync(latestCompleted, userId)
                : null;
        }

        private async Task<PlacementTestResultResponse?> TryGetStoredPlacementResultAsync(
            MockTestAttempt attempt, int userId)
        {
            // Use the stored section rows as the source of truth for AI analysis. The
            // snapshot fallback supports completed Placement attempts created by older flows.
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

        private static bool IsSupportedJlptCode(string? code)
            => code?.Trim().ToUpperInvariant() is "N5" or "N4" or "N3";

        // =====================================================================
        // PROMPT
        // =====================================================================
        private static string BuildPrompt(
            LearningPathPromptContext promptContext,
            List<CatalogLesson> catalog,
            string certName,
            DateTime? examDate,
            DateTime today,
            int daysUntilExam,
            int planDays,
            string horizon,
            int dailyStudyMinutes,
            string? studyTimePreference)
        {
            string strategy = examDate.HasValue
                ? "Đánh giá khả năng đạt mục tiêu theo dữ liệu Placement trước, sau đó tự chọn ngắn hạn đến ngày thi hoặc dài hạn nếu cần dời lịch."
                : horizon == "short_term"
                    ? "NGẮN HẠN: củng cố nhanh các kỹ năng yếu nhất, ưu tiên bài cốt lõi và luyện đề có chọn lọc."
                    : "DÀI HẠN: xây nền theo thứ tự, phân bổ đều kiến thức và kỹ năng, tăng dần luyện đề ở giai đoạn cuối.";

            var sb = new StringBuilder();
            sb.AppendLine("[THÔNG TIN NGƯỜI HỌC VÀ KẾT QUẢ ĐÁNH GIÁ NĂNG LỰC (DỮ LIỆU THẬT TRONG HỆ THỐNG)]");
            sb.AppendLine(promptContext.ToPromptText());
            sb.AppendLine($"- Chứng chỉ mục tiêu: {certName}");
            sb.AppendLine($"- Năng lực hiện tại theo Placement gần nhất: JLPT {promptContext.AssessedJlptLevel}");
            sb.AppendLine($"- Hôm nay: {today:yyyy-MM-dd}");
            sb.AppendLine(examDate.HasValue
                ? $"- Ngày dự định thi: {examDate.Value:yyyy-MM-dd} (còn {daysUntilExam} ngày)"
                : "- Người học chưa đặt ngày thi cụ thể.");
            sb.AppendLine(examDate.HasValue
                ? $"- Hãy tự quyết định kế hoạch NGẮN HẠN đến ngày thi ({daysUntilExam} ngày) nếu thật sự khả thi; nếu không, lập kế hoạch DÀI HẠN đúng {MaxPlanDays} ngày và khuyên ngày thi phù hợp hơn."
                : $"- Chưa có ngày thi cụ thể; lập kế hoạch {horizon} trong đúng {planDays} ngày.");
            sb.AppendLine("- Nếu năng lực hiện tại thấp hơn mục tiêu 2 cấp JLPT (ví dụ N5 lên N3), tối thiểu khuyến nghị 180 ngày; nếu thấp hơn 1 cấp, tối thiểu 90 ngày. Không được đánh giá khả thi cho thời hạn ngắn hơn các mức này.");
            sb.AppendLine($"- Chiến lược ôn luyện: {strategy}");
            sb.AppendLine($"- Thời gian học mỗi ngày: {dailyStudyMinutes} phút (số bài mỗi ngày PHẢI vừa khung thời gian này).");
            if (!string.IsNullOrWhiteSpace(studyTimePreference))
                sb.AppendLine($"- Khung giờ học ưa thích: {studyTimePreference}.");

            sb.AppendLine("\n[DANH SÁCH BÀI HỌC CÓ SẴN TRONG HỆ THỐNG - CHỈ ĐƯỢC CHỌN BÀI TRONG DANH SÁCH NÀY]");
            foreach (var l in catalog)
                sb.AppendLine($"- id={l.Id} | JLPT {l.LevelCode} | {l.Title} | tiến độ hiện tại {l.ProgressPercent:0}%");

            sb.AppendLine("\n[YÊU CẦU 1 - NHẬN XÉT NĂNG LỰC]");
            sb.AppendLine("- Dựa trên điểm Placement và điểm từng kỹ năng, nêu điểm mạnh và điểm yếu cụ thể.");
            sb.AppendLine(examDate.HasValue
                ? $"- Đánh giá trung thực khả năng đạt {certName} trong {daysUntilExam} ngày. Nếu tổng Placement dưới 50% hoặc có kỹ năng dưới 30%, mặc định KHÔNG khả thi trong thời hạn này."
                : $"- Đánh giá tiến độ hiện tại và điều kiện cần để tiến tới {certName} sau kế hoạch {planDays} ngày.");
            sb.AppendLine("- Trả về isExamDateRealistic (boolean hoặc null nếu chưa đặt ngày thi) và recommendedStudyDays (số ngày học thực tế cần có).");

            sb.AppendLine("\n[YÊU CẦU 2 - LỘ TRÌNH ÔN LUYỆN THEO NGÀY]");
            if (examDate.HasValue)
            {
                sb.AppendLine($"- Nếu isExamDateRealistic=true: dailySchedules phải có đúng {Math.Min(daysUntilExam, MaxPlanDays)} ngày (một ngày cho mỗi ngày đến kỳ thi, tối đa {MaxPlanDays}).");
                sb.AppendLine($"- Nếu isExamDateRealistic=false: dailySchedules phải có đúng {MaxPlanDays} ngày và summary/feasibility phải khuyên dời lịch thi, đề xuất số ngày cần chuẩn bị.");
            }
            else
            {
                sb.AppendLine($"- Chia lộ trình thành ĐÚNG {planDays} NGÀY liên tục (day = 1 đến day = {planDays}), không bỏ sót, không trùng.");
            }
            sb.AppendLine("- Mỗi ngày chọn các bài học từ DANH SÁCH ở trên bằng id. TUYỆT ĐỐI không tự bịa bài học hay id không có trong danh sách.");
            sb.AppendLine("- Ưu tiên kỹ năng yếu theo Placement; bài đang học dở nên hoàn thành trước.");
            sb.AppendLine("- Nếu người học có từ trong sổ tay (notebook), có thể dành một số ngày/phần ôn lại các từ đó trong 'focus'.");
            sb.AppendLine("- Những ngày cuối trước kỳ thi: giảm nội dung mới, ưu tiên ôn lại và luyện đề.");

            sb.AppendLine("\n[ĐỊNH DẠNG ĐẦU RA]");
            sb.AppendLine("- Chỉ trả về JSON hợp lệ, KHÔNG kèm giải thích hay markdown, với các khóa:");
            sb.AppendLine("  summary (string - nhận xét năng lực hiện tại), strengths (string[]), weaknesses (string[]), feasibility (string),");
            sb.AppendLine("  isExamDateRealistic (boolean/null), recommendedStudyDays (int), focusTopics (string[]),");
            sb.AppendLine("  dailySchedules (mảng: day:int, title:string, focus:string, lessonIds:int[] - id lấy từ danh sách bài học).");

            return sb.ToString();
        }

        private static string NormalizeHorizon(string? requestedHorizon, int daysUntilExam)
        {
            if (string.Equals(requestedHorizon, "short_term", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestedHorizon, "short", StringComparison.OrdinalIgnoreCase))
                return "short_term";
            if (string.Equals(requestedHorizon, "long_term", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestedHorizon, "long", StringComparison.OrdinalIgnoreCase))
                return "long_term";
            return daysUntilExam is > 0 and <= 30 ? "short_term" : "long_term";
        }

        private static bool? AssessExamDateFeasibility(
            AiLearningPathResult assessment,
            LearningPathPromptContext context,
            int daysUntilExam,
            bool hasExamDate)
        {
            if (!hasExamDate)
                return null;

            var requiredMinimumDays = GetMinimumRecommendedStudyDays(context);

            if (assessment.IsExamDateRealistic != true ||
                assessment.RecommendedStudyDays <= 0 ||
                assessment.RecommendedStudyDays > daysUntilExam ||
                daysUntilExam < requiredMinimumDays ||
                (context.LatestTestScorePercent ?? 0) < 50)
                return false;

            if (context.LatestTestLanguageSkillBreakdown is { Count: > 0 } skills &&
                skills.Values.Any(score => score < 30))
                return false;

            return true;
        }

        private static int GetMinimumRecommendedStudyDays(LearningPathPromptContext context)
        {
            var levelGap = Math.Max(0, JlptCurriculum.Rank(context.TargetJlptLevel) -
                                       JlptCurriculum.Rank(context.AssessedJlptLevel));
            if (levelGap >= 2 || (context.LatestTestScorePercent ?? 0) < 50 ||
                context.LatestTestLanguageSkillBreakdown is { Count: > 0 } skills && skills.Values.Any(score => score < 30))
                return MaxPlanDays;
            return levelGap == 1 ? 90 : 1;
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
            List<DailyScheduleResponse> DailySchedules,
            bool? IsExamDateRealistic,
            int RecommendedStudyDays,
            string Horizon);

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
                    DailySchedules: dailySchedules,
                    IsExamDateRealistic: ReadNullableBool(root, "isExamDateRealistic"),
                    RecommendedStudyDays: ReadInt(root, "recommendedStudyDays"),
                    Horizon: string.Empty);
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

            if (result.RecommendedStudyDays <= 0)
                return "thiếu recommendedStudyDays hợp lệ";

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

        private static bool? ReadNullableBool(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                return null;
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }

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
