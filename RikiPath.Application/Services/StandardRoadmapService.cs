using System.Data.Common;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.Helpers;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;
using RikiPath.Application.Responses.Profile;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

/// <summary>Deterministic, skill-adaptive curriculum roadmap built over canonical lesson progress and multi-source evidence.</summary>
public sealed class StandardRoadmapService(
    IUnitOfWork unitOfWork,
    IClaimService claimService,
    ILogger<StandardRoadmapService>? logger = null,
    AdaptiveSkillProfileService? skillProfileService = null,
    ILearnerSetupStateService? setupStateService = null)
    : IStandardRoadmapService
{
    private async Task<ApiResponse<StandardRoadmapResponse>?> ValidateStandardEligibilityAsync(
        int userId, CancellationToken cancellationToken)
    {
        var resolver = setupStateService ?? new LearnerSetupStateService(unitOfWork, claimService);
        var state = await resolver.ResolveForUserAsync(userId, includeProgress: false, cancellationToken);
        if (state is null)
            return ApiResponse<StandardRoadmapResponse>.NotFound("Không tìm thấy người dùng.");

        if (!state.HasConfirmedGoal)
            return ApiResponse<StandardRoadmapResponse>.Fail(
                "Vui lòng chọn chứng chỉ và mục tiêu JLPT (N5–N2) trước khi xem lộ trình học.",
                HttpStatusCode.BadRequest, errorCode: "STUDY_LEVEL_REQUIRED");

        if (!state.StandardRoadmapEligible)
        {
            if (state.HasActivePlacementAttempt)
                return ApiResponse<StandardRoadmapResponse>.Fail(
                    "Bạn đang có bài đánh giá năng lực chưa hoàn tất. Vui lòng tiếp tục làm bài hoặc chọn bỏ qua đánh giá năng lực.",
                    HttpStatusCode.Conflict, errorCode: "PLACEMENT_IN_PROGRESS");

            if (state.HasPendingPlacementSubmission)
                return ApiResponse<StandardRoadmapResponse>.Fail(
                    "Bạn có lượt đánh giá năng lực đã hết giờ cần nộp trước khi mở lộ trình.",
                    HttpStatusCode.Conflict, errorCode: "PLACEMENT_PENDING_SUBMISSION");

            if (state.PlacementCompatibilityStatus == "INSUFFICIENT_FOR_TARGET")
                return ApiResponse<StandardRoadmapResponse>.Fail(
                    state.PlacementCompatibilityReason ?? "Kết quả đánh giá hiện tại chưa bao phủ đủ cấp độ mục tiêu. Hãy đánh giá bổ sung hoặc chủ động chọn bỏ qua để mở lộ trình tiêu chuẩn.",
                    HttpStatusCode.Conflict, errorCode: "PLACEMENT_INSUFFICIENT_FOR_TARGET");

            return ApiResponse<StandardRoadmapResponse>.Fail(
                "Vui lòng hoàn thành bài đánh giá năng lực hoặc chủ động chọn bỏ qua để sử dụng lộ trình tiêu chuẩn.",
                HttpStatusCode.Conflict, errorCode: "STANDARD_ROADMAP_PLACEMENT_REQUIRED");
        }

        return null;
    }

    public async Task<ApiResponse<StandardRoadmapResponse>> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var gateFailure = await ValidateStandardEligibilityAsync(userId, cancellationToken);
            if (gateFailure is not null)
                return gateFailure;

            var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
            if (user is null)
                return ApiResponse<StandardRoadmapResponse>.NotFound("Không tìm thấy người dùng.");

            var levelId = user.StartingCertificateLevelId ?? user.TargetCertificateLevelId;
            if (!levelId.HasValue)
                return ApiResponse<StandardRoadmapResponse>.Fail(
                    "Hãy chọn cấp độ đang học trước khi xem lộ trình tiêu chuẩn.",
                    HttpStatusCode.BadRequest, errorCode: "STUDY_LEVEL_REQUIRED");

            var level = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(
                levelId.Value, cancellationToken);
            if (level is null || !JlptCurriculum.IsSupportedStudyLevel(level.Code))
                return ApiResponse<StandardRoadmapResponse>.Fail(
                    "Cấp độ học không khả dụng; lộ trình hiện hỗ trợ N5–N2.",
                    HttpStatusCode.BadRequest, errorCode: "STUDY_LEVEL_UNSUPPORTED");

            var lessons = await unitOfWork.Lessons.GetByCertificateLevelAsync(level.Id, cancellationToken);
            var progress = await unitOfWork.LessonProgresses.GetByUserAsync(userId, cancellationToken);
            var progressByLesson = progress
                .GroupBy(item => item.LessonId)
                .ToDictionary(group => group.Key,
                    group => group.OrderByDescending(item => item.IsCompleted)
                        .ThenByDescending(item => item.ProgressPercent).First());
            var rows = lessons.OrderBy(item => item.SortOrder).ThenBy(item => item.Id)
                .Select(lesson =>
                {
                    progressByLesson.TryGetValue(lesson.Id, out var item);
                    var completed = item?.IsCompleted == true || item?.ProgressPercent >= 100;
                    return new StandardRoadmapLessonResponse
                    {
                        LessonId = lesson.Id,
                        Title = lesson.Title,
                        Description = lesson.Description,
                        SortOrder = lesson.SortOrder,
                        EstimatedMinutes = lesson.EstimatedMinutes > 0 ? lesson.EstimatedMinutes : 15,
                        TopicId = lesson.TopicId,
                        TopicTitle = lesson.Topic?.Title,
                        PrerequisiteLessonId = lesson.PrerequisiteLessonId,
                        LearningObjectives = lesson.LearningObjectives,
                        ProgressPercent = completed ? 100 : Math.Clamp(item?.ProgressPercent ?? 0, 0, 99),
                        IsCompleted = completed,
                        IsStudied = completed,
                        CompletionState = completed ? "Completed" : item is not null ? "Viewed" : "NotStarted",
                    };
                }).ToList();

            return ApiResponse<StandardRoadmapResponse>.Success(new StandardRoadmapResponse
            {
                CurrentStudyLevelId = level.Id,
                CurrentStudyLevelName = level.Code,
                TargetJlptLevelId = user.TargetCertificateLevelId,
                TargetJlptLevelName = user.TargetCertificateLevel?.Code,
                LearningMode = user.LearningMode == LearningMode.AiPersonalized ? "AI_PERSONALIZED" : "STANDARD",
                TotalLessons = rows.Count,
                CompletedLessons = rows.Count(row => row.IsCompleted),
                NextLessonId = rows.FirstOrDefault(row => !row.IsCompleted)?.LessonId,
                Lessons = rows,
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return ApiResponse<StandardRoadmapResponse>.Fail(
                "Không thể tải lộ trình tiêu chuẩn.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<StandardRoadmapResponse>> GetAssessmentRoadmapAsync(CancellationToken cancellationToken)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var gateFailure = await ValidateStandardEligibilityAsync(userId, cancellationToken);
            if (gateFailure is not null)
                return gateFailure;

            var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
            if (user is null)
                return ApiResponse<StandardRoadmapResponse>.NotFound("Không tìm thấy người dùng.");

            var targetId = user.TargetCertificateLevelId ?? user.StartingCertificateLevelId;
            var target = targetId is int resolvedTargetId
                ? await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(resolvedTargetId, cancellationToken)
                : null;
            if (target is null || !JlptCurriculum.IsSupportedStudyLevel(target.Code))
                return ApiResponse<StandardRoadmapResponse>.Fail("Hãy chọn mục tiêu JLPT từ N5 đến N2.",
                    HttpStatusCode.BadRequest, "STUDY_LEVEL_REQUIRED");

            var targetCode = target.Code.Trim().ToUpperInvariant();
            var comparisonCode = PlacementAssessmentModePolicy.ComparisonLevel(targetCode);
            var profileSvc = skillProfileService ?? new AdaptiveSkillProfileService(unitOfWork);
            var profileResult = await profileSvc.BuildProfileAsync(
                userId, target.Id, targetCode, user.LastStudyDate, cancellationToken);

            var hasPlacementEvidence = profileResult.LatestPlacementAttemptId.HasValue ||
                profileResult.PlacementAssessments.Any(a => a.HasSufficientEvidence);
            var placementDecisionCode = user.PlacementDecision switch
            {
                PlacementDecision.Skipped => "SKIPPED",
                PlacementDecision.Completed => "COMPLETED",
                _ => hasPlacementEvidence ? "COMPLETED" : "NOT_TAKEN",
            };

            var skillByCode = profileResult.Skills.ToDictionary(s => s.SkillCode, StringComparer.OrdinalIgnoreCase);

            // Resolve optional foundation bridge level (strictly ascending order: Bridge first, Target second)
            var allLevels = await unitOfWork.CertificateLevels.GetLearnerVisibleJlptLevelsAsync(cancellationToken);
            CertificateLevel? bridgeLevel = null;
            if (comparisonCode is not null)
            {
                var explicitStart = user.StartingCertificateLevel is { IsDeleted: false } s &&
                    s.CertificateTypeId == target.CertificateTypeId &&
                    JlptCurriculum.Rank(s.Code) > 0 &&
                    JlptCurriculum.Rank(s.Code) < JlptCurriculum.Rank(targetCode)
                    ? s
                    : null;

                var weakPlacementFoundation = profileResult.PlacementAssessments.Any(a =>
                    a.HasSufficientEvidence &&
                    !string.Equals(a.EstimatedLevelCode, targetCode, StringComparison.OrdinalIgnoreCase));

                var weakAssessedSkills = profileResult.Skills.Any(s =>
                    s.ProficiencyStatus == "NEEDS_REVIEW");

                var declaredRank = JlptCurriculum.Rank(user.DeclaredJlptCertificateCode);
                var lacksFoundationProof = !hasPlacementEvidence &&
                    JlptCurriculum.Rank(targetCode) >= 3 &&
                    declaredRank < JlptCurriculum.Rank(comparisonCode);

                if (explicitStart is not null)
                {
                    bridgeLevel = explicitStart;
                }
                else if (weakPlacementFoundation || weakAssessedSkills || lacksFoundationProof)
                {
                    bridgeLevel = allLevels.FirstOrDefault(l =>
                        l.CertificateTypeId == target.CertificateTypeId &&
                        l.Code.Equals(comparisonCode, StringComparison.OrdinalIgnoreCase));
                }
            }

            var studyLevels = bridgeLevel is not null && bridgeLevel.Id != target.Id
                ? new[] { bridgeLevel, target }
                : new[] { target };

            // Collect published catalog lessons across selected levels
            var selected = new List<(AssessmentRoadmapLesson Lesson, string SkillCode, string LevelCode, bool IsBridge)>();
            var skillAssessmentsByCode = profileResult.PlacementAssessments.ToDictionary(
                a => a.SkillCode,
                a => new StandardRoadmapSkillResponse { Assessment = a },
                StringComparer.OrdinalIgnoreCase);

            foreach (var level in studyLevels)
            {
                var levelCode = level.Code.Trim().ToUpperInvariant();
                var isBridge = bridgeLevel is not null && level.Id == bridgeLevel.Id && level.Id != target.Id;
                var lessons = await unitOfWork.Lessons.GetAssessmentRoadmapLessonsAsync(level.Id, userId, cancellationToken);
                var levelSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var lesson in lessons)
                {
                    var skillCode = JlptCurriculum.ResolveSkillCode(
                        lesson.LanguageSkillId, lesson.LanguageSkillCode, lesson.LanguageSkillName);

                    // Skip bridge lessons whose skill is already proven STRONG unless Learner already started/studied them
                    var alreadyTouched = lesson.Progress is { } p && (p.IsCompleted || p.ProgressPercent > 0 || p.FirstViewedAt.HasValue);
                    if (isBridge && !alreadyTouched &&
                        skillByCode.TryGetValue(skillCode, out var prof) &&
                        prof.ProficiencyStatus == "STRONG")
                    {
                        continue;
                    }

                    if (!skillAssessmentsByCode.ContainsKey(skillCode))
                    {
                        skillAssessmentsByCode.Add(skillCode, new StandardRoadmapSkillResponse
                        {
                            Assessment = new LearningPathSectionAssessmentResponse
                            {
                                SkillCode = skillCode,
                                Skill = JlptCurriculum.SkillLabel(skillCode),
                                SectionTitle = JlptCurriculum.SkillLabel(skillCode),
                                TargetLevelCode = targetCode,
                                HasData = skillByCode.TryGetValue(skillCode, out var sp) && sp.ProficiencyStatus != "NOT_YET_ASSESSED",
                                HasSufficientEvidence = skillByCode.TryGetValue(skillCode, out var sp2) && sp2.ProficiencyStatus != "NOT_YET_ASSESSED",
                                ScorePercent = sp?.ProficiencyPercent,
                                Priority = sp?.Priority ?? "needs_assessment",
                                Comment = sp?.Summary ?? string.Empty,
                            },
                        });
                    }

                    levelSkills.Add(skillCode);
                    selected.Add((lesson, skillCode, levelCode, isBridge));
                }

                foreach (var assessment in profileResult.PlacementAssessments)
                {
                    if (!levelSkills.Contains(assessment.SkillCode))
                        skillAssessmentsByCode[assessment.SkillCode].UnavailableLevelCodes.Add(levelCode);
                }
            }

            var lessonIds = selected.Select(item => item.Lesson.Id).ToArray();
            var topicIds = selected.Where(item => item.Lesson.TopicId.HasValue).Select(item => item.Lesson.TopicId!.Value).Distinct().ToArray();
            var levelIds = studyLevels.Select(l => l.Id).Distinct().ToArray();

            // Load Quick Checks + Group Quizzes + Stage Assessments in single-pass clean queries
            var quickCheckRows = lessonIds.Length == 0
                ? []
                : await LessonPracticeProgress.ReadyExercises(unitOfWork)
                    .Where(e => e.LessonId.HasValue && e.Scope == PracticeExerciseScopes.LessonQuickCheck && lessonIds.Contains(e.LessonId.Value))
                    .OrderBy(e => e.SortOrder).ThenBy(e => e.Id)
                    .Select(e => new
                    {
                        e.Id,
                        LessonId = e.LessonId!.Value,
                        e.PassingScorePercent,
                        QuestionCount = e.Questions!.Count(q => !q.IsDeleted),
                    })
                    .ToListAsync(cancellationToken);

            var quickCheckByLesson = quickCheckRows
                .GroupBy(x => x.LessonId)
                .ToDictionary(g => g.Key, g => g.First());

            var checkpointRows = (topicIds.Length == 0 && levelIds.Length == 0)
                ? []
                : await LessonPracticeProgress.ReadyExercises(unitOfWork)
                    .Where(e => (e.Scope == PracticeExerciseScopes.GroupQuiz && e.TopicId.HasValue && topicIds.Contains(e.TopicId.Value))
                        || (e.Scope == PracticeExerciseScopes.StageAssessment && e.CertificateLevelId.HasValue && levelIds.Contains(e.CertificateLevelId.Value)))
                    .OrderBy(e => e.SortOrder).ThenBy(e => e.Id)
                    .Select(e => new
                    {
                        e.Id,
                        e.Title,
                        e.Description,
                        e.Scope,
                        e.TopicId,
                        e.CertificateLevelId,
                        e.LanguageSkillId,
                        SkillCode = e.LanguageSkill.Code,
                        SkillName = e.LanguageSkill.Name,
                        e.PassingScorePercent,
                        QuestionCount = e.Questions!.Count(q => !q.IsDeleted),
                    })
                    .ToListAsync(cancellationToken);

            var allExerciseIds = quickCheckRows.Select(q => q.Id)
                .Concat(checkpointRows.Select(c => c.Id))
                .Distinct()
                .ToArray();

            var attemptStats = allExerciseIds.Length == 0
                ? []
                : await unitOfWork.PracticeExerciseAttempts.Query()
                    .AsNoTracking()
                    .Where(a => !a.IsDeleted && a.UserId == userId && a.HomeworkAssignmentId == null
                        && a.IsCompleted && allExerciseIds.Contains(a.PracticeExerciseId))
                    .Select(a => new { a.PracticeExerciseId, a.ScorePercent, a.SubmittedAt })
                    .ToListAsync(cancellationToken);

            var statsByExercise = attemptStats
                .GroupBy(a => a.PracticeExerciseId)
                .ToDictionary(g => g.Key, g => new
                {
                    Count = g.Count(),
                    BestScore = g.Where(x => x.ScorePercent.HasValue).Select(x => x.ScorePercent!.Value).DefaultIfEmpty().Max(),
                    HasAnyScore = g.Any(x => x.ScorePercent.HasValue),
                    LatestScore = g.OrderByDescending(x => x.SubmittedAt).FirstOrDefault()?.ScorePercent,
                });

            // Pass 1: Build Lesson rows & track cleared lessons cleanly without linear deadlock
            var lessonDtos = new List<(StandardRoadmapLessonResponse Dto, AssessmentRoadmapLesson Raw, bool IsBridge)>(selected.Count);
            var clearedLessonIds = new HashSet<int>();
            var visibleLessonIds = lessonIds.ToHashSet();

            foreach (var (lesson, skillCode, levelCode, isBridge) in selected)
            {
                var progress = lesson.Progress;
                var hasQc = quickCheckByLesson.TryGetValue(lesson.Id, out var qc);
                var qcPassing = hasQc && qc is not null
                    ? (qc.PassingScorePercent > 0 ? qc.PassingScorePercent : LessonPracticeProgress.PassingScore)
                    : (double?)null;
                var stat = hasQc && qc is not null && statsByExercise.TryGetValue(qc.Id, out var s) ? s : null;
                var qcPassed = hasQc && qcPassing.HasValue && stat is { HasAnyScore: true } && stat.BestScore >= qcPassing.Value;
                var studied = (progress?.IsCompleted == true || progress?.ProgressPercent >= 100) || qcPassed;

                // Lesson clears when its QuickCheck passes, OR (no QuickCheck exists and lesson is studied)
                var cleared = hasQc ? qcPassed : studied;
                if (cleared) clearedLessonIds.Add(lesson.Id);

                var viewed = progress is not null && (progress.ProgressPercent > 0 || progress.FirstViewedAt.HasValue);
                var state = cleared
                    ? "Completed"
                    : profileResult.FailedLessonIds.Contains(lesson.Id) || (stat is { Count: > 0 } && !qcPassed)
                        ? "NeedsReview"
                        : stat is { Count: > 0 }
                            ? "PracticeAttempted"
                            : studied
                                ? "Studied"
                                : viewed
                                    ? "Viewed"
                                    : "NotStarted";

                var dto = new StandardRoadmapLessonResponse
                {
                    LessonId = lesson.Id,
                    Title = lesson.Title,
                    Description = lesson.Description,
                    SortOrder = lesson.SortOrder > 0 ? lesson.SortOrder : lessonDtos.Count + 1,
                    TopicId = lesson.TopicId,
                    TopicTitle = lesson.TopicTitle,
                    EstimatedMinutes = lesson.EstimatedMinutes > 0 ? lesson.EstimatedMinutes : 15,
                    PrerequisiteLessonId = lesson.PrerequisiteLessonId,
                    LearningObjectives = lesson.LearningObjectives,
                    SkillCode = skillCode,
                    LanguageSkillName = lesson.LanguageSkillName,
                    LevelCode = levelCode,
                    KanjiCount = lesson.KanjiCount,
                    VocabularyCount = lesson.VocabularyCount,
                    GrammarCount = lesson.GrammarCount,
                    HasPracticeExercise = hasQc,
                    PracticePassed = qcPassed,
                    IsStudied = studied,
                    IsCompleted = cleared,
                    CompletionState = state,
                    BestPracticeScore = stat is { HasAnyScore: true } ? Math.Round(stat.BestScore, 1) : null,
                    PracticeAttemptCount = stat?.Count ?? 0,
                    QuickCheckExerciseId = hasQc && qc is not null ? qc.Id : null,
                    QuickCheckQuestionCount = hasQc && qc is not null ? qc.QuestionCount : 0,
                    QuickCheckPassingScorePercent = qcPassing,
                    ProgressPercent = cleared ? 100 : Math.Clamp(progress?.ProgressPercent ?? (studied ? 85 : 0), 0, 99),
                };

                lessonDtos.Add((dto, lesson, isBridge));
            }

            // Pass 2: Evaluate explicit prerequisite locking (never deadlock entire roadmap on missing exercises or failed quizzes)
            foreach (var (dto, raw, _) in lessonDtos)
            {
                dto.IsLocked = raw.PrerequisiteLessonId is int prereqId
                    && visibleLessonIds.Contains(prereqId)
                    && !clearedLessonIds.Contains(prereqId);
            }

            // Pass 3: Assemble Stages grouped cleanly by (Level, Topic)
            var stages = new List<StandardRoadmapStageResponse>();
            var syntheticStageId = -1;
            var levelMap = studyLevels.ToDictionary(l => l.Code.Trim().ToUpperInvariant(), l => l.Id);

            var grouped = lessonDtos
                .GroupBy(x => new { x.Dto.LevelCode, x.Raw.TopicId, x.IsBridge })
                .OrderBy(g => g.Key.IsBridge ? 0 : 1)
                .ThenBy(g => JlptCurriculum.Rank(g.Key.LevelCode))
                .ThenBy(g => g.First().Raw.TopicSortOrder ?? int.MaxValue)
                .ThenBy(g => g.Key.TopicId ?? int.MaxValue)
                .ToList();

            var stageOrder = 1;
            foreach (var group in grouped)
            {
                var firstRaw = group.First().Raw;
                var stageLessons = group
                    .Select(x => x.Dto)
                    .OrderBy(l => l.SortOrder)
                    .ThenBy(l => l.LessonId)
                    .ToList();

                var allCleared = stageLessons.Count > 0 && stageLessons.All(l => l.IsCompleted);
                var allStudied = stageLessons.Count > 0 && stageLessons.All(l => l.IsStudied);

                RoadmapCheckpointResponse? groupQuiz = null;
                if (group.Key.TopicId is int tid)
                {
                    var gq = checkpointRows.FirstOrDefault(c => c.Scope == PracticeExerciseScopes.GroupQuiz && c.TopicId == tid);
                    if (gq is not null)
                    {
                        statsByExercise.TryGetValue(gq.Id, out var gStat);
                        var gqPassing = gq.PassingScorePercent > 0 ? gq.PassingScorePercent : LessonPracticeProgress.PassingScore;
                        var isPassed = gStat is { HasAnyScore: true } && gStat.BestScore >= gqPassing;
                        var unlocked = allCleared || allStudied;
                        groupQuiz = new RoadmapCheckpointResponse
                        {
                            ExerciseId = gq.Id,
                            Title = gq.Title,
                            Description = gq.Description,
                            Scope = gq.Scope,
                            SkillCode = JlptCurriculum.ResolveSkillCode(gq.LanguageSkillId, gq.SkillCode, gq.SkillName),
                            QuestionCount = gq.QuestionCount,
                            PassingScorePercent = gqPassing,
                            IsUnlocked = unlocked,
                            IsPassed = isPassed,
                            BestScorePercent = gStat is { HasAnyScore: true } ? Math.Round(gStat.BestScore, 1) : null,
                            AttemptCount = gStat?.Count ?? 0,
                            Status = !unlocked ? "Locked" : isPassed ? "Passed" : gStat is { Count: > 0 } ? "NeedsReview" : "Available",
                            LockReason = unlocked ? null : "Hoàn thành các bài học trong chặng để mở Quiz củng cố.",
                        };
                    }
                }

                var stageHasFailure = stageLessons.Any(l => l.CompletionState == "NeedsReview") || groupQuiz?.Status == "NeedsReview";
                var stageHasProgress = stageLessons.Any(l => l.CompletionState != "NotStarted") || groupQuiz is { AttemptCount: > 0 };
                var stageStatus = allCleared && (groupQuiz is null || groupQuiz.IsPassed)
                    ? "Completed"
                    : stageHasFailure
                        ? "NeedsReview"
                        : stageHasProgress
                            ? "InProgress"
                            : "NotStarted";

                var defaultStageTitle = group.Key.IsBridge
                    ? $"Củng cố nền tảng JLPT {group.Key.LevelCode}"
                    : $"Chặng học JLPT {group.Key.LevelCode}";

                stages.Add(new StandardRoadmapStageResponse
                {
                    StageId = group.Key.TopicId ?? syntheticStageId--,
                    StageCode = firstRaw.TopicStageCode,
                    Title = firstRaw.TopicTitle ?? defaultStageTitle,
                    Description = group.Key.IsBridge
                        ? $"Hệ thống bổ trợ kiến thức nền JLPT {group.Key.LevelCode} giúp bạn sẵn sàng cho mục tiêu {targetCode}."
                        : null,
                    LearningObjectives = firstRaw.TopicObjectives,
                    LevelCode = group.Key.LevelCode ?? targetCode,
                    IsBridgeStage = group.Key.IsBridge,
                    SortOrder = stageOrder++,
                    Status = stageStatus,
                    TotalLessons = stageLessons.Count,
                    CompletedLessons = stageLessons.Count(l => l.IsCompleted),
                    EstimatedMinutesTotal = stageLessons.Sum(l => l.EstimatedMinutes),
                    Lessons = stageLessons,
                    GroupQuiz = groupQuiz,
                });
            }

            // Attach level-wide Stage Assessment checkpoint onto the final stage of each JLPT level
            foreach (var levelGroup in stages.GroupBy(s => s.LevelCode))
            {
                if (!levelMap.TryGetValue(levelGroup.Key, out var lvlId)) continue;
                var sa = checkpointRows.FirstOrDefault(c => c.Scope == PracticeExerciseScopes.StageAssessment && c.CertificateLevelId == lvlId);
                if (sa is null) continue;

                var levelStages = levelGroup.ToList();
                var anchorStage = levelStages.LastOrDefault();
                if (anchorStage is null) continue;

                var levelLessons = levelStages.SelectMany(s => s.Lessons).ToList();
                var levelReady = levelLessons.Count > 0 && levelLessons.All(l => l.IsCompleted || l.IsStudied);
                statsByExercise.TryGetValue(sa.Id, out var sStat);
                var saPassing = sa.PassingScorePercent > 0 ? sa.PassingScorePercent : LessonPracticeProgress.PassingScore;
                var saPassed = sStat is { HasAnyScore: true } && sStat.BestScore >= saPassing;

                anchorStage.StageAssessment = new RoadmapCheckpointResponse
                {
                    ExerciseId = sa.Id,
                    Title = sa.Title,
                    Description = sa.Description,
                    Scope = sa.Scope,
                    SkillCode = JlptCurriculum.ResolveSkillCode(sa.LanguageSkillId, sa.SkillCode, sa.SkillName),
                    QuestionCount = sa.QuestionCount,
                    PassingScorePercent = saPassing,
                    IsUnlocked = levelReady,
                    IsPassed = saPassed,
                    BestScorePercent = sStat is { HasAnyScore: true } ? Math.Round(sStat.BestScore, 1) : null,
                    AttemptCount = sStat?.Count ?? 0,
                    Status = !levelReady ? "Locked" : saPassed ? "Passed" : sStat is { Count: > 0 } ? "NeedsReview" : "Available",
                    LockReason = levelReady ? null : $"Hoàn thành các bài học cấp {levelGroup.Key} để mở đánh giá cuối chặng.",
                };

                if (sa.QuestionCount > 0)
                {
                    if (anchorStage.Status == "Completed")
                    {
                        anchorStage.Status = saPassed
                            ? "Mastered"
                            : sStat is { Count: > 0 }
                                ? "NeedsReview"
                                : "InProgress";
                    }
                    else if (!saPassed && sStat is { Count: > 0 } && anchorStage.Status == "InProgress")
                    {
                        anchorStage.Status = "NeedsReview";
                    }
                }
            }

            var flatLessons = stages.SelectMany(s => s.Lessons).ToList();
            for (var i = 0; i < flatLessons.Count; i++) flatLessons[i].SortOrder = i + 1;

            // Pass 4: the recommended roadmap is strictly sequential (no skipping). Runs before ranking so the
            // recommended next lesson is always the first open step, and CheckAssessmentLessonAsync (lesson detail,
            // completion, Quick Check) rejects any later step with SELF_STUDY_LESSON_LOCKED.
            ApplySequentialLocks(stages);

            foreach (var skill in skillAssessmentsByCode.Values)
            {
                skill.Assessment.SuggestedStudySharePercent = flatLessons.Count == 0
                    ? null
                    : Math.Round(flatLessons.Count(row => row.SkillCode == skill.Assessment.SkillCode) * 100d / flatLessons.Count, 1);
            }

            var dailyMinutes = user.DailyStudyMinutesMin ?? user.DailyStudyMinutes;
            if (dailyMinutes <= 0) dailyMinutes = 30;

            AdaptiveRoadmapRankingPolicy.AnnotateLessonsAndSelectNext(
                stages,
                skillByCode,
                profileResult.FailedLessonIds,
                targetCode,
                dailyMinutes,
                profileResult.SrsDueTodayCount,
                profileResult.DaysSinceLastStudy,
                out var selection);

            var workloadAdvice = profileResult.DaysSinceLastStudy >= 3
                ? $"Bạn đã nghỉ {profileResult.DaysSinceLastStudy} ngày — hãy quay lại nhẹ nhàng với {Math.Min(profileResult.SrsDueTodayCount, 10)} thẻ SRS đến hạn và 1 bài khoảng {dailyMinutes} phút."
                : $"Nhịp học đề xuất ~{dailyMinutes} phút/ngày" + (profileResult.SrsDueTodayCount > 0 ? $" · {profileResult.SrsDueTodayCount} thẻ SRS đến hạn hôm nay." : ".");

            var adaptiveNotice = !hasPlacementEvidence
                ? "Lộ trình đang khởi tạo theo mục tiêu JLPT của bạn. Làm bài đánh giá năng lực hoặc các Quick Check để hệ thống đo chính xác từng kỹ năng."
                : bridgeLevel is not null && bridgeLevel.Id != target.Id
                    ? $"Đã bổ sung chặng cầu nối JLPT {bridgeLevel.Code} cho những kỹ năng cần củng cố trước khi vào trọn vẹn JLPT {targetCode}."
                    : $"Lộ trình đã tối ưu theo hồ sơ kỹ năng thực tế cho mục tiêu JLPT {targetCode}.";

            var activeStudyLevel = bridgeLevel ?? target;

            return ApiResponse<StandardRoadmapResponse>.Success(new StandardRoadmapResponse
            {
                CurrentStudyLevelId = activeStudyLevel.Id,
                CurrentStudyLevelName = activeStudyLevel.Code.Trim().ToUpperInvariant(),
                TargetJlptLevelId = target.Id,
                TargetJlptLevelName = targetCode,
                LearningMode = user.LearningMode == LearningMode.AiPersonalized ? "AI_PERSONALIZED" : "STANDARD",
                PlacementAttemptId = profileResult.LatestPlacementAttemptId,
                ComparisonLevelCode = comparisonCode,
                TotalLessons = flatLessons.Count,
                CompletedLessons = flatLessons.Count(row => row.IsCompleted),
                NextLessonId = selection.NextLessonId,
                Skills = skillAssessmentsByCode.Values.ToList(),
                Lessons = flatLessons,
                HasPlacementEvidence = hasPlacementEvidence,
                PlacementDecision = placementDecisionCode,
                DailyStudyMinutes = dailyMinutes,
                SrsDueTodayCount = profileResult.SrsDueTodayCount,
                DaysSinceLastStudy = profileResult.DaysSinceLastStudy,
                WorkloadAdvice = workloadAdvice,
                AdaptiveNotice = adaptiveNotice,
                NextRecommendationReason = selection.NextRecommendationReason,
                NextActionType = selection.NextActionType,
                NextCheckpointExerciseId = selection.NextCheckpointExerciseId,
                SkillProfile = profileResult.Skills,
                Stages = stages,
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (DbException exception) when (exception.SqlState is "42703" or "42P01")
        {
            logger?.LogError(exception, "The database schema is incomplete for the self-study roadmap.");
            return ApiResponse<StandardRoadmapResponse>.Fail(
                "Cơ sở dữ liệu cần được cập nhật trước khi tải lộ trình tự học.",
                HttpStatusCode.InternalServerError, "STUDY_SCHEMA_UPDATE_REQUIRED");
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Failed to load the adaptive self-study roadmap.");
            return ApiResponse<StandardRoadmapResponse>.Fail(
                "Không thể tải lộ trình tự học thích ứng.", HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Sequential gate over the ordered roadmap: once a step is not cleared, every later unfinished lesson is locked
    /// and every later checkpoint stays closed. Lessons clear by passing their Quick Check (or by being studied when
    /// they have none); a Group Quiz / Stage Assessment gates the following steps only when it has questions, so an
    /// empty checkpoint never deadlocks the roadmap. Completed lessons stay open for review; explicit prerequisites
    /// from Pass 2 are kept.
    /// </summary>
    private static void ApplySequentialLocks(List<StandardRoadmapStageResponse> stages)
    {
        const string checkpointLockReason = "Hoàn thành các bước trước trong lộ trình để mở bài kiểm tra này.";
        var blocked = false;
        foreach (var stage in stages.OrderBy(s => s.SortOrder))
        {
            foreach (var lesson in stage.Lessons.OrderBy(l => l.SortOrder))
            {
                if (blocked && !lesson.IsCompleted) lesson.IsLocked = true;
                if (!lesson.IsCompleted) blocked = true;
            }

            foreach (var checkpoint in new[] { stage.GroupQuiz, stage.StageAssessment })
            {
                if (checkpoint is null || checkpoint.QuestionCount <= 0) continue;
                if (blocked && !checkpoint.IsPassed && checkpoint.IsUnlocked)
                {
                    checkpoint.IsUnlocked = false;
                    checkpoint.Status = "Locked";
                    checkpoint.LockReason = checkpointLockReason;
                }
                if (!checkpoint.IsPassed) blocked = true;
            }
        }
    }

    public async Task<ApiResponse> CheckAssessmentLessonAsync(int lessonId, CancellationToken cancellationToken)
    {
        var result = await GetAssessmentRoadmapAsync(cancellationToken);
        if (!result.IsSuccess)
            return ApiResponse.Fail(result.ErrorMessage ?? "Không thể tải lộ trình tự học.",
                (HttpStatusCode)result.StatusCode, result.ErrorCode);

        var lesson = result.Result!.Lessons.Find(row => row.LessonId == lessonId);
        if (lesson is null)
            return ApiResponse.NotFound("Bài này không thuộc lộ trình tự học hiện tại.");
        return lesson.IsLocked
            ? ApiResponse.Fail("Hãy hoàn thành bài tiên quyết trước khi mở bài học này.", HttpStatusCode.Conflict, "SELF_STUDY_LESSON_LOCKED")
            : ApiResponse.Success();
    }

    public async Task<ApiResponse<LearnerSetupStateResponse>> ActivateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var gateFailure = await ValidateStandardEligibilityAsync(userId, cancellationToken);
            if (gateFailure is not null)
                return ApiResponse<LearnerSetupStateResponse>.Fail(
                    gateFailure.ErrorMessage ?? "Chưa đủ điều kiện mở lộ trình tiêu chuẩn.",
                    (HttpStatusCode)gateFailure.StatusCode,
                    gateFailure.ErrorCode);

            var user = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, cancellationToken);
            if (user is null)
                return ApiResponse<LearnerSetupStateResponse>.NotFound("Không tìm thấy người dùng.");

            var changed = false;
            if (user.LearningMode != LearningMode.Standard)
            {
                user.LearningMode = LearningMode.Standard;
                user.LearningGoalVersion++;
                unitOfWork.UserAccounts.Update(user);
                changed = true;
            }

            var recorded = await JourneyEventRecorder.RecordAsync(
                unitOfWork, userId, JourneyEventRecorder.StandardRoadmapReady, user.LearningGoalVersion,
                "Lộ trình học của bạn đã sẵn sàng!",
                "Lộ trình tiêu chuẩn theo mục tiêu JLPT của bạn đã được mở.",
                "/roadmap/", cancellationToken);

            if (changed || recorded)
                await unitOfWork.SaveChangesAsync(cancellationToken);

            var resolver = setupStateService ?? new LearnerSetupStateService(unitOfWork, claimService);
            var updatedState = await resolver.ResolveForUserAsync(userId, includeProgress: true, cancellationToken);
            if (updatedState is null)
                return ApiResponse<LearnerSetupStateResponse>.NotFound("Không tìm thấy người dùng.");

            return ApiResponse<LearnerSetupStateResponse>.Success(updatedState);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return ApiResponse<LearnerSetupStateResponse>.Fail(
                "Không thể khởi tạo lộ trình tiêu chuẩn.", HttpStatusCode.InternalServerError);
        }
    }
}
