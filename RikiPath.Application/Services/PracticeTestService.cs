using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MockTests;
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

        public const string InvalidJlptExamMonthCode = "JLPT_EXAM_MONTH_INVALID";
        public const string InvalidJlptExamMonthMessage = "Ngày dự định thi JLPT phải nằm trong tháng 7 hoặc tháng 12.";

        public static bool IsJlptExamMonth(DateTime date) => NormalizeExamDate(date).Month is 7 or 12;

        public static int GetDaysUntilExam(DateTime targetExamDate, DateTime utcNow)
            => (NormalizeExamDate(targetExamDate) - GetVietnamToday(utcNow)).Days;

        public static bool IsFutureExamDate(DateTime? targetExamDate, DateTime utcNow)
            => targetExamDate.HasValue && GetDaysUntilExam(targetExamDate.Value, utcNow) >= 1;
    }

    public class PracticeTestService(IUnitOfWork unitOfWork, IClaimService claimService) : IPracticeTestService
    {
        public const string AttemptTypePlacement = "placement";
        // Published option IDs are positive. This choice is only emitted for Placement.
        private const int NotStudiedOptionId = 0;
        private const string NotStudiedResponseJson = "{\"kind\":\"not_studied\"}";
        public const string StatusInProgress = "inProgress";
        public const string StatusCompleted = "completed";

        public const string ErrorBankNotReady = "PLACEMENT_BANK_NOT_READY";
        public const string ErrorAttemptNotFound = "PLACEMENT_ATTEMPT_NOT_FOUND";
        public const string ErrorAttemptExpired = "PLACEMENT_ATTEMPT_EXPIRED";
        public const string ErrorAttemptReplaced = "PLACEMENT_ATTEMPT_REPLACED";
        public const string ErrorAnswerInvalid = "PLACEMENT_ANSWER_INVALID";
        public const string ErrorAttemptNotReady = "PLACEMENT_ATTEMPT_NOT_READY";
        public const string ErrorAttemptNotCompleted = "PLACEMENT_ATTEMPT_NOT_COMPLETED";

        private const int MinimumEligibleQuestionsPerLevel = 2;
        private const int DefaultQuestionsPerLevel = 12;
        private const string NotFoundAttemptMessage = "Không tìm thấy lượt làm bài đánh giá năng lực.";

        private static readonly string[] SupportedLevels = ["N5", "N4", "N3", "N2"];
        private static readonly string[] RequiredLevels = ["N5", "N4", "N3"];
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

                var targetId = GetAdaptiveGoalId(user);
                var activeAttempt = FindCanonicalActiveAttempt(
                    placementAttempts, serverNow, targetId)?.Attempt;
                var completedWithSnapshot = FindLatestCompletedAssessment(placementAttempts, targetId);

                var latestCompleted = completedWithSnapshot.Attempt;
                var recovered = completedWithSnapshot.Recovered;

                int? assessedLevelId = recovered is { AssessedJlptLevelId: > 0 } ? recovered.AssessedJlptLevelId : null;
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

                var hasValidExamDate = user.TargetExamDate is not { } examDate ||
                    (BusinessDateHelper.IsJlptExamMonth(examDate) &&
                     BusinessDateHelper.IsFutureExamDate(examDate, serverNow));
                var isReadyForLearningPath =
                    (user.StartingCertificateLevelId ?? user.TargetCertificateLevelId).HasValue &&
                    user.TargetCertificateLevelId.HasValue &&
                    hasValidExamDate &&
                    user.DailyStudyMinutes > 0 &&
                    recovered is not null;

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

        public async Task<ApiResponse<PlacementAnswersSavedResponse>> SavePlacementAnswersAsync(
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

                ApiResponse<PlacementAnswersSavedResponse>? response = null;
                await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
                {
                    await LockUserForPlacementStartAsync(userId, transactionCancellationToken);
                    var attempt = await LoadPlacementAttemptForSubmissionAsync(attemptId, transactionCancellationToken);
                    var now = EnsureUtc(UtcNowProvider());
                    if (attempt is null || attempt.UserId != userId ||
                        (attempt.IsDeleted && !CanSubmitEarlyClosedAttempt(attempt, now)))
                    {
                        response = ApiResponse<PlacementAnswersSavedResponse>.NotFound(
                            NotFoundAttemptMessage, errorCode: ErrorAttemptNotFound, retryable: false);
                        return;
                    }
                    if (attempt.IsCompleted)
                    {
                        response = ApiResponse<PlacementAnswersSavedResponse>.Fail(
                            "Lượt làm bài đã được nộp.", HttpStatusCode.Conflict,
                            errorCode: ErrorAttemptNotReady, retryable: false);
                        return;
                    }
                    if (request?.Answers is null || request.Answers.Count == 0 || request.Answers.Any(a => a is null))
                    {
                        response = ApiResponse<PlacementAnswersSavedResponse>.Fail(
                            "Chưa có lựa chọn hợp lệ để lưu.", HttpStatusCode.BadRequest,
                            errorCode: ErrorAnswerInvalid, retryable: false);
                        return;
                    }
                    if (!TryDeserializeSnapshot(attempt.QuestionSnapshotJson, out var snapshotDoc) ||
                        !HasValidSnapshotQuestions(snapshotDoc.Questions))
                    {
                        response = ApiResponse<PlacementAnswersSavedResponse>.Fail(
                            "Lượt làm bài không có snapshot câu hỏi hợp lệ.", HttpStatusCode.Conflict,
                            errorCode: ErrorAttemptNotReady, retryable: false);
                        return;
                    }

                    var expiresAt = EnsureUtc(attempt.ExpiresAt ?? snapshotDoc.ExpiresAt);
                    if (now >= expiresAt)
                    {
                        response = ApiResponse<PlacementAnswersSavedResponse>.Fail(
                            "Lượt làm bài đã hết thời gian.", HttpStatusCode.Conflict,
                            errorCode: ErrorAttemptExpired, retryable: false);
                        return;
                    }

                    var duplicates = request.Answers.GroupBy(a => a.QuestionId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                    if (duplicates.Count > 0)
                    {
                        response = ApiResponse<PlacementAnswersSavedResponse>.Fail(
                            $"Không được gửi lặp câu hỏi: {string.Join(", ", duplicates)}.", HttpStatusCode.BadRequest,
                            errorCode: ErrorAnswerInvalid, retryable: false);
                        return;
                    }

                    var questionsById = snapshotDoc.Questions.ToDictionary(q => q.QuestionId);
                    foreach (var answer in request.Answers)
                    {
                        if (!questionsById.TryGetValue(answer.QuestionId, out var question) ||
                            !IsValidPlacementSelection(question, answer))
                        {
                            response = ApiResponse<PlacementAnswersSavedResponse>.Fail(
                                $"Câu hỏi hoặc lựa chọn không thuộc lượt làm bài (QuestionId: {answer.QuestionId}).",
                                HttpStatusCode.BadRequest, errorCode: ErrorAnswerInvalid, retryable: false);
                            return;
                        }
                    }

                    foreach (var answer in request.Answers)
                        ApplyPlacementSelection(questionsById[answer.QuestionId], answer);

                    var answerRows = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attemptId, transactionCancellationToken);
                    var answersByQuestionId = answerRows.ToDictionary(a => a.MockQuestionId);
                    foreach (var answer in request.Answers)
                    {
                        if (answersByQuestionId.TryGetValue(answer.QuestionId, out var row))
                            SetPersistedPlacementSelection(row, questionsById[answer.QuestionId]);
                    }

                    attempt.QuestionSnapshotJson = JsonSerializer.Serialize(snapshotDoc, SnapshotJsonOptions);
                    attempt.ModifiedDate = now;
                    unitOfWork.MockTestAttempts.Update(attempt);
                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                    response = ApiResponse<PlacementAnswersSavedResponse>.Success(new PlacementAnswersSavedResponse
                    {
                        AttemptId = attempt.Id,
                        SavedAt = now,
                        SavedQuestionCount = request.Answers.Count,
                    });
                }, cancellationToken);
                return response!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                return ApiResponse<PlacementAnswersSavedResponse>.Fail(
                    "Không thể lưu lựa chọn bài đánh giá năng lực.", HttpStatusCode.InternalServerError);
            }
            finally
            {
                attemptLock?.Release();
            }
        }

        public Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestAsync(
            CancellationToken cancellationToken = default)
            => StartPlacementTestInternalAsync(DefaultQuestionsPerLevel, retakeTargetAssessment: false, cancellationToken);

        public Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestAsync(
            StartPlacementTestRequest? request,
            CancellationToken cancellationToken = default)
        {
            var requestedQuestionsPerLevel = Math.Clamp(DefaultQuestionsPerLevel,
                MinimumEligibleQuestionsPerLevel,
                DefaultQuestionsPerLevel);
            return StartPlacementTestInternalAsync(requestedQuestionsPerLevel, request?.RetakeTargetAssessment == true, cancellationToken);
        }

        private async Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestInternalAsync(
            int questionsPerSupportedLevel,
            bool retakeTargetAssessment,
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
                var goal = unitOfWork.UserAccounts is null ? null :
                    await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
                var goalCode = NormalizeLevelCode(goal?.TargetCertificateLevel?.Code);
                var adaptiveGoalId = GetAdaptiveGoalId(goal);
                var attempts = await LoadUserPlacementAttemptsAsync(userId, cancellationToken);
                var preExistingActive = FindCanonicalActiveAttempt(attempts, serverNow, adaptiveGoalId);
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

                var latestTarget = retakeTargetAssessment && adaptiveGoalId is int targetLevelId
                    ? GetLatestTargetResult(attempts, targetLevelId) : null;
                var comparisonLevel = PlacementAssessmentModePolicy.ComparisonLevel(goalCode);
                var shortQuestionLimit = PlacementAssessmentModePolicy.ShortAssessmentQuestionsPerSkillAndLevel(
                    goalCode, goal?.DeclaredJlptCertificateCode);
                if (retakeTargetAssessment && (latestTarget is null || latestTarget.TotalScore >= 50))
                    return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                        "Bạn có thể làm lại bài khi kết quả gần nhất ở cấp mục tiêu dưới 50%.",
                        HttpStatusCode.Conflict, errorCode: "PLACEMENT_RETAKE_NOT_AVAILABLE");
                var previousQuestionIds = latestTarget is not null && TryDeserializeSnapshot(
                    attempts.FirstOrDefault(attempt => attempt.Id == latestTarget.AttemptId)?.QuestionSnapshotJson,
                    out var previousSnapshot)
                    ? previousSnapshot.Questions.Select(question => question.QuestionId).ToHashSet()
                    : [];

                var tests = await unitOfWork.MockTests.GetAllWithSectionsAndQuestionsAsync(cancellationToken);
                var allLevels = (await unitOfWork.CertificateLevels.GetAllAsync(cancellationToken))
                    .Where(l => !l.IsDeleted && l.Id > 0 && !string.IsNullOrWhiteSpace(l.Code) &&
                        (!adaptiveGoalId.HasValue || l.CertificateTypeId == goal!.TargetCertificateLevel!.CertificateTypeId))
                    .ToList();

                var supportedCatalogLevels = allLevels
                    .Select(l => (LevelId: l.Id, LevelCode: NormalizeLevelCode(l.Code), Rank: GetLevelRank(l.Code)))
                    .Where(l => JlptCurriculum.Rank(l.LevelCode) > 0)
                    .GroupBy(l => l.LevelCode)
                    .Select(g => g.OrderBy(x => x.LevelId).First())
                    .OrderBy(l => l.Rank)
                    .ToDictionary(l => l.LevelCode);

                var levelById = allLevels
                    .Select(l => (LevelId: l.Id, LevelCode: NormalizeLevelCode(l.Code), Rank: GetLevelRank(l.Code)))
                    .Where(l => JlptCurriculum.Rank(l.LevelCode) > 0)
                    .ToDictionary(l => l.LevelId);
                var comparisonLevelId = comparisonLevel is not null && supportedCatalogLevels.TryGetValue(comparisonLevel, out var lowerLevel)
                    ? (int?)lowerLevel.LevelId : null;

                var eligibleCandidates = new List<(
                    MockTest Test,
                    MockTestSection Section,
                    MockQuestion Question,
                    List<MockQuestionOption> ValidOptions,
                    int? CorrectOptionId,
                    string[]? OrderingTokens,
                    string[]? OrderingAnswerKey,
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

                    if (resolvedCode is null || JlptCurriculum.Rank(resolvedCode) == 0 || resolvedLevelId <= 0 ||
                        (adaptiveGoalId.HasValue && resolvedLevelId != adaptiveGoalId.Value && resolvedLevelId != comparisonLevelId))
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
                        if (JlptSkillAssessmentPolicy.Classify(section.LanguageSkill?.Name) is null) continue;
                        var orderedQuestions = (section.Questions ?? [])
                            .Where(q => q is not null && !q.IsDeleted && q.Id > 0)
                            .OrderBy(q => q.SortOrder)
                            .ThenBy(q => q.Id);

                        foreach (var question in orderedQuestions)
                        {
                            // TODO: Require audio again after the placement question bank has listening recordings.
                            if (!HasUsablePrompt(question) || !seenQuestionIds.Add(question.Id))
                            {
                                continue;
                            }

                            if (SentenceOrdering.IsType(question.QuestionType))
                            {
                                if (JlptSkillAssessmentPolicy.Classify(section.LanguageSkill?.Name) == JlptStudySkill.Grammar &&
                                    SentenceOrdering.TryRead(question.InteractionDataJson, question.AnswerKeyJson, out var tokens, out var answerKey))
                                {
                                    eligibleCandidates.Add((test, section, question, [], null, tokens, answerKey,
                                        resolvedLevelId, resolvedCode, levelRank));
                                }
                                continue;
                            }

                            var validOptions = (question.Options ?? [])
                                .Where(o => o is not null &&
                                            !o.IsDeleted &&
                                            o.Id > 0 &&
                                            (!string.IsNullOrWhiteSpace(o.OptionText) || !string.IsNullOrWhiteSpace(o.ImageUrl)))
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
                                OrderingTokens: null,
                                OrderingAnswerKey: null,
                                LevelId: resolvedLevelId,
                                LevelCode: resolvedCode,
                                LevelRank: levelRank));
                        }
                    }
                }

                var groupedByLevel = eligibleCandidates
                    .GroupBy(c => c.LevelCode)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var selectedQuestions = eligibleCandidates.GetRange(0, 0);
                int? fullTestTimeLimit = null;
                if (adaptiveGoalId.HasValue && comparisonLevel is not null)
                {
                    var groups = (from skill in Enum.GetValues<JlptStudySkill>()
                                  from level in new[] { comparisonLevel, goalCode }
                                  select new
                                  {
                                      Skill = skill,
                                      Level = level,
                                      Candidates = eligibleCandidates.Where(candidate => candidate.LevelCode == level &&
                                          JlptSkillAssessmentPolicy.Classify(candidate.Section.LanguageSkill?.Name) == skill)
                                          .OrderByDescending(candidate => skill == JlptStudySkill.Listening &&
                                              !string.IsNullOrWhiteSpace(candidate.Question.AudioUrl))
                                          .ThenBy(candidate => previousQuestionIds.Contains(candidate.Question.Id))
                                          .ToList(),
                                  }).ToList();
                    var missing = groups.FirstOrDefault(group =>
                        group.Candidates.Count < PlacementAssessmentModePolicy.MinimumQuestionsPerSkillAndLevel);
                    if (missing is not null)
                        return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                            $"Ngân hàng đề cần ít nhất {PlacementAssessmentModePolicy.MinimumQuestionsPerSkillAndLevel} câu hợp lệ cho {JlptSkillAssessmentPolicy.Label(missing.Skill)} {missing.Level}.",
                            HttpStatusCode.Conflict, errorCode: ErrorBankNotReady, retryable: false);

                    var countPerGroup = Math.Min(shortQuestionLimit > 0 ? shortQuestionLimit : 5,
                        groups.Min(group => group.Candidates.Count));
                    foreach (var group in groups)
                    {
                        var ordering = group.Candidates.FindIndex(candidate => candidate.OrderingTokens is not null);
                        if (ordering >= countPerGroup)
                            group.Candidates[countPerGroup - 1] = group.Candidates[ordering];
                        selectedQuestions.AddRange(group.Candidates.Take(countPerGroup));
                    }

                    if (shortQuestionLimit > 0)
                        fullTestTimeLimit = selectedQuestions.Count * 5 / 4;
                }
                else if (adaptiveGoalId.HasValue)
                {
                    var fullTest = eligibleCandidates
                        .GroupBy(candidate => candidate.Test)
                        .Select(group => new
                        {
                            Test = group.Key,
                            Questions = group,
                            Skills = group.GroupBy(candidate => JlptSkillAssessmentPolicy.Classify(candidate.Section.LanguageSkill?.Name)).ToList()
                        })
                        .Where(test => test.Skills.Count(skill => skill.Key.HasValue && skill.Count() >= 3) == Enum.GetValues<JlptStudySkill>().Length)
                        .OrderBy(test => test.Questions.Count(candidate => previousQuestionIds.Contains(candidate.Question.Id)))
                        .ThenByDescending(test => test.Questions.Count())
                        .ThenBy(test => test.Test.Id)
                        .FirstOrDefault();
                    if (fullTest is null)
                        return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                            $"Chưa có đề {goalCode} với ít nhất 3 câu hợp lệ cho mỗi kỹ năng: từ vựng, ngữ pháp, đọc hiểu và nghe hiểu.",
                            HttpStatusCode.Conflict, errorCode: ErrorBankNotReady, retryable: false);

                    selectedQuestions = fullTest.Questions.ToList();
                    if (fullTest.Test.TimeLimitMinutes > 0)
                        fullTestTimeLimit = fullTest.Test.TimeLimitMinutes;

                }
                else
                {
                    foreach (var requiredLevel in RequiredLevels)
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

                    selectedQuestions = SupportedLevels
                        .Where(levelCode => groupedByLevel.TryGetValue(levelCode, out var candidates) && candidates.Count >= MinimumEligibleQuestionsPerLevel)
                        .SelectMany(levelCode => groupedByLevel[levelCode]
                            .Where(candidate => JlptSkillAssessmentPolicy.Classify(candidate.Section.LanguageSkill?.Name) is not null)
                            .GroupBy(candidate => JlptSkillAssessmentPolicy.Classify(candidate.Section.LanguageSkill?.Name))
                            .SelectMany(group => group.Select((candidate, index) => new { Candidate = candidate, Index = index }))
                            .OrderBy(x => x.Index).ThenBy(x => x.Candidate.Section.LanguageSkillId)
                            .Take(questionsPerSupportedLevel).Select(x => x.Candidate))
                        .ToList();
                }

                var timeLimitMinutes = fullTestTimeLimit ?? ComputeTimeLimitMinutes(selectedQuestions.Count);

                var startedAt = EnsureUtc(UtcNowProvider());
                var expiresAt = EnsureUtc(startedAt.AddMinutes(timeLimitMinutes));

                var snapshotDoc = new PlacementAttemptSnapshotDocument
                {
                    SchemaVersion = 2,
                    AssessmentTargetLevelId = adaptiveGoalId,
                    IsShortenedAssessment = shortQuestionLimit > 0,
                    TimeLimitMinutes = timeLimitMinutes,
                    StartedAt = startedAt,
                    ExpiresAt = expiresAt,
                    CatalogLevels = supportedCatalogLevels.Values
                        .Where(level => !adaptiveGoalId.HasValue || selectedQuestions.Any(question => question.LevelId == level.LevelId))
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
                        OrderingTokens = item.OrderingTokens,
                        OrderingAnswerKey = item.OrderingAnswerKey,
                        SelectedOptionId = null,
                        IsCorrect = false,
                        Options = item.ValidOptions.Select(opt => new PlacementSnapshotOptionItem
                        {
                            OptionId = opt.Id,
                            Content = opt.OptionText,
                            ImageUrl = opt.ImageUrl,
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

                        var activeAfterLock = FindCanonicalActiveAttempt(allPlacementAttempts, nowInTx, adaptiveGoalId);
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

                        if (latestTarget is not null &&
                            GetLatestTargetResult(allPlacementAttempts, adaptiveGoalId!.Value)?.AttemptId != latestTarget.AttemptId)
                        {
                            response = ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                                "Trạng thái bài đánh giá vừa thay đổi. Vui lòng bắt đầu lại.",
                                HttpStatusCode.Conflict, errorCode: "PLACEMENT_STAGE_CHANGED", retryable: true);
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
                                    staleDoc.IsReplaced = staleDoc.ParentAssessmentAttemptId.HasValue || staleDoc.IsComprehensiveAssessment;
                                    staleDoc.IsSupersededExpired = !staleDoc.IsReplaced;
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
                        await LockUserForPlacementStartAsync(userId, transactionCancellationToken);
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

                        var serverNow = EnsureUtc(UtcNowProvider());
                        // Starting another goal releases the single active slot. A full attempt
                        // closed this way can still be submitted before its original deadline.
                        var recoverEarlyClosure = CanSubmitEarlyClosedAttempt(attempt, serverNow);
                        if (attempt.IsDeleted && !recoverEarlyClosure)
                        {
                            if (TryDeserializeSnapshot(attempt.QuestionSnapshotJson, out var closedSnapshot) &&
                                closedSnapshot.IsReplaced)
                            {
                                response = ApiResponse<PlacementTestResultResponse>.Fail(
                                    "Lượt mini đã được thay bằng bài đánh giá mới. Hãy mở lượt đang làm.",
                                    HttpStatusCode.Conflict, errorCode: ErrorAttemptReplaced, retryable: false);
                                return;
                            }
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

                            await PopulatePlacementResultDetailsAsync(canonicalCompleted, userId, attempt.QuestionSnapshotJson, transactionCancellationToken);
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

                        var expiresAt = EnsureUtc(attempt.ExpiresAt ?? snapshotDoc.ExpiresAt);
                        var isExpired = serverNow >= expiresAt;

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
                            if (!IsValidPlacementSelection(frozenQuestion, submittedAnswer))
                            {
                                response = ApiResponse<PlacementTestResultResponse>.Fail(
                                    $"Lựa chọn không thuộc câu hỏi {submittedAnswer.QuestionId}.",
                                    HttpStatusCode.BadRequest,
                                    errorCode: ErrorAnswerInvalid,
                                    retryable: false);
                                return;
                            }
                        }

                        if (!isExpired)
                        {
                            foreach (var submittedAnswer in request.Answers)
                                ApplyPlacementSelection(snapshotQuestionsById[submittedAnswer.QuestionId], submittedAnswer);
                        }

                        foreach (var question in snapshotDoc.Questions)
                            question.IsCorrect = question.OrderingAnswerKey is { } answerKey
                                ? question.OrderingResponse is { } sequence && sequence.SequenceEqual(answerKey)
                                : question.CorrectOptionId.HasValue && question.SelectedOptionId == question.CorrectOptionId;

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
                                    SetPersistedPlacementSelection(answerRow, frozenQuestion);
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

                        var isTargetAssessment = snapshotDoc.AssessmentTargetLevelId.HasValue &&
                            !snapshotDoc.ParentAssessmentAttemptId.HasValue && !snapshotDoc.IsComprehensiveAssessment;
                        var targetLevel = evaluation.LevelBreakdowns
                            .FirstOrDefault(level => level.JlptLevelId == snapshotDoc.AssessmentTargetLevelId);
                        var canonicalResult = new PlacementTestResultResponse
                        {
                            AttemptId = attempt.Id,
                            AttemptType = AttemptTypePlacement,
                            Status = StatusCompleted,
                            StartedAt = EnsureUtc(attempt.StartedAt),
                            DurationSeconds = Math.Max(0, (int)(
                                (isExpired ? expiresAt : serverNow) - EnsureUtc(attempt.StartedAt)).TotalSeconds),
                            SubmittedAt = serverNow,
                            AssessmentTargetLevelId = snapshotDoc.AssessmentTargetLevelId,
                            IsComprehensiveAssessment = snapshotDoc.IsComprehensiveAssessment,
                            IsShortenedAssessment = snapshotDoc.IsShortenedAssessment,
                            ParentAssessmentAttemptId = snapshotDoc.ParentAssessmentAttemptId,
                            TotalScore = sectionResults.OverallScore,
                            CorrectCount = snapshotDoc.Questions.Count(q => q.IsCorrect),
                            NotStudiedCount = snapshotDoc.Questions.Count(q => q.SelectedOptionId == NotStudiedOptionId),
                            TotalQuestions = snapshotDoc.Questions.Count,
                            AssessedJlptLevelId = isTargetAssessment || snapshotDoc.IsComprehensiveAssessment ? 0 : evaluation.AssessedLevelId,
                            AssessedJlptLevelName = isTargetAssessment || snapshotDoc.IsComprehensiveAssessment ? string.Empty : evaluation.AssessedLevelName,
                            RecommendedTargetJlptLevelId = targetLevel?.JlptLevelId ?? evaluation.RecommendedTargetLevelId,
                            RecommendedTargetJlptLevelName = targetLevel?.JlptLevelName ?? evaluation.RecommendedTargetLevelName,
                            WrongQuestionIds = snapshotDoc.Questions
                                .Where(q => !q.IsCorrect)
                                .Select(q => q.QuestionId)
                                .OrderBy(id => id)
                                .ToList(),
                            LevelBreakdowns = evaluation.LevelBreakdowns,
                            Sections = sectionResults.Responses,
                        };

                        snapshotDoc.CompletedResult = canonicalResult;
                        if (recoverEarlyClosure)
                        {
                            attempt.IsDeleted = false;
                        }
                        attempt.SubmittedAt = serverNow;
                        attempt.IsCompleted = true;
                        attempt.TotalScore = sectionResults.OverallScore;
                        attempt.QuestionSnapshotJson = JsonSerializer.Serialize(snapshotDoc, SnapshotJsonOptions);
                        unitOfWork.MockTestAttempts.Update(attempt);

                        // Assessment comes only from this owned, immutable Placement result;
                        // it never changes the learner's chosen starting level, goal, or active path.
                        var learner = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(
                            userId, transactionCancellationToken);
                        if (learner is not null && !snapshotDoc.ParentAssessmentAttemptId.HasValue &&
                            (snapshotDoc.AssessmentTargetLevelId is null ||
                             snapshotDoc.AssessmentTargetLevelId == learner.TargetCertificateLevelId))
                        {
                            if (!isTargetAssessment && !snapshotDoc.IsComprehensiveAssessment)
                                learner.AssessedCertificateLevelId = evaluation.AssessedLevelId;
                            learner.PlacementDecision = PlacementDecision.Completed;
                            unitOfWork.UserAccounts.Update(learner);
                        }

                        await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                        await PopulatePlacementResultDetailsAsync(canonicalResult, userId, attempt.QuestionSnapshotJson, transactionCancellationToken);
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
                            await PopulatePlacementResultDetailsAsync(canonicalCompleted, userId, latestAttempt.QuestionSnapshotJson, cancellationToken);
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
                    if (attempt.ExpiresAt is { } expiresAt && EnsureUtc(expiresAt) <= EnsureUtc(UtcNowProvider()))
                        return ApiResponse<PlacementTestResultResponse>.Fail(
                            "Lượt làm bài đã hết thời gian và cần được nộp.",
                            HttpStatusCode.Conflict, errorCode: ErrorAttemptExpired, retryable: false);

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

                await PopulatePlacementResultDetailsAsync(canonicalResult, userId, attempt.QuestionSnapshotJson, cancellationToken);
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
            var goal = unitOfWork.UserAccounts is null ? null :
                await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
            return FindCanonicalActiveAttempt(attempts, serverNowUtc, GetAdaptiveGoalId(goal));
        }

        private static int? GetAdaptiveGoalId(UserAccount? user)
            => NormalizeLevelCode(user?.TargetCertificateLevel?.Code) is "N2" or "N3" or "N4" or "N5"
                ? user?.TargetCertificateLevelId
                : null;

        private static (MockTestAttempt Attempt, PlacementAttemptSnapshotDocument Snapshot)?
            FindCanonicalActiveAttempt(
                IEnumerable<MockTestAttempt> placementAttempts,
                DateTime serverNowUtc,
                int? adaptiveGoalId)
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
                    snapshotDoc.AssessmentTargetLevelId == adaptiveGoalId &&
                    !snapshotDoc.IsComprehensiveAssessment &&
                    !snapshotDoc.ParentAssessmentAttemptId.HasValue &&
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

        private static bool CanSubmitEarlyClosedAttempt(MockTestAttempt attempt, DateTime now)
            => IsSupersededExpiredAttempt(attempt) &&
               attempt.ExpiresAt is { } expiry && EnsureUtc(expiry) > now &&
               TryDeserializeSnapshot(attempt.QuestionSnapshotJson, out var snapshot) &&
               !snapshot.ParentAssessmentAttemptId.HasValue;

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
                if (cached.DurationSeconds <= 0)
                    cached.DurationSeconds = Math.Max(0, (int)(cached.SubmittedAt.Value - cached.StartedAt).TotalSeconds);
            }

            return cached;
        }

        internal static (MockTestAttempt? Attempt, PlacementTestResultResponse? Recovered) FindLatestCompletedAssessment(
            IEnumerable<MockTestAttempt> attempts, int? targetId, bool requireMatchingGoal = false) =>
            attempts.Where(attempt => attempt.IsCompleted && attempt.TotalScore.HasValue)
                .Select(attempt => (Attempt: (MockTestAttempt?)attempt, Recovered: RecoverResultFromCompletedAttempt(attempt)))
                .Where(item => item.Recovered is { IsMiniAssessment: false } result &&
                    (result.AssessmentTargetLevelId == targetId ||
                     (!requireMatchingGoal && result.AssessmentTargetLevelId is null)))
                .OrderByDescending(item => item.Recovered!.ScreeningLevelName is not null)
                .ThenByDescending(item => item.Attempt!.SubmittedAt ?? item.Attempt.StartedAt)
                .ThenByDescending(item => item.Attempt!.Id)
                .FirstOrDefault();

        internal static PlacementTestResultResponse? TryGetCompletedResultFromSnapshot(string? snapshotJson)
        {
            if (!TryDeserializeSnapshot(snapshotJson, out var snapshotDoc) ||
                snapshotDoc.CompletedResult is not { } completedResult ||
                !IsValidCompletedResultSnapshot(completedResult))
            {
                return null;
            }

            // Older low results stored no recommendation while waiting for a mini assessment.
            if (completedResult.ScreeningLevelName is { } targetLevel)
            {
                completedResult.RecommendedTargetJlptLevelId = completedResult.AssessmentTargetLevelId!.Value;
                completedResult.RecommendedTargetJlptLevelName = targetLevel;
            }
            return completedResult;
        }

        internal static List<SectionResultItem>? TryGetSkillEvidenceFromSnapshot(string? snapshotJson)
        {
            if (!TryDeserializeSnapshot(snapshotJson, out var snapshot) ||
                snapshot.CompletedResult is not { } completed || !IsValidCompletedResultSnapshot(completed) ||
                snapshot.Questions.Count == 0) return null;
            return BuildSectionResultsFromSnapshot(0, snapshot.Questions.Where(q =>
                JlptSkillAssessmentPolicy.Classify(q.LanguageSkillName) != JlptStudySkill.Listening || !string.IsNullOrWhiteSpace(q.AudioUrl)).ToList()).Responses;
        }

        private static bool IsValidCompletedResultSnapshot(PlacementTestResultResponse result)
            => result.TotalQuestions > 0 &&
               result.CorrectCount >= 0 &&
               result.CorrectCount <= result.TotalQuestions &&
               result.TotalScore >= 0 &&
               result.TotalScore <= 100 &&
               ((result.AssessmentTargetLevelId.HasValue && !result.IsMiniAssessment) ||
                (result.AssessedJlptLevelId > 0 && IsSupportedLevel(result.AssessedJlptLevelName))) &&
               (result.ScreeningLevelName is not null ||
                (result.RecommendedTargetJlptLevelId > 0 && IsSupportedLevel(result.RecommendedTargetJlptLevelName))) &&
               result.LevelBreakdowns is { Count: > 0 } &&
               result.Sections is { Count: > 0 };

        private static bool HasValidSnapshotQuestions(List<PlacementSnapshotQuestionItem>? questions)
            => questions is { Count: > 0 } &&
               questions.All(q =>
                   q is not null &&
                   q.QuestionId > 0 &&
                   q.JlptLevelId > 0 &&
                   IsSupportedLevel(q.JlptLevelName));

        private static bool IsValidPlacementSelection(PlacementSnapshotQuestionItem question, AnswerSubmissionItem answer)
        {
            if (answer.SelectedOptionId == NotStudiedOptionId) return answer.Response is null;
            if (question.OrderingTokens is not { } tokens)
                return answer.Response is null && (answer.SelectedOptionId is null ||
                    question.Options.Any(option => option.OptionId == answer.SelectedOptionId.Value));
            return answer.SelectedOptionId is null && (answer.Response is null ||
                (SentenceOrdering.TryReadSequence(answer.Response, out var sequence) &&
                    SentenceOrdering.IsSubsequenceOfChoices(tokens, sequence)));
        }

        private static void ApplyPlacementSelection(PlacementSnapshotQuestionItem question, AnswerSubmissionItem answer)
        {
            question.SelectedOptionId = answer.SelectedOptionId;
            question.OrderingResponse = answer.Response is not null &&
                SentenceOrdering.TryReadSequence(answer.Response, out var sequence) && sequence.Length > 0
                ? sequence : null;
        }

        private static void SetPersistedPlacementSelection(MockTestAnswer row, PlacementSnapshotQuestionItem question)
        {
            var notStudied = question.SelectedOptionId == NotStudiedOptionId;
            row.SelectedOptionId = notStudied ? null : question.SelectedOptionId;
            row.ResponseJson = notStudied ? NotStudiedResponseJson :
                question.OrderingResponse is { } sequence ? JsonSerializer.Serialize(sequence) : null;
        }

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
                ParentAssessmentAttemptId = snapshotDoc.ParentAssessmentAttemptId,
                IsComprehensiveAssessment = snapshotDoc.IsComprehensiveAssessment,
                IsShortenedAssessment = snapshotDoc.IsShortenedAssessment,
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
                        SelectedOptionId = q.SelectedOptionId,
                        QuestionType = q.OrderingTokens is null ? "SingleChoice" : SentenceOrdering.QuestionType,
                        InteractionData = q.OrderingTokens is { } tokens
                            ? JsonSerializer.SerializeToElement(new { tokens }) : null,
                        Response = q.OrderingResponse is { } sequence ? JsonSerializer.SerializeToElement(sequence) : null,
                        Options = q.Options
                            .OrderBy(o => o.Order)
                            .ThenBy(o => o.OptionId)
                            .Select(o => new PlacementTestOptionResponse
                            {
                                OptionId = o.OptionId,
                                Content = o.Content,
                                ImageUrl = o.ImageUrl,
                                Order = o.Order,
                            })
                            .Append(new PlacementTestOptionResponse
                            {
                                OptionId = NotStudiedOptionId,
                                Content = "Tôi chưa học, tôi không biết",
                                Order = q.Options.Select(o => o.Order).DefaultIfEmpty().Max() + 1,
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
                .Where(x => x.Rank >= 1 && x.Rank <= 4)
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
                    q.LanguageSkillName,
                    q.JlptLevelId,
                    q.JlptLevelName))
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
                        JlptLevelId: g.Key.JlptLevelId,
                        JlptLevelName: g.Key.JlptLevelName,
                        HasAudioEvidence: g.All(q => !string.IsNullOrWhiteSpace(q.AudioUrl)),
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
                JlptLevelId = g.JlptLevelId,
                JlptLevelName = g.JlptLevelName,
                HasAudioEvidence = g.HasAudioEvidence,
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
            return normalized is "N5" or "N4" or "N3" or "N2" or "N1";
        }

        private async Task PopulatePlacementResultDetailsAsync(
            PlacementTestResultResponse result, int userId, string? snapshotJson, CancellationToken cancellationToken)
        {
            var goal = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
            var targetCode = goal?.TargetCertificateLevel?.Code;
            if (string.IsNullOrWhiteSpace(targetCode) && goal?.TargetCertificateLevelId is int goalLevelId)
                targetCode = (await unitOfWork.CertificateLevels.GetByIdAsync(goalLevelId, cancellationToken))?.Code;
            result.SkillAssessments = JlptSkillAssessmentPolicy.Assess(result,
                NormalizeLevelCode(result.ScreeningLevelName ?? targetCode),
                TryGetSkillEvidenceFromSnapshot(snapshotJson));

            if (result.IsMiniAssessment || result.AssessmentTargetLevelId is not int targetId)
                return;

            if (goal?.TargetCertificateLevelId != targetId)
            {
                result.FollowUpUnavailableReason = "CURRENT_TARGET_MISMATCH";
                return;
            }

            if (result.ScreeningLevelName is null || result.TotalScore >= 50)
                return;

            var attempts = await LoadUserPlacementAttemptsAsync(userId, cancellationToken);
            if (GetLatestTargetResult(attempts, targetId)?.AttemptId != result.AttemptId)
            {
                return;
            }

            if (FindCanonicalActiveAttempt(attempts, EnsureUtc(UtcNowProvider()), targetId) is not null)
            {
                result.FollowUpUnavailableReason = "TARGET_ASSESSMENT_IN_PROGRESS";
                return;
            }

            result.CanRetake = true;
        }

        private static PlacementTestResultResponse? GetLatestTargetResult(IEnumerable<MockTestAttempt> attempts, int targetId) =>
            attempts.Where(attempt => attempt.IsCompleted)
                .OrderByDescending(attempt => attempt.SubmittedAt ?? attempt.StartedAt)
                .ThenByDescending(attempt => attempt.Id)
                .Select(RecoverResultFromCompletedAttempt)
                .FirstOrDefault(result => result?.ScreeningLevelName is not null && result.AssessmentTargetLevelId == targetId);

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

        public Task<ApiResponse<PlacementAnswerAckResponse>> SavePlacementAnswerAsync(int attemptId, SavePlacementAnswerRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ApiResponse<StartAttemptResponse>> StartAttemptAsync(int practiceTestId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ApiResponse<AttemptResultResponse>> SubmitAttemptAsync(int attemptId, SubmitAttemptRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ApiResponse<AttemptResultResponse>> GetAttemptResultAsync(int attemptId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ApiResponse<List<TestSummaryResponse>>> GetTestsByLevelAsync(int jlptLevelId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ApiResponse<MockTestDetailResponse>> GetPlacementTestDetailAsync(int certificateLevelId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ApiResponse<DetailedAttemptResultResponse>> GetDetailedResultAsync(int attemptId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ApiResponse<List<QuestionReviewCard>>> GetWrongQuestionsReviewSessionAsync(int? practiceTestId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        internal sealed class PlacementAttemptSnapshotDocument
        {
            public int SchemaVersion { get; set; } = 2;
            public bool IsComprehensiveAssessment { get; set; }
            public bool IsShortenedAssessment { get; set; }
            public int? AssessmentTargetLevelId { get; set; }
            public int? ParentAssessmentAttemptId { get; set; }
            public int TimeLimitMinutes { get; set; }
            public DateTime StartedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public bool IsSupersededExpired { get; set; }
            public bool IsReplaced { get; set; }
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
            public int? CorrectOptionId { get; set; }
            public string[]? OrderingTokens { get; set; }
            public string[]? OrderingAnswerKey { get; set; }
            public string[]? OrderingResponse { get; set; }
            public int? SelectedOptionId { get; set; }
            public bool IsCorrect { get; set; }
            public List<PlacementSnapshotOptionItem> Options { get; set; } = [];
        }

        internal sealed class PlacementSnapshotOptionItem
        {
            public string? ImageUrl { get; set; }
            public int OptionId { get; set; }
            public string Content { get; set; } = string.Empty;
            public int Order { get; set; }
            public bool IsCorrect { get; set; }
        }
    }
}
