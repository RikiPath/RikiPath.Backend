using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PlacementTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.PlacementTests;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace RikiPath.Application.Services
{
    public static class BusinessDateHelper
    {
        public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        public static DateTime EnsureUtc(DateTime value)
            => value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };

        public static DateTime GetVietnamToday(DateTime utcNow)
            => DateTime.SpecifyKind((EnsureUtc(utcNow) + VietnamOffset).Date, DateTimeKind.Unspecified);

        public static DateTime NormalizeExamDate(DateTime value)
        {
            if (value.TimeOfDay == TimeSpan.Zero)
            {
                return DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
            }

            return value.Kind switch
            {
                DateTimeKind.Utc => DateTime.SpecifyKind((value + VietnamOffset).Date, DateTimeKind.Unspecified),
                DateTimeKind.Local => DateTime.SpecifyKind((value.ToUniversalTime() + VietnamOffset).Date, DateTimeKind.Unspecified),
                _ => DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified),
            };
        }

        public static int GetDaysUntilExam(DateTime targetExamDate, DateTime utcNow)
            => (NormalizeExamDate(targetExamDate) - GetVietnamToday(utcNow)).Days;

        public static bool IsFutureExamDate(DateTime? targetExamDate, DateTime utcNow)
            => targetExamDate.HasValue && GetDaysUntilExam(targetExamDate.Value, utcNow) >= 1;
    }

    public class PlacementTestService(IUnitOfWork unitOfWork, IClaimService claimService) : IPlacementTestService
    {
        public const string AttemptTypePlacement = "placement";
        public const string StatusInProgress = "inProgress";
        public const string StatusCompleted = "completed";

        public const string ErrorBankNotReady = "PLACEMENT_BANK_NOT_READY";
        public const string ErrorAttemptNotFound = "PLACEMENT_ATTEMPT_NOT_FOUND";
        public const string ErrorAttemptExpired = "PLACEMENT_ATTEMPT_EXPIRED";
        public const string ErrorAnswerInvalid = "PLACEMENT_ANSWER_INVALID";
        public const string ErrorAttemptNotReady = "PLACEMENT_ATTEMPT_NOT_READY";
        public const string ErrorAttemptNotCompleted = "PLACEMENT_ATTEMPT_NOT_COMPLETED";

        private const int MinimumEligibleQuestionsPerLevel = 2;
        private const int DefaultQuestionsPerLevel = 5;
        private const string NotFoundAttemptMessage = "Không tìm thấy lượt làm bài đánh giá năng lực.";

        private static readonly string[] SupportedLevels = ["N5", "N4", "N3"];
        private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> UserStartLocks = new();
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> AttemptSubmitLocks = new();

        public Func<DateTime> UtcNowProvider { get; init; } = static () => DateTime.UtcNow;

        public async Task<ApiResponse<PlacementTestStatusResponse>> GetStatusAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
                if (user is null)
                {
                    return ApiResponse<PlacementTestStatusResponse>.NotFound("Không tìm thấy người dùng.");
                }

                var serverNow = EnsureUtc(UtcNowProvider());
                var placementAttempts = await LoadUserPlacementAttemptsAsync(userId, cancellationToken);

                var activeAttempt = placementAttempts
                    .Where(a => !a.IsCompleted &&
                                a.ExpiresAt.HasValue &&
                                EnsureUtc(a.ExpiresAt.Value) > serverNow &&
                                TryDeserializeSnapshot(a.QuestionSnapshotJson, out var doc) &&
                                doc.Questions.Count > 0)
                    .OrderByDescending(a => a.StartedAt)
                    .ThenByDescending(a => a.Id)
                    .FirstOrDefault();

                var completedWithSnapshot = placementAttempts
                    .Where(a => a.IsCompleted && a.TotalScore.HasValue)
                    .OrderByDescending(a => a.SubmittedAt ?? a.StartedAt)
                    .ThenByDescending(a => a.Id)
                    .Select(a => (Attempt: a, Recovered: RecoverResultFromCompletedAttempt(a)))
                    .FirstOrDefault(x => x.Recovered is not null);

                var latestCompleted = completedWithSnapshot.Attempt;
                var recovered = completedWithSnapshot.Recovered;

                int? assessedLevelId = recovered?.AssessedJlptLevelId;
                string? assessedLevelName = recovered?.AssessedJlptLevelName;
                int? recommendedLevelId = recovered?.RecommendedTargetJlptLevelId;
                string? recommendedLevelName = recovered?.RecommendedTargetJlptLevelName;

                string? targetLevelName = user.TargetCertificateLevel?.Code;
                if (user.TargetCertificateLevelId.HasValue && string.IsNullOrWhiteSpace(targetLevelName))
                {
                    var targetLevel = await unitOfWork.CertificateLevels.GetByIdAsync(
                        user.TargetCertificateLevelId.Value,
                        cancellationToken);
                    targetLevelName = targetLevel?.Code;
                }

                var hasValidExamDate = BusinessDateHelper.IsFutureExamDate(user.TargetExamDate, serverNow);
                var isReadyForLearningPath =
                    (user.StartingCertificateLevelId ?? user.TargetCertificateLevelId).HasValue &&
                    user.TargetCertificateLevelId.HasValue &&
                    hasValidExamDate &&
                    user.DailyStudyMinutes > 0;

                return ApiResponse<PlacementTestStatusResponse>.Success(new PlacementTestStatusResponse
                {
                    AttemptType = AttemptTypePlacement,
                    PlacementDecision = latestCompleted is not null || user.PlacementDecision == PlacementDecision.Completed
                        ? "COMPLETED"
                        : user.PlacementDecision == PlacementDecision.Skipped ? "SKIPPED" : "NOT_TAKEN",
                    HasActiveAttempt = activeAttempt is not null,
                    ActiveAttemptId = activeAttempt?.Id,
                    ActiveAttemptExpiresAt = activeAttempt?.ExpiresAt is { } exp ? EnsureUtc(exp) : null,
                    HasCompletedPlacementTest = latestCompleted is not null,
                    LatestCompletedAttemptId = latestCompleted?.Id,
                    LatestAttemptId = latestCompleted?.Id,
                    LatestSubmittedAt = latestCompleted?.SubmittedAt is { } sub ? EnsureUtc(sub) : null,
                    LatestScorePercent = latestCompleted?.TotalScore,
                    AssessedJlptLevelId = assessedLevelId,
                    AssessedJlptLevelName = assessedLevelName,
                    RecommendedTargetJlptLevelId = recommendedLevelId,
                    RecommendedTargetJlptLevelName = recommendedLevelName,
                    TargetJlptLevelId = user.TargetCertificateLevelId,
                    TargetJlptLevelName = targetLevelName,
                    DailyStudyMinutes = user.DailyStudyMinutes,
                    TargetExamDate = user.TargetExamDate,
                    IsReadyForLearningPath = isReadyForLearningPath,
                });
            }
            catch (Exception)
            {
                return ApiResponse<PlacementTestStatusResponse>.Fail(
                    "Không thể tải trạng thái bài kiểm tra đánh giá năng lực.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PlacementTestStatusResponse>> SkipPlacementAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                if (user is null)
                    return ApiResponse<PlacementTestStatusResponse>.NotFound("Không tìm thấy người dùng.");

                // Không ghi đè quyết định COMPLETED đã được xác nhận từ kết quả thật.
                if (user.PlacementDecision != PlacementDecision.Completed)
                {
                    user.PlacementDecision = PlacementDecision.Skipped;
                    unitOfWork.UserAccounts.Update(user);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                return await GetStatusAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return ApiResponse<PlacementTestStatusResponse>.Fail(
                    "Không thể lưu lựa chọn bỏ qua bài đánh giá.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PlacementAttemptSnapshotResponse?>> GetActiveAttemptAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var serverNow = EnsureUtc(UtcNowProvider());
                var active = await TryFindCanonicalActiveAttemptAsync(userId, serverNow, cancellationToken);
                if (active is null)
                {
                    return ApiResponse<PlacementAttemptSnapshotResponse?>.Success(null);
                }

                var snapshotResponse = BuildSnapshotResponse(
                    active.Value.Attempt,
                    active.Value.Snapshot,
                    serverNow,
                    isResumed: true);
                return ApiResponse<PlacementAttemptSnapshotResponse?>.Success(snapshotResponse);
            }
            catch (Exception)
            {
                return ApiResponse<PlacementAttemptSnapshotResponse?>.Fail(
                    "Không thể tải lượt làm bài đánh giá năng lực đang diễn ra.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestAsync(
            CancellationToken cancellationToken = default)
            => StartPlacementTestInternalAsync(DefaultQuestionsPerLevel, cancellationToken);

        public Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestAsync(
            StartPlacementTestRequest? request,
            CancellationToken cancellationToken = default)
        {
            var requestedQuestionsPerLevel = Math.Clamp(
                request?.MaxQuestionsPerLevel ?? DefaultQuestionsPerLevel,
                MinimumEligibleQuestionsPerLevel,
                DefaultQuestionsPerLevel);
            return StartPlacementTestInternalAsync(requestedQuestionsPerLevel, cancellationToken);
        }

        private async Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestInternalAsync(
            int questionsPerSupportedLevel,
            CancellationToken cancellationToken)
        {
            var userId = 0;
            SemaphoreSlim? userLock = null;
            try
            {
                userId = claimService.GetUserClaim().Id;
                userLock = UserStartLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
                await userLock.WaitAsync(cancellationToken);

                var serverNow = EnsureUtc(UtcNowProvider());
                var preExistingActive = await TryFindCanonicalActiveAttemptAsync(
                    userId,
                    serverNow,
                    cancellationToken);
                if (preExistingActive is not null)
                {
                    return ApiResponse<PlacementAttemptSnapshotResponse>.Success(
                        BuildSnapshotResponse(
                            preExistingActive.Value.Attempt,
                            preExistingActive.Value.Snapshot,
                            serverNow,
                            isResumed: true),
                        HttpStatusCode.OK);
                }

                var tests = await unitOfWork.MockTests.GetAllWithSectionsAndQuestionsAsync(cancellationToken);
                var allLevels = (await unitOfWork.CertificateLevels.GetAllAsync(cancellationToken))
                    .Where(l => !l.IsDeleted && l.Id > 0 && !string.IsNullOrWhiteSpace(l.Code))
                    .ToList();

                var supportedCatalogLevels = allLevels
                    .Select(l => (LevelId: l.Id, LevelCode: NormalizeLevelCode(l.Code), Rank: GetLevelRank(l.Code)))
                    .Where(l => IsSupportedLevel(l.LevelCode))
                    .GroupBy(l => l.LevelCode)
                    .Select(g => g.OrderBy(x => x.LevelId).First())
                    .OrderBy(l => l.Rank)
                    .ToDictionary(l => l.LevelCode);

                var levelById = allLevels
                    .Select(l => (LevelId: l.Id, LevelCode: NormalizeLevelCode(l.Code), Rank: GetLevelRank(l.Code)))
                    .Where(l => IsSupportedLevel(l.LevelCode))
                    .ToDictionary(l => l.LevelId);

                var eligibleCandidates = new List<(
                    MockTest Test,
                    MockTestSection Section,
                    MockQuestion Question,
                    List<MockQuestionOption> ValidOptions,
                    int CorrectOptionId,
                    int LevelId,
                    string LevelCode,
                    int LevelRank)>();

                var seenQuestionIds = new HashSet<int>();

                foreach (var test in (tests ?? []).Where(t => t is not null && !t.IsDeleted && t.Id > 0 && t.CertificateLevelId > 0).OrderBy(t => t.Id))
                {
                    if (test.CertificateLevel is { IsDeleted: true })
                    {
                        continue;
                    }

                    string? resolvedCode = null;
                    int resolvedLevelId = test.CertificateLevelId;

                    if (test.CertificateLevel is { IsDeleted: false, Id: > 0 } navLevel &&
                        !string.IsNullOrWhiteSpace(navLevel.Code))
                    {
                        resolvedCode = NormalizeLevelCode(navLevel.Code);
                        resolvedLevelId = navLevel.Id;
                    }
                    else if (levelById.TryGetValue(test.CertificateLevelId, out var catLevel))
                    {
                        resolvedCode = catLevel.LevelCode;
                        resolvedLevelId = catLevel.LevelId;
                    }

                    if (resolvedCode is null || !IsSupportedLevel(resolvedCode) || resolvedLevelId <= 0)
                    {
                        continue;
                    }

                    var levelRank = GetLevelRank(resolvedCode);
                    if (!supportedCatalogLevels.ContainsKey(resolvedCode))
                    {
                        supportedCatalogLevels[resolvedCode] = (resolvedLevelId, resolvedCode, levelRank);
                    }

                    var orderedSections = (test.Sections ?? [])
                        .Where(s => s is not null && !s.IsDeleted && s.Id > 0)
                        .OrderBy(s => s.SortOrder)
                        .ThenBy(s => s.Id);

                    foreach (var section in orderedSections)
                    {
                        var orderedQuestions = (section.Questions ?? [])
                            .Where(q => q is not null && !q.IsDeleted && q.Id > 0)
                            .OrderBy(q => q.SortOrder)
                            .ThenBy(q => q.Id);

                        foreach (var question in orderedQuestions)
                        {
                            if (!HasUsablePrompt(question) || !seenQuestionIds.Add(question.Id))
                            {
                                continue;
                            }

                            var validOptions = (question.Options ?? [])
                                .Where(o => o is not null &&
                                            !o.IsDeleted &&
                                            o.Id > 0 &&
                                            !string.IsNullOrWhiteSpace(o.OptionText))
                                .OrderBy(o => o.SortOrder)
                                .ThenBy(o => o.Id)
                                .ToList();

                            if (validOptions.Count < 2)
                            {
                                continue;
                            }

                            if (validOptions.Select(o => o.Id).Distinct().Count() != validOptions.Count)
                            {
                                continue;
                            }

                            var correctOptions = validOptions.Where(o => o.IsCorrect).ToList();
                            if (correctOptions.Count != 1)
                            {
                                continue;
                            }

                            eligibleCandidates.Add((
                                Test: test,
                                Section: section,
                                Question: question,
                                ValidOptions: validOptions,
                                CorrectOptionId: correctOptions[0].Id,
                                LevelId: resolvedLevelId,
                                LevelCode: resolvedCode,
                                LevelRank: levelRank));
                        }
                    }
                }

                var groupedByLevel = eligibleCandidates
                    .GroupBy(c => c.LevelCode)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var requiredLevel in SupportedLevels)
                {
                    if (!groupedByLevel.TryGetValue(requiredLevel, out var levelQuestions) ||
                        levelQuestions.Count < MinimumEligibleQuestionsPerLevel)
                    {
                        return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                            "Ngân hàng câu hỏi đánh giá năng lực chưa đủ câu hỏi hợp lệ cho các cấp độ N5, N4 và N3.",
                            HttpStatusCode.Conflict,
                            errorCode: ErrorBankNotReady,
                            retryable: false);
                    }
                }

                var selectedQuestions = SupportedLevels
                    .SelectMany(levelCode => groupedByLevel[levelCode].Take(questionsPerSupportedLevel))
                    .ToList();

                var timeLimitMinutes = ComputeTimeLimitMinutes(selectedQuestions.Count);

                var startedAt = EnsureUtc(UtcNowProvider());
                var expiresAt = EnsureUtc(startedAt.AddMinutes(timeLimitMinutes));

                var snapshotDoc = new PlacementAttemptSnapshotDocument
                {
                    SchemaVersion = 2,
                    TimeLimitMinutes = timeLimitMinutes,
                    StartedAt = startedAt,
                    ExpiresAt = expiresAt,
                    CatalogLevels = supportedCatalogLevels.Values
                        .OrderBy(l => l.Rank)
                        .Select(l => new PlacementSnapshotLevelCatalogItem
                        {
                            JlptLevelId = l.LevelId,
                            JlptLevelName = l.LevelCode,
                            Rank = l.Rank,
                        })
                        .ToList(),
                    Questions = selectedQuestions.Select((item, index) => new PlacementSnapshotQuestionItem
                    {
                        QuestionId = item.Question.Id,
                        MockTestId = item.Test.Id,
                        MockTestSectionId = item.Section.Id,
                        SectionTitle = item.Section.Title ?? string.Empty,
                        SectionOrder = item.Section.SortOrder,
                        LanguageSkillName = item.Section.LanguageSkill?.Name ?? string.Empty,
                        JlptLevelId = item.LevelId,
                        JlptLevelName = item.LevelCode,
                        Order = index + 1,
                        Content = item.Question.QuestionText ?? string.Empty,
                        AudioUrl = item.Question.AudioUrl,
                        ImageUrl = item.Question.ImageUrl,
                        CorrectOptionId = item.CorrectOptionId,
                        SelectedOptionId = null,
                        IsCorrect = false,
                        Options = item.ValidOptions.Select(opt => new PlacementSnapshotOptionItem
                        {
                            OptionId = opt.Id,
                            Content = opt.OptionText,
                            Order = opt.SortOrder,
                            IsCorrect = opt.IsCorrect,
                        }).ToList(),
                    }).ToList(),
                };

                var snapshotJson = JsonSerializer.Serialize(snapshotDoc, SnapshotJsonOptions);

                ApiResponse<PlacementAttemptSnapshotResponse>? response = null;
                try
                {
                    await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
                    {
                        await LockUserForPlacementStartAsync(userId, transactionCancellationToken);

                        var nowInTx = EnsureUtc(UtcNowProvider());
                        var allPlacementAttempts = await LoadUserPlacementAttemptsAsync(
                            userId,
                            transactionCancellationToken);

                        var activeAfterLock = FindCanonicalActiveAttempt(allPlacementAttempts, nowInTx);
                        if (activeAfterLock is not null)
                        {
                            response = ApiResponse<PlacementAttemptSnapshotResponse>.Success(
                                BuildSnapshotResponse(
                                    activeAfterLock.Value.Attempt,
                                    activeAfterLock.Value.Snapshot,
                                    nowInTx,
                                    isResumed: true),
                                HttpStatusCode.OK);
                            return;
                        }

                        var staleOpenAttempts = allPlacementAttempts
                            .Where(a => !a.IsCompleted && !a.IsDeleted)
                            .ToList();
                        if (staleOpenAttempts.Count > 0)
                        {
                            foreach (var stale in staleOpenAttempts)
                            {
                                if (TryDeserializeSnapshot(stale.QuestionSnapshotJson, out var staleDoc))
                                {
                                    staleDoc.IsSupersededExpired = true;
                                    stale.QuestionSnapshotJson = JsonSerializer.Serialize(staleDoc, SnapshotJsonOptions);
                                }
                                stale.IsDeleted = true;
                                stale.ModifiedDate = nowInTx;
                                unitOfWork.MockTestAttempts.Update(stale);
                            }

                            await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                        }

                        var attempt = new MockTestAttempt
                        {
                            UserId = userId,
                            AttemptKind = AttemptKind.Placement,
                            StartedAt = startedAt,
                            ExpiresAt = expiresAt,
                            SubmittedAt = null,
                            TotalScore = null,
                            IsCompleted = false,
                            QuestionSnapshotJson = snapshotJson,
                        };

                        await unitOfWork.MockTestAttempts.AddAsync(attempt, transactionCancellationToken);
                        await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                        var answerRows = selectedQuestions.Select(item => new MockTestAnswer
                        {
                            MockTestAttemptId = attempt.Id,
                            MockQuestionId = item.Question.Id,
                            SelectedOptionId = null,
                            IsCorrect = false,
                        });
                        await unitOfWork.MockTestAnswers.AddRangeAsync(answerRows, transactionCancellationToken);
                        await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                        response = ApiResponse<PlacementAttemptSnapshotResponse>.Created(
                            BuildSnapshotResponse(attempt, snapshotDoc, startedAt, isResumed: false));
                    }, cancellationToken);
                }
                catch (DbUpdateException)
                {
                    var recoveredNow = EnsureUtc(UtcNowProvider());
                    var canonicalActive = await TryFindCanonicalActiveAttemptAsync(
                        userId,
                        recoveredNow,
                        cancellationToken);
                    if (canonicalActive is not null)
                    {
                        return ApiResponse<PlacementAttemptSnapshotResponse>.Success(
                            BuildSnapshotResponse(
                                canonicalActive.Value.Attempt,
                                canonicalActive.Value.Snapshot,
                                recoveredNow,
                                isResumed: true),
                            HttpStatusCode.OK);
                    }

                    return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                        "Không thể đồng bộ lượt làm bài với máy chủ. Vui lòng thử lại.",
                        HttpStatusCode.Conflict,
                        errorCode: ErrorAttemptNotReady,
                        retryable: true);
                }

                return response!;
            }
            catch (Exception)
            {
                return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                    "Không thể bắt đầu bài kiểm tra đánh giá năng lực.",
                    HttpStatusCode.InternalServerError);
            }
            finally
            {
                userLock?.Release();
            }
        }

        public async Task<ApiResponse<PlacementTestResultResponse>> SubmitPlacementTestAsync(
            int attemptId,
            SubmitPlacementTestRequest request,
            CancellationToken cancellationToken = default)
        {
            var userId = 0;
            SemaphoreSlim? attemptLock = null;
            try
            {
                userId = claimService.GetUserClaim().Id;
                attemptLock = AttemptSubmitLocks.GetOrAdd(attemptId, _ => new SemaphoreSlim(1, 1));
                await attemptLock.WaitAsync(cancellationToken);

                ApiResponse<PlacementTestResultResponse>? response = null;
                try
                {
                    await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
                    {
                        var attempt = await LoadPlacementAttemptForSubmissionAsync(
                            attemptId,
                            transactionCancellationToken);

                        if (attempt is null ||
                            attempt.AttemptKind != AttemptKind.Placement ||
                            attempt.UserId != userId)
                        {
                            response = ApiResponse<PlacementTestResultResponse>.NotFound(
                                NotFoundAttemptMessage,
                                errorCode: ErrorAttemptNotFound,
                                retryable: false);
                            return;
                        }

                        if (attempt.IsDeleted)
                        {
                            if (IsSupersededExpiredAttempt(attempt))
                            {
                                response = ApiResponse<PlacementTestResultResponse>.Fail(
                                    "Lượt làm bài đã hết thời gian.",
                                    HttpStatusCode.Conflict,
                                    errorCode: ErrorAttemptExpired,
                                    retryable: false);
                                return;
                            }

                            response = ApiResponse<PlacementTestResultResponse>.NotFound(
                                NotFoundAttemptMessage,
                                errorCode: ErrorAttemptNotFound,
                                retryable: false);
                            return;
                        }

                        if (attempt.IsCompleted)
                        {
                            var canonicalCompleted = RecoverResultFromCompletedAttempt(attempt);
                            if (canonicalCompleted is null)
                            {
                                response = ApiResponse<PlacementTestResultResponse>.Fail(
                                    "Lượt làm bài không có snapshot kết quả hợp lệ.",
                                    HttpStatusCode.Conflict,
                                    errorCode: ErrorAttemptNotReady,
                                    retryable: false);
                                return;
                            }

                            response = ApiResponse<PlacementTestResultResponse>.Success(
                                canonicalCompleted,
                                HttpStatusCode.OK);
                            return;
                        }

                        if (!TryDeserializeSnapshot(attempt.QuestionSnapshotJson, out var snapshotDoc) ||
                            !HasValidSnapshotQuestions(snapshotDoc.Questions))
                        {
                            response = ApiResponse<PlacementTestResultResponse>.Fail(
                                "Lượt làm bài không có snapshot câu hỏi hợp lệ. Vui lòng bắt đầu lại.",
                                HttpStatusCode.Conflict,
                                errorCode: ErrorAttemptNotReady,
                                retryable: false);
                            return;
                        }

                        var serverNow = EnsureUtc(UtcNowProvider());
                        var expiresAt = EnsureUtc(attempt.ExpiresAt ?? snapshotDoc.ExpiresAt);
                        if (serverNow >= expiresAt)
                        {
                            response = ApiResponse<PlacementTestResultResponse>.Fail(
                                "Lượt làm bài đã hết thời gian.",
                                HttpStatusCode.Conflict,
                                errorCode: ErrorAttemptExpired,
                                retryable: false);
                            return;
                        }

                        if (request?.Answers is null)
                        {
                            response = ApiResponse<PlacementTestResultResponse>.Fail(
                                "Danh sách câu trả lời không hợp lệ.",
                                HttpStatusCode.BadRequest,
                                errorCode: ErrorAnswerInvalid,
                                retryable: false);
                            return;
                        }

                        if (request.Answers.Any(answer => answer is null))
                        {
                            response = ApiResponse<PlacementTestResultResponse>.Fail(
                                "Danh sách câu trả lời có phần tử không hợp lệ.",
                                HttpStatusCode.BadRequest,
                                errorCode: ErrorAnswerInvalid,
                                retryable: false);
                            return;
                        }

                        var duplicateQuestionIds = request.Answers
                            .GroupBy(answer => answer.QuestionId)
                            .Where(group => group.Count() > 1)
                            .Select(group => group.Key)
                            .OrderBy(id => id)
                            .ToList();
                        if (duplicateQuestionIds.Count > 0)
                        {
                            response = ApiResponse<PlacementTestResultResponse>.Fail(
                                $"Không được gửi lặp câu hỏi: {string.Join(", ", duplicateQuestionIds)}.",
                                HttpStatusCode.BadRequest,
                                errorCode: ErrorAnswerInvalid,
                                retryable: false);
                            return;
                        }

                        var snapshotQuestionsById = snapshotDoc.Questions.ToDictionary(q => q.QuestionId);
                        var invalidQuestionIds = request.Answers
                            .Select(answer => answer.QuestionId)
                            .Where(questionId => !snapshotQuestionsById.ContainsKey(questionId))
                            .OrderBy(questionId => questionId)
                            .ToList();
                        if (invalidQuestionIds.Count > 0)
                        {
                            response = ApiResponse<PlacementTestResultResponse>.Fail(
                                $"Câu hỏi không thuộc lượt làm bài: {string.Join(", ", invalidQuestionIds)}.",
                                HttpStatusCode.BadRequest,
                                errorCode: ErrorAnswerInvalid,
                                retryable: false);
                            return;
                        }

                        foreach (var submittedAnswer in request.Answers)
                        {
                            var frozenQuestion = snapshotQuestionsById[submittedAnswer.QuestionId];
                            if (submittedAnswer.SelectedOptionId is not null &&
                                !frozenQuestion.Options.Any(o => o.OptionId == submittedAnswer.SelectedOptionId.Value))
                            {
                                response = ApiResponse<PlacementTestResultResponse>.Fail(
                                    $"Lựa chọn không thuộc câu hỏi {submittedAnswer.QuestionId}.",
                                    HttpStatusCode.BadRequest,
                                    errorCode: ErrorAnswerInvalid,
                                    retryable: false);
                                return;
                            }
                        }

                        foreach (var q in snapshotDoc.Questions)
                        {
                            q.SelectedOptionId = null;
                            q.IsCorrect = false;
                        }

                        foreach (var submittedAnswer in request.Answers)
                        {
                            var frozenQuestion = snapshotQuestionsById[submittedAnswer.QuestionId];
                            frozenQuestion.SelectedOptionId = submittedAnswer.SelectedOptionId;
                            frozenQuestion.IsCorrect = submittedAnswer.SelectedOptionId.HasValue &&
                                submittedAnswer.SelectedOptionId.Value == frozenQuestion.CorrectOptionId;
                        }

                        var existingAnswerRows = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(
                            attemptId,
                            transactionCancellationToken);
                        if (existingAnswerRows.Count > 0)
                        {
                            var answerRowsByQuestionId = existingAnswerRows.ToDictionary(a => a.MockQuestionId);
                            foreach (var frozenQuestion in snapshotDoc.Questions)
                            {
                                if (answerRowsByQuestionId.TryGetValue(frozenQuestion.QuestionId, out var answerRow))
                                {
                                    answerRow.SelectedOptionId = frozenQuestion.SelectedOptionId;
                                    answerRow.IsCorrect = frozenQuestion.IsCorrect;
                                }
                            }
                        }

                        var sectionResults = BuildSectionResultsFromSnapshot(attemptId, snapshotDoc.Questions);
                        var existingSectionResults = await unitOfWork.MockTestSectionResults.GetByAttemptIdAsync(
                            attemptId,
                            transactionCancellationToken);
                        if (existingSectionResults.Count == 0 && sectionResults.Entities.Count > 0)
                        {
                            await unitOfWork.MockTestSectionResults.AddRangeAsync(
                                sectionResults.Entities,
                                transactionCancellationToken);
                        }

                        var evaluation = EvaluateLevelsFromSnapshot(
                            snapshotDoc.Questions,
                            snapshotDoc.CatalogLevels,
                            sectionResults.OverallScore);

                        var canonicalResult = new PlacementTestResultResponse
                        {
                            AttemptId = attempt.Id,
                            AttemptType = AttemptTypePlacement,
                            Status = StatusCompleted,
                            StartedAt = EnsureUtc(attempt.StartedAt),
                            SubmittedAt = serverNow,
                            TotalScore = sectionResults.OverallScore,
                            CorrectCount = snapshotDoc.Questions.Count(q => q.IsCorrect),
                            TotalQuestions = snapshotDoc.Questions.Count,
                            AssessedJlptLevelId = evaluation.AssessedLevelId,
                            AssessedJlptLevelName = evaluation.AssessedLevelName,
                            RecommendedTargetJlptLevelId = evaluation.RecommendedTargetLevelId,
                            RecommendedTargetJlptLevelName = evaluation.RecommendedTargetLevelName,
                            WrongQuestionIds = snapshotDoc.Questions
                                .Where(q => !q.IsCorrect)
                                .Select(q => q.QuestionId)
                                .OrderBy(id => id)
                                .ToList(),
                            LevelBreakdowns = evaluation.LevelBreakdowns,
                            Sections = sectionResults.Responses,
                        };

                        snapshotDoc.CompletedResult = canonicalResult;
                        attempt.SubmittedAt = serverNow;
                        attempt.IsCompleted = true;
                        attempt.TotalScore = sectionResults.OverallScore;
                        attempt.QuestionSnapshotJson = JsonSerializer.Serialize(snapshotDoc, SnapshotJsonOptions);
                        unitOfWork.MockTestAttempts.Update(attempt);

                        // Assessment comes only from this owned, immutable Placement result;
                        // it never changes the learner's chosen starting level, goal, or active path.
                        var learner = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(
                            userId, transactionCancellationToken);
                        if (learner is not null)
                        {
                            learner.AssessedCertificateLevelId = evaluation.AssessedLevelId;
                            learner.PlacementDecision = PlacementDecision.Completed;
                            unitOfWork.UserAccounts.Update(learner);
                        }

                        await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                        response = ApiResponse<PlacementTestResultResponse>.Success(
                            canonicalResult,
                            HttpStatusCode.OK);
                    }, cancellationToken);
                }
                catch (DbUpdateException)
                {
                    var latestAttempt = await LoadPlacementAttemptByIdAsync(attemptId, cancellationToken);
                    if (latestAttempt is not null &&
                        !latestAttempt.IsDeleted &&
                        latestAttempt.AttemptKind == AttemptKind.Placement &&
                        latestAttempt.UserId == userId &&
                        latestAttempt.IsCompleted)
                    {
                        var canonicalCompleted = RecoverResultFromCompletedAttempt(latestAttempt);
                        if (canonicalCompleted is not null)
                        {
                            return ApiResponse<PlacementTestResultResponse>.Success(
                                canonicalCompleted,
                                HttpStatusCode.OK);
                        }

                        return ApiResponse<PlacementTestResultResponse>.Fail(
                            "Lượt làm bài không có snapshot kết quả hợp lệ.",
                            HttpStatusCode.Conflict,
                            errorCode: ErrorAttemptNotReady,
                            retryable: false);
                    }

                    return ApiResponse<PlacementTestResultResponse>.Fail(
                        "Không thể đồng bộ kết quả với máy chủ. Vui lòng thử lại.",
                        HttpStatusCode.Conflict,
                        errorCode: ErrorAttemptNotReady,
                        retryable: true);
                }

                return response!;
            }
            catch (Exception)
            {
                return ApiResponse<PlacementTestResultResponse>.Fail(
                    "Không thể chấm điểm bài kiểm tra đánh giá năng lực.",
                    HttpStatusCode.InternalServerError);
            }
            finally
            {
                attemptLock?.Release();
            }
        }

        public async Task<ApiResponse<PlacementTestResultResponse>> GetPlacementTestResultAsync(
            int attemptId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var attempt = await LoadPlacementAttemptByIdAsync(attemptId, cancellationToken);
                if (attempt is null ||
                    attempt.IsDeleted ||
                    attempt.AttemptKind != AttemptKind.Placement ||
                    attempt.UserId != userId)
                {
                    return ApiResponse<PlacementTestResultResponse>.NotFound(
                        NotFoundAttemptMessage,
                        errorCode: ErrorAttemptNotFound,
                        retryable: false);
                }

                if (!attempt.IsCompleted)
                {
                    return ApiResponse<PlacementTestResultResponse>.Fail(
                        "Lượt làm bài này chưa được nộp/chấm điểm.",
                        HttpStatusCode.Conflict,
                        errorCode: ErrorAttemptNotCompleted,
                        retryable: false);
                }

                var canonicalResult = RecoverResultFromCompletedAttempt(attempt);
                if (canonicalResult is null)
                {
                    return ApiResponse<PlacementTestResultResponse>.Fail(
                        "Lượt làm bài không có snapshot kết quả hợp lệ.",
                        HttpStatusCode.Conflict,
                        errorCode: ErrorAttemptNotReady,
                        retryable: false);
                }

                return ApiResponse<PlacementTestResultResponse>.Success(canonicalResult);
            }
            catch (Exception)
            {
                return ApiResponse<PlacementTestResultResponse>.Fail(
                    "Không thể tải kết quả bài kiểm tra đánh giá năng lực.",
                    HttpStatusCode.InternalServerError);
            }
        }

        internal static IReadOnlyDictionary<string, double>? TryExtractSkillBreakdownFromSnapshot(string? snapshotJson)
        {
            var completedResult = TryGetCompletedResultFromSnapshot(snapshotJson);
            if (completedResult?.Sections is not { Count: > 0 } sections)
            {
                return null;
            }

            var dict = sections
                .Where(s => !string.IsNullOrWhiteSpace(s.SectionTitle))
                .GroupBy(s => s.SectionTitle)
                .ToDictionary(g => g.Key, g => g.Average(x => x.ScorePercent));
            return dict.Count > 0 ? dict : null;
        }

        private async Task LockUserForPlacementStartAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            if (unitOfWork.UserAccounts is null)
            {
                return;
            }

            try
            {
                await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, cancellationToken);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private async Task<(MockTestAttempt Attempt, PlacementAttemptSnapshotDocument Snapshot)?>
            TryFindCanonicalActiveAttemptAsync(
                int userId,
                DateTime serverNowUtc,
                CancellationToken cancellationToken)
        {
            var attempts = await LoadUserPlacementAttemptsAsync(userId, cancellationToken);
            return FindCanonicalActiveAttempt(attempts, serverNowUtc);
        }

        private static (MockTestAttempt Attempt, PlacementAttemptSnapshotDocument Snapshot)?
            FindCanonicalActiveAttempt(
                IEnumerable<MockTestAttempt> placementAttempts,
                DateTime serverNowUtc)
        {
            foreach (var candidate in placementAttempts
                .Where(a => !a.IsDeleted &&
                            a.AttemptKind == AttemptKind.Placement &&
                            !a.IsCompleted &&
                            a.ExpiresAt.HasValue &&
                            EnsureUtc(a.ExpiresAt.Value) > serverNowUtc)
                .OrderByDescending(a => a.StartedAt)
                .ThenByDescending(a => a.Id))
            {
                if (TryDeserializeSnapshot(candidate.QuestionSnapshotJson, out var snapshotDoc) &&
                    HasValidSnapshotQuestions(snapshotDoc.Questions))
                {
                    return (candidate, snapshotDoc);
                }
            }

            return null;
        }

        private async Task<List<MockTestAttempt>> LoadUserPlacementAttemptsAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var attempts = await unitOfWork.MockTestAttempts.GetPlacementByUserAsync(userId, cancellationToken);
            if (attempts is { Count: > 0 })
            {
                return attempts
                    .Where(a => !a.IsDeleted && a.AttemptKind == AttemptKind.Placement && a.UserId == userId)
                    .OrderByDescending(a => a.StartedAt)
                    .ThenByDescending(a => a.Id)
                    .ToList();
            }

            var fallback = await unitOfWork.MockTestAttempts.FindAsync(
                a => a.UserId == userId && !a.IsDeleted && a.AttemptKind == AttemptKind.Placement,
                cancellationToken);
            return (fallback ?? [])
                .Where(a => !a.IsDeleted && a.AttemptKind == AttemptKind.Placement && a.UserId == userId)
                .OrderByDescending(a => a.StartedAt)
                .ThenByDescending(a => a.Id)
                .ToList();
        }

        private async Task<MockTestAttempt?> LoadPlacementAttemptByIdAsync(
            int attemptId,
            CancellationToken cancellationToken)
        {
            var attempt = await unitOfWork.MockTestAttempts.GetPlacementByIdAsync(attemptId, cancellationToken);
            attempt ??= await unitOfWork.MockTestAttempts.GetByIdAsync(attemptId, cancellationToken);
            if (attempt is null || attempt.IsDeleted || attempt.AttemptKind != AttemptKind.Placement)
            {
                return null;
            }

            return attempt;
        }

        private async Task<MockTestAttempt?> LoadPlacementAttemptForSubmissionAsync(
            int attemptId,
            CancellationToken cancellationToken)
        {
            MockTestAttempt? attempt = null;
            try
            {
                attempt = await unitOfWork.MockTestAttempts.GetPlacementForSubmissionAsync(
                    attemptId,
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                attempt = await unitOfWork.MockTestAttempts.GetByIdAsync(attemptId, cancellationToken);
            }

            attempt ??= await unitOfWork.MockTestAttempts.GetByIdAsync(attemptId, cancellationToken);
            if (attempt is null || attempt.AttemptKind != AttemptKind.Placement)
            {
                return null;
            }

            return attempt;
        }

        private static bool IsSupersededExpiredAttempt(MockTestAttempt attempt)
            => attempt.IsDeleted &&
               !attempt.IsCompleted &&
               TryDeserializeSnapshot(attempt.QuestionSnapshotJson, out var snapshotDoc) &&
               snapshotDoc.IsSupersededExpired;

        private static PlacementTestResultResponse? RecoverResultFromCompletedAttempt(MockTestAttempt attempt)
        {
            var cached = TryGetCompletedResultFromSnapshot(attempt.QuestionSnapshotJson);
            if (cached is null)
            {
                return null;
            }

            cached.AttemptId = attempt.Id;
            cached.AttemptType = AttemptTypePlacement;
            cached.Status = StatusCompleted;
            cached.StartedAt = EnsureUtc(attempt.StartedAt);
            if (attempt.SubmittedAt.HasValue)
            {
                cached.SubmittedAt = EnsureUtc(attempt.SubmittedAt.Value);
            }

            return cached;
        }

        internal static PlacementTestResultResponse? TryGetCompletedResultFromSnapshot(string? snapshotJson)
        {
            if (!TryDeserializeSnapshot(snapshotJson, out var snapshotDoc) ||
                snapshotDoc.CompletedResult is not { } completedResult ||
                !IsValidCompletedResultSnapshot(completedResult))
            {
                return null;
            }

            return completedResult;
        }

        private static bool IsValidCompletedResultSnapshot(PlacementTestResultResponse result)
            => result.TotalQuestions > 0 &&
               result.CorrectCount >= 0 &&
               result.CorrectCount <= result.TotalQuestions &&
               result.TotalScore >= 0 &&
               result.TotalScore <= 100 &&
               result.AssessedJlptLevelId > 0 &&
               IsSupportedLevel(result.AssessedJlptLevelName) &&
               result.RecommendedTargetJlptLevelId > 0 &&
               IsSupportedLevel(result.RecommendedTargetJlptLevelName) &&
               result.LevelBreakdowns is { Count: > 0 } &&
               result.Sections is { Count: > 0 };

        private static bool HasValidSnapshotQuestions(List<PlacementSnapshotQuestionItem>? questions)
            => questions is { Count: > 0 } &&
               questions.All(q =>
                   q is not null &&
                   q.QuestionId > 0 &&
                   q.JlptLevelId > 0 &&
                   IsSupportedLevel(q.JlptLevelName));

        private static PlacementAttemptSnapshotResponse BuildSnapshotResponse(
            MockTestAttempt attempt,
            PlacementAttemptSnapshotDocument snapshotDoc,
            DateTime serverNowUtc,
            bool isResumed)
        {
            var startedAt = EnsureUtc(attempt.StartedAt);
            var expiresAt = EnsureUtc(attempt.ExpiresAt ?? snapshotDoc.ExpiresAt);

            return new StartPlacementTestResponse
            {
                AttemptId = attempt.Id,
                AttemptType = AttemptTypePlacement,
                Status = StatusInProgress,
                IsResumed = isResumed,
                StartedAt = startedAt,
                ExpiresAt = expiresAt,
                ServerNow = EnsureUtc(serverNowUtc),
                TimeLimitMinutes = snapshotDoc.TimeLimitMinutes,
                TotalQuestions = snapshotDoc.Questions.Count,
                Questions = snapshotDoc.Questions
                    .OrderBy(q => q.Order)
                    .ThenBy(q => q.QuestionId)
                    .Select(q => new PlacementTestQuestionResponse
                    {
                        QuestionId = q.QuestionId,
                        MockTestId = q.MockTestId,
                        MockTestSectionId = q.MockTestSectionId,
                        SectionTitle = q.SectionTitle,
                        SectionOrder = q.SectionOrder,
                        JlptLevelId = q.JlptLevelId,
                        JlptLevelName = q.JlptLevelName,
                        Order = q.Order,
                        Content = q.Content,
                        AudioUrl = q.AudioUrl,
                        ImageUrl = q.ImageUrl,
                        Options = q.Options
                            .OrderBy(o => o.Order)
                            .ThenBy(o => o.OptionId)
                            .Select(o => new PlacementTestOptionResponse
                            {
                                OptionId = o.OptionId,
                                Content = o.Content,
                                Order = o.Order,
                            })
                            .ToList(),
                    })
                    .ToList(),
            };
        }

        private static (
            int AssessedLevelId,
            string AssessedLevelName,
            int RecommendedTargetLevelId,
            string RecommendedTargetLevelName,
            List<PlacementLevelBreakdownItem> LevelBreakdowns) EvaluateLevelsFromSnapshot(
            List<PlacementSnapshotQuestionItem> questions,
            List<PlacementSnapshotLevelCatalogItem> catalogLevels,
            double overallScore)
        {
            var breakdowns = questions
                .Where(q => IsSupportedLevel(q.JlptLevelName))
                .GroupBy(q => (
                    LevelId: q.JlptLevelId,
                    LevelCode: NormalizeLevelCode(q.JlptLevelName),
                    Rank: GetLevelRank(q.JlptLevelName)))
                .OrderBy(group => group.Key.Rank)
                .ThenBy(group => group.Key.LevelId)
                .Select(group =>
                {
                    var total = group.Count();
                    var correct = group.Count(q => q.IsCorrect);
                    var score = total == 0 ? 0d : Math.Round(correct * 100d / total, 2);
                    return new PlacementLevelBreakdownItem
                    {
                        JlptLevelId = group.Key.LevelId,
                        JlptLevelName = group.Key.LevelCode,
                        CorrectCount = correct,
                        TotalCount = total,
                        ScorePercent = score,
                    };
                })
                .ToList();

            var knownLevels = (catalogLevels ?? [])
                .Select(l => (LevelId: l.JlptLevelId, LevelCode: NormalizeLevelCode(l.JlptLevelName), Rank: GetLevelRank(l.JlptLevelName)))
                .Concat(breakdowns.Select(b => (LevelId: b.JlptLevelId, LevelCode: b.JlptLevelName, Rank: GetLevelRank(b.JlptLevelName))))
                .Where(x => x.Rank >= 1 && x.Rank <= 3)
                .GroupBy(x => x.LevelCode)
                .Select(g => g.First())
                .OrderBy(x => x.Rank)
                .ToList();

            if (knownLevels.Count == 0)
            {
                knownLevels.Add((LevelId: 1, LevelCode: "N5", Rank: 1));
            }

            (int LevelId, string LevelCode, int Rank) assessed;
            if (breakdowns.Count > 1)
            {
                var passedLevels = breakdowns
                    .Where(b => b.ScorePercent >= 60.0)
                    .OrderBy(b => GetLevelRank(b.JlptLevelName))
                    .ToList();
                if (passedLevels.Count > 0)
                {
                    var highestPassed = passedLevels[^1];
                    assessed = (
                        highestPassed.JlptLevelId,
                        highestPassed.JlptLevelName,
                        GetLevelRank(highestPassed.JlptLevelName));
                }
                else
                {
                    var lowest = breakdowns[0];
                    assessed = (
                        lowest.JlptLevelId,
                        lowest.JlptLevelName,
                        GetLevelRank(lowest.JlptLevelName));
                }
            }
            else
            {
                var targetCode = overallScore >= 80.0
                    ? "N3"
                    : overallScore >= 50.0
                        ? "N4"
                        : "N5";
                var matched = knownLevels.FirstOrDefault(l => l.LevelCode == targetCode);
                if (matched.LevelCode is not null)
                {
                    assessed = matched;
                }
                else if (breakdowns.Count == 1)
                {
                    assessed = (
                        breakdowns[0].JlptLevelId,
                        breakdowns[0].JlptLevelName,
                        GetLevelRank(breakdowns[0].JlptLevelName));
                }
                else
                {
                    assessed = knownLevels[0];
                }
            }

            var assessedBreakdown = breakdowns.FirstOrDefault(b => b.JlptLevelName == assessed.LevelCode);
            var assessedScore = assessedBreakdown?.ScorePercent ?? overallScore;

            (int LevelId, string LevelCode, int Rank) recommended = assessed;
            if (assessedScore >= 75.0)
            {
                var nextLevel = knownLevels
                    .Where(l => l.Rank > assessed.Rank && l.Rank <= 3)
                    .OrderBy(l => l.Rank)
                    .FirstOrDefault();
                if (nextLevel.LevelCode is not null)
                {
                    recommended = nextLevel;
                }
            }

            return (
                assessed.LevelId,
                assessed.LevelCode,
                recommended.LevelId,
                recommended.LevelCode,
                breakdowns);
        }

        private static (List<MockTestSectionResult> Entities, List<SectionResultItem> Responses, double OverallScore)
            BuildSectionResultsFromSnapshot(
                int attemptId,
                List<PlacementSnapshotQuestionItem> questions)
        {
            var grouped = questions
                .GroupBy(q => (
                    q.MockTestSectionId,
                    q.SectionTitle,
                    q.SectionOrder,
                    q.LanguageSkillName))
                .OrderBy(g => g.Key.SectionOrder)
                .ThenBy(g => g.Key.MockTestSectionId)
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(q => q.IsCorrect);
                    var score = total == 0 ? 0d : Math.Round(correct * 100d / total, 2);
                    return (
                        SectionId: g.Key.MockTestSectionId,
                        SectionTitle: g.Key.SectionTitle,
                        LanguageSkillName: g.Key.LanguageSkillName,
                        Correct: correct,
                        Total: total,
                        Score: score);
                })
                .ToList();

            var entities = grouped
                .Where(g => g.SectionId > 0)
                .Select(g => new MockTestSectionResult
                {
                    MockTestAttemptId = attemptId,
                    MockTestSectionId = g.SectionId,
                    CorrectCount = g.Correct,
                    TotalCount = g.Total,
                    ScorePercent = g.Score,
                })
                .ToList();

            var responses = grouped.Select(g => new SectionResultItem
            {
                MockTestSectionId = g.SectionId,
                SectionTitle = g.SectionTitle,
                LanguageSkillName = g.LanguageSkillName,
                CorrectCount = g.Correct,
                TotalCount = g.Total,
                Score = g.Score,
                ScorePercent = g.Score,
            }).ToList();

            var totalCorrect = questions.Count(q => q.IsCorrect);
            var totalCount = questions.Count;
            var overallScore = totalCount == 0 ? 0d : Math.Round(totalCorrect * 100d / totalCount, 2);

            return (entities, responses, overallScore);
        }

        private static bool TryDeserializeSnapshot(
            string? json,
            out PlacementAttemptSnapshotDocument document)
        {
            document = new PlacementAttemptSnapshotDocument();
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<PlacementAttemptSnapshotDocument>(json, SnapshotJsonOptions);
                if (parsed is null)
                {
                    return false;
                }

                document = parsed;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool HasUsablePrompt(MockQuestion question)
            => !string.IsNullOrWhiteSpace(question.QuestionText) ||
               !string.IsNullOrWhiteSpace(question.ImageUrl) ||
               !string.IsNullOrWhiteSpace(question.AudioUrl);

        private static bool IsSupportedLevel(string? levelCode)
        {
            var normalized = NormalizeLevelCode(levelCode);
            return normalized is "N5" or "N4" or "N3";
        }

        private static int ComputeTimeLimitMinutes(int questionCount)
            => Math.Clamp(questionCount * 2, 10, 45);

        private static string NormalizeLevelCode(string? rawCode)
        {
            var trimmed = (rawCode ?? string.Empty).Trim().ToUpperInvariant();
            return trimmed switch
            {
                "N5" or "JLPT N5" => "N5",
                "N4" or "JLPT N4" => "N4",
                "N3" or "JLPT N3" => "N3",
                "N2" or "JLPT N2" => "N2",
                "N1" or "JLPT N1" => "N1",
                _ => trimmed,
            };
        }

        private static int GetLevelRank(string? code) => NormalizeLevelCode(code) switch
        {
            "N5" => 1,
            "N4" => 2,
            "N3" => 3,
            "N2" => 4,
            "N1" => 5,
            _ => 99,
        };

        private static DateTime EnsureUtc(DateTime value)
            => BusinessDateHelper.EnsureUtc(value);

        internal sealed class PlacementAttemptSnapshotDocument
        {
            public int SchemaVersion { get; set; } = 2;
            public int TimeLimitMinutes { get; set; }
            public DateTime StartedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public bool IsSupersededExpired { get; set; }
            public List<PlacementSnapshotLevelCatalogItem> CatalogLevels { get; set; } = [];
            public List<PlacementSnapshotQuestionItem> Questions { get; set; } = [];
            public PlacementTestResultResponse? CompletedResult { get; set; }
        }

        internal sealed class PlacementSnapshotLevelCatalogItem
        {
            public int JlptLevelId { get; set; }
            public string JlptLevelName { get; set; } = string.Empty;
            public int Rank { get; set; }
        }

        internal sealed class PlacementSnapshotQuestionItem
        {
            public int QuestionId { get; set; }
            public int MockTestId { get; set; }
            public int MockTestSectionId { get; set; }
            public string SectionTitle { get; set; } = string.Empty;
            public int SectionOrder { get; set; }
            public string LanguageSkillName { get; set; } = string.Empty;
            public int JlptLevelId { get; set; }
            public string JlptLevelName { get; set; } = string.Empty;
            public int Order { get; set; }
            public string Content { get; set; } = string.Empty;
            public string? AudioUrl { get; set; }
            public string? ImageUrl { get; set; }
            public int CorrectOptionId { get; set; }
            public int? SelectedOptionId { get; set; }
            public bool IsCorrect { get; set; }
            public List<PlacementSnapshotOptionItem> Options { get; set; } = [];
        }

        internal sealed class PlacementSnapshotOptionItem
        {
            public int OptionId { get; set; }
            public string Content { get; set; } = string.Empty;
            public int Order { get; set; }
            public bool IsCorrect { get; set; }
        }
    }
}
