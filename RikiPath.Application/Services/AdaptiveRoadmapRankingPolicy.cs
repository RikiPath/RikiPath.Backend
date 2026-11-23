using RikiPath.Application.Responses.LearningPaths;

namespace RikiPath.Application.Services;

/// <summary>
/// Centralized, deterministic lesson and checkpoint recommendation ranking rules for Standard Roadmap V2.
/// Internal ranking weights never leak into UI responses; only human-readable explanations are surfaced.
/// </summary>
internal static class AdaptiveRoadmapRankingPolicy
{
    public const double GoalRelevanceWeight = 35d;
    public const double WeaknessPriorityWeight = 30d;
    public const double RemediationBoostWeight = 25d;
    public const double PrerequisiteReadyWeight = 20d;
    public const double StageSequenceWeight = 15d;
    public const double WorkloadFitWeight = 10d;

    public sealed record RecommendationSelection(
        int? NextLessonId,
        string NextActionType,
        int? NextCheckpointExerciseId,
        string NextRecommendationReason);

    public static void AnnotateLessonsAndSelectNext(
        IReadOnlyList<StandardRoadmapStageResponse> stages,
        IReadOnlyDictionary<string, AdaptiveSkillProfileItemResponse> skillByCode,
        HashSet<int> failedLessonIds,
        string targetLevelCode,
        int dailyStudyMinutes,
        int srsDueTodayCount,
        int daysSinceLastStudy,
        out RecommendationSelection selection)
    {
        var allLessons = stages.SelectMany(s => s.Lessons).ToList();
        var scoredCandidates = new List<(StandardRoadmapLessonResponse Lesson, StandardRoadmapStageResponse Stage, double Score, string Reason)>();

        for (var stageIdx = 0; stageIdx < stages.Count; stageIdx++)
        {
            var stage = stages[stageIdx];
            for (var lessonIdx = 0; lessonIdx < stage.Lessons.Count; lessonIdx++)
            {
                var lesson = stage.Lessons[lessonIdx];
                var skillCode = lesson.SkillCode ?? "vocabulary";
                skillByCode.TryGetValue(skillCode, out var skillProf);
                var skillLabel = skillProf?.SkillLabel ?? JlptCurriculum.SkillLabel(skillCode);
                var isFailed = failedLessonIds.Contains(lesson.LessonId) || lesson.CompletionState == "NeedsReview";

                lesson.IsRemediation = isFailed || stage.IsBridgeStage;

                if (lesson.IsCompleted)
                {
                    lesson.RecommendationReason = null;
                    continue;
                }

                if (isFailed)
                {
                    lesson.RecommendationReason = string.IsNullOrWhiteSpace(stage.Title)
                        ? $"Cần ôn lại kiến thức {skillLabel} và làm lại Quick Check của bài này."
                        : $"Cần ôn lại {skillLabel} và làm lại Quick Check trong {stage.Title}.";
                }
                else if (stage.IsBridgeStage)
                {
                    lesson.RecommendationReason = $"Củng cố nền tảng {skillLabel} ở cấp {stage.LevelCode} trước khi tập trung {targetLevelCode}.";
                }
                else if (skillProf?.ProficiencyStatus == "NEEDS_REVIEW")
                {
                    lesson.RecommendationReason = $"Được đề xuất ưu tiên vì kỹ năng {skillLabel} ở mục tiêu {targetLevelCode} đang cần củng cố.";
                }
                else if (skillProf?.ProficiencyStatus == "NOT_YET_ASSESSED")
                {
                    lesson.RecommendationReason = $"Giúp xây dựng nền tảng {skillLabel} và ghi nhận thêm dữ liệu năng lực ở cấp {stage.LevelCode}.";
                }
                else
                {
                    lesson.RecommendationReason = $"Bước tiếp theo trong chặng \"{stage.Title}\" hướng tới JLPT {targetLevelCode}.";
                }

                if (lesson.IsLocked) continue;

                // Centralized multi-factor score
                double score = 0;
                // 1. Prerequisite & foundation priority: bridge stages before target when weak foundation exists
                score += stage.IsBridgeStage ? GoalRelevanceWeight + 8d :
                    (lesson.LevelCode == targetLevelCode ? GoalRelevanceWeight : 18d);

                // 2. Skill weakness priority (without ignoring other skills)
                score += skillProf?.Priority switch
                {
                    "high" => WeaknessPriorityWeight,
                    "medium" => WeaknessPriorityWeight * 0.6d,
                    "needs_assessment" => WeaknessPriorityWeight * 0.5d,
                    _ => WeaknessPriorityWeight * 0.2d,
                };

                // 3. Failed QuickCheck remediation boost
                if (isFailed) score += RemediationBoostWeight;

                // 4. Explicit prerequisite met or in-progress momentum
                score += PrerequisiteReadyWeight;
                if (lesson.CompletionState == "Studied" && lesson.HasPracticeExercise && !lesson.PracticePassed)
                    score += 12d; // learner already studied content; finish Quick Check next

                // 5. Preserve stage sequence so lessons remain predictable & stable
                score += Math.Max(0d, StageSequenceWeight - stageIdx * 2.5d - lessonIdx * 1.2d);

                // 6. Workload fit for learner daily minutes (prefer shorter lessons after break or low budget)
                var budget = Math.Max(15, dailyStudyMinutes);
                if (daysSinceLastStudy >= 3 && lesson.EstimatedMinutes <= Math.Max(20, budget))
                    score += WorkloadFitWeight;
                else if (lesson.EstimatedMinutes <= budget)
                    score += WorkloadFitWeight * 0.7d;

                scoredCandidates.Add((lesson, stage, score, lesson.RecommendationReason));
            }
        }

        // Prefer completing an unlocked failed/pending stage checkpoint when its stage's lessons are all done
        foreach (var stage in stages)
        {
            if (stage.TotalLessons > 0 && stage.CompletedLessons == stage.TotalLessons)
            {
                if (stage.GroupQuiz is { IsUnlocked: true, IsPassed: false } gq)
                {
                    selection = new RecommendationSelection(
                        null,
                        "GroupQuiz",
                        gq.ExerciseId,
                        $"Bạn đã hoàn thành toàn bộ bài học trong \"{stage.Title}\". Hãy làm \"{gq.Title}\" để kiểm tra tổng hợp chặng.");
                    return;
                }
                if (stage.StageAssessment is { IsUnlocked: true, IsPassed: false } sa)
                {
                    selection = new RecommendationSelection(
                        null,
                        "StageAssessment",
                        sa.ExerciseId,
                        $"Bạn đã sẵn sàng làm bài đánh giá chặng \"{sa.Title}\" cho cấp {stage.LevelCode}.");
                    return;
                }
            }
        }

        var top = scoredCandidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Stage.IsBridgeStage ? 0 : 1)
            .ThenBy(c => c.Stage.SortOrder)
            .ThenBy(c => c.Lesson.SortOrder)
            .FirstOrDefault();

        if (top.Lesson is not null)
        {
            top.Lesson.IsRecommendedNext = true;
            var actionType = top.Lesson.IsStudied && top.Lesson.HasPracticeExercise && !top.Lesson.PracticePassed
                ? "QuickCheck"
                : "Lesson";
            selection = new RecommendationSelection(top.Lesson.LessonId, actionType, null, top.Reason);
            return;
        }

        if (srsDueTodayCount > 0)
        {
            selection = new RecommendationSelection(
                null,
                "Review",
                null,
                $"Bạn đã hoàn thành các bài học hiện có trong lộ trình. Hãy ôn tập {srsDueTodayCount} thẻ SRS đến hạn hôm nay.");
            return;
        }

        selection = new RecommendationSelection(
            null,
            "Completed",
            null,
            allLessons.Count == 0
                ? $"Hiện chưa có bài học đã xuất bản cho mục tiêu {targetLevelCode}."
                : $"Chúc mừng! Bạn đã hoàn thành toàn bộ các chặng hiện có hướng tới {targetLevelCode}.");
    }
}
