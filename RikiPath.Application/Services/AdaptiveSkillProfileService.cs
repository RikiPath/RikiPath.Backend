using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Responses.LearningPaths;
using RikiPath.Application.Responses.PlacementTests;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

/// <summary>
/// Weighted learner skill evidence aggregator combining Placement, Mock Tests,
/// Practice Exercises (Quick Check / Group Quiz / Stage Assessment), and Scheduled SRS recall.
/// Never promotes unassessed skills to fake percentages or treats flashcard count alone as JLPT mastery.
/// </summary>
public sealed class AdaptiveSkillProfileService(IUnitOfWork unitOfWork)
{
    private const double MinimumWeightedSample = 3.0;
    private static readonly string[] CanonicalSkills = ["vocabulary", "kanji", "grammar", "reading", "listening"];

    private sealed class SkillAccumulator
    {
        public double WeightedScoreSum { get; set; }
        public double TotalWeight { get; set; }
        public double PrimaryWeightedQuestions { get; set; }
        public int EvidenceCount { get; set; }
        public DateTime? LastAssessedAtUtc { get; set; }
        public HashSet<string> Sources { get; } = [];
        public HashSet<string> WeakTopics { get; } = [];
        public HashSet<string> StrongTopics { get; } = [];
    }

    public sealed record Result(
        List<AdaptiveSkillProfileItemResponse> Skills,
        int SrsDueTodayCount,
        int DaysSinceLastStudy,
        HashSet<int> FailedLessonIds,
        HashSet<int> WeakTopicIds,
        int? LatestPlacementAttemptId,
        PlacementTestResultResponse? LatestPlacementResult,
        List<LearningPathSectionAssessmentResponse> PlacementAssessments);

    public async Task<Result> BuildProfileAsync(
        int userId,
        int targetLevelId,
        string targetLevelCode,
        DateTime? lastStudyDate,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var buckets = CanonicalSkills.ToDictionary(code => code, _ => new SkillAccumulator());
        var failedLessonIds = new HashSet<int>();
        var weakTopicIds = new HashSet<int>();

        // 1. Placement Evidence (strictly compatible with target level)
        var placementAttempts = await unitOfWork.MockTestAttempts.GetPlacementByUserAsync(userId, cancellationToken);
        var compatibility = PracticeTestService.EvaluatePlacementCompatibility(
            placementAttempts, targetLevelId, targetLevelCode);
        var fallbackAttempt = compatibility.CompatibleAttempt;
        var fallbackResult = compatibility.CompatibleResult;

        var placementAssessments = new List<LearningPathSectionAssessmentResponse>();
        if (fallbackAttempt is not null && fallbackResult is not null)
        {
            var audioEvidence = PracticeTestService.TryGetSkillEvidenceFromSnapshot(fallbackAttempt.QuestionSnapshotJson);
            placementAssessments = JlptSkillAssessmentPolicy.Assess(fallbackResult, targetLevelCode, audioEvidence);

            var assessedAt = BusinessDateHelper.EnsureUtc(fallbackAttempt.SubmittedAt ?? fallbackAttempt.StartedAt);
            var decay = DecayFactor(utcNow, assessedAt, 90d);
            foreach (var sec in audioEvidence ?? fallbackResult.Sections)
            {
                if (sec.TotalCount <= 0 || sec.CorrectCount < 0 || sec.CorrectCount > sec.TotalCount)
                    continue;
                var skillCode = JlptCurriculum.ResolveSkillCode(null, null,
                    string.IsNullOrWhiteSpace(sec.LanguageSkillName) ? sec.SectionTitle : sec.LanguageSkillName);
                if (skillCode == "listening" && sec.HasAudioEvidence != true)
                    continue;
                if (!buckets.TryGetValue(skillCode, out var acc))
                    continue;

                var pct = sec.CorrectCount * 100d / sec.TotalCount;
                var questionUnits = Math.Min(15.0, sec.TotalCount) * decay;
                var weight = 1.0 * questionUnits;
                acc.WeightedScoreSum += pct * weight;
                acc.TotalWeight += weight;
                acc.PrimaryWeightedQuestions += sec.TotalCount * decay;
                acc.EvidenceCount += sec.TotalCount;
                acc.Sources.Add("Placement");
                UpdateLatest(acc, assessedAt);
            }
        }

        // 2. Non-Placement MockTest / PracticeSet Section Results
        var mockResults = await unitOfWork.MockTestSectionResults.Query()
            .AsNoTracking()
            .Where(r => !r.IsDeleted
                && !r.MockTestAttempt.IsDeleted
                && r.MockTestAttempt.UserId == userId
                && r.MockTestAttempt.IsCompleted
                && r.MockTestAttempt.SubmittedAt != null
                && !r.MockTestAttempt.IsPlacementAssessment
                && r.MockTestAttempt.AttemptKind != AttemptKind.Placement
                && r.TotalCount > 0)
            .Select(r => new
            {
                r.CorrectCount,
                r.TotalCount,
                r.ScorePercent,
                SubmittedAt = r.MockTestAttempt.SubmittedAt!.Value,
                SkillId = r.MockTestSection.LanguageSkillId,
                SkillCode = r.MockTestSection.LanguageSkill.Code,
                SkillName = r.MockTestSection.LanguageSkill.Name,
                SectionTitle = r.MockTestSection.Title,
            })
            .ToListAsync(cancellationToken);

        foreach (var mr in mockResults)
        {
            var skillCode = JlptCurriculum.ResolveSkillCode(mr.SkillId, mr.SkillCode, mr.SkillName ?? mr.SectionTitle);
            if (!buckets.TryGetValue(skillCode, out var acc))
                continue;

            var assessedAt = BusinessDateHelper.EnsureUtc(mr.SubmittedAt);
            var decay = DecayFactor(utcNow, assessedAt, 60d);
            var reliabilityScale = Math.Min(1.0, mr.TotalCount / 5.0);
            var weight = 1.0 * Math.Min(15.0, mr.TotalCount) * decay * reliabilityScale;
            acc.WeightedScoreSum += mr.ScorePercent * weight;
            acc.TotalWeight += weight;
            acc.PrimaryWeightedQuestions += mr.TotalCount * decay;
            acc.EvidenceCount += mr.TotalCount;
            acc.Sources.Add("MockTest");
            UpdateLatest(acc, assessedAt);
        }

        // 3. Practice Exercise Attempts (QuickCheck / GroupQuiz / StageAssessment, excl. Homework)
        var practiceAttempts = await unitOfWork.PracticeExerciseAttempts.Query()
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.UserId == userId
                && a.HomeworkAssignmentId == null
                && a.IsCompleted
                && a.SubmittedAt != null
                && a.ScorePercent.HasValue
                && !a.PracticeExercise.IsDeleted)
            .Select(a => new
            {
                a.PracticeExerciseId,
                Scope = a.PracticeExercise.Scope,
                PassingScore = a.PracticeExercise.PassingScorePercent,
                LessonId = a.PracticeExercise.LessonId,
                LessonTopicId = a.PracticeExercise.Lesson != null ? a.PracticeExercise.Lesson.TopicId : null,
                DirectTopicId = a.PracticeExercise.TopicId,
                ExerciseTitle = a.PracticeExercise.Title,
                TopicTitle = a.PracticeExercise.Topic != null
                    ? a.PracticeExercise.Topic.Title
                    : (a.PracticeExercise.Lesson != null && a.PracticeExercise.Lesson.Topic != null
                        ? a.PracticeExercise.Lesson.Topic.Title
                        : null),
                SkillId = a.PracticeExercise.LanguageSkillId,
                SkillCode = a.PracticeExercise.LanguageSkill.Code,
                SkillName = a.PracticeExercise.LanguageSkill.Name,
                ScorePercent = a.ScorePercent!.Value,
                SubmittedAt = a.SubmittedAt!.Value,
                QuestionCount = a.PracticeExercise.Questions!.Count(q => !q.IsDeleted),
            })
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync(cancellationToken);

        var latestByExercise = new HashSet<int>();
        foreach (var pa in practiceAttempts)
        {
            var isLatestForExercise = latestByExercise.Add(pa.PracticeExerciseId);
            if (isLatestForExercise && pa.ScorePercent < pa.PassingScore)
            {
                if (pa.LessonId.HasValue) failedLessonIds.Add(pa.LessonId.Value);
                if (pa.DirectTopicId.HasValue) weakTopicIds.Add(pa.DirectTopicId.Value);
                if (pa.LessonTopicId.HasValue) weakTopicIds.Add(pa.LessonTopicId.Value);
            }

            var skillCode = JlptCurriculum.ResolveSkillCode(pa.SkillId, pa.SkillCode, pa.SkillName);
            if (!buckets.TryGetValue(skillCode, out var acc))
                continue;

            var assessedAt = BusinessDateHelper.EnsureUtc(pa.SubmittedAt);
            var (baseWeight, halfLifeDays, sourceTag) = pa.Scope switch
            {
                PracticeExerciseScopes.StageAssessment => (1.0, 60d, "StageAssessment"),
                PracticeExerciseScopes.GroupQuiz => (0.75, 45d, "GroupQuiz"),
                _ => (0.50, 30d, "QuickCheck"),
            };

            var qCount = Math.Max(1, pa.QuestionCount);
            var decay = DecayFactor(utcNow, assessedAt, halfLifeDays);
            var weight = baseWeight * Math.Min(15.0, qCount) * decay;
            acc.WeightedScoreSum += pa.ScorePercent * weight;
            acc.TotalWeight += weight;
            acc.PrimaryWeightedQuestions += qCount * decay;
            acc.EvidenceCount += qCount;
            acc.Sources.Add(sourceTag);
            UpdateLatest(acc, assessedAt);

            var topicLabel = pa.TopicTitle ?? pa.ExerciseTitle;
            if (isLatestForExercise && !string.IsNullOrWhiteSpace(topicLabel))
            {
                if (pa.ScorePercent < pa.PassingScore) acc.WeakTopics.Add(topicLabel);
                else if (pa.ScorePercent >= 85) acc.StrongTopics.Add(topicLabel);
            }
        }

        // 4. Scheduled SRS Recall Signal (NEVER counts Free Practice or unreviewed flashcard creation)
        var endOfTodayUtc = utcNow.Date.AddDays(1);
        var srsCards = await unitOfWork.ReviewCards.Query()
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.UserId == userId)
            .Select(c => new
            {
                c.Status,
                c.NextReviewDate,
                c.CardType,
                c.Repetitions,
                c.Lapses,
                c.CorrectCount,
                c.IncorrectCount,
                c.LastReviewedAt,
            })
            .ToListAsync(cancellationToken);

        var dueCount = srsCards.Count(c => c.Status == SrsCardStatus.Active && c.NextReviewDate < endOfTodayUtc);

        foreach (var group in srsCards
            .Where(c => c.Repetitions > 0 && (c.CorrectCount + c.IncorrectCount) >= 3)
            .GroupBy(c => JlptCurriculum.ResolveSkillCode(null, null, c.CardType)))
        {
            if (!buckets.TryGetValue(group.Key, out var acc))
                continue;

            var cardList = group.ToList();
            if (cardList.Count == 0) continue;
            var totalCorrect = cardList.Sum(c => c.CorrectCount);
            var totalWrong = cardList.Sum(c => c.IncorrectCount) + cardList.Sum(c => c.Lapses) * 0.5;
            var recallPct = totalCorrect * 100d / Math.Max(1.0, totalCorrect + totalWrong);
            var srsWeight = Math.Min(2.5, cardList.Count * 0.25); // capped so SRS refines, never overrides missing primary evidence
            acc.WeightedScoreSum += recallPct * srsWeight;
            acc.TotalWeight += srsWeight;
            acc.EvidenceCount += cardList.Count;
            acc.Sources.Add("SrsRecall");
            var latestSrs = cardList.Where(c => c.LastReviewedAt.HasValue).Select(c => c.LastReviewedAt!.Value).DefaultIfEmpty().Max();
            if (latestSrs != default) UpdateLatest(acc, BusinessDateHelper.EnsureUtc(latestSrs));
        }

        // 5. Days since last study activity across progress + quiz + mock
        var latestProgressAt = await unitOfWork.LessonProgresses.Query()
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.UserId == userId)
            .Select(p => (DateTime?)(p.LastInteractedAt ?? p.LastWatchedAt ?? p.CompletedAt ?? p.FirstViewedAt))
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync(cancellationToken);

        var latestActivity = new[]
        {
            lastStudyDate,
            latestProgressAt,
            practiceAttempts.Select(a => (DateTime?)a.SubmittedAt).FirstOrDefault(),
            fallbackAttempt?.SubmittedAt,
        }
        .Where(d => d.HasValue)
        .Select(d => BusinessDateHelper.EnsureUtc(d!.Value))
        .DefaultIfEmpty()
        .Max();

        var daysSinceLastStudy = latestActivity == default
            ? 0
            : Math.Max(0, (int)(utcNow.Date - latestActivity.Date).TotalDays);

        // 6. Finalize immutable per-skill DTOs
        var skillItems = CanonicalSkills.Select(code =>
        {
            var acc = buckets[code];
            var hasSufficient = acc.PrimaryWeightedQuestions >= MinimumWeightedSample && acc.TotalWeight > 0;
            double? pct = hasSufficient
                ? Math.Round(Math.Clamp(acc.WeightedScoreSum / acc.TotalWeight, 0d, 100d), 1)
                : null;

            var status = !hasSufficient
                ? "NOT_YET_ASSESSED"
                : pct >= 80d
                    ? "STRONG"
                    : pct >= 60d
                        ? "DEVELOPING"
                        : "NEEDS_REVIEW";

            var priority = status switch
            {
                "NOT_YET_ASSESSED" => "needs_assessment",
                "NEEDS_REVIEW" => "high",
                "DEVELOPING" => "medium",
                _ => "maintenance",
            };

            var confidence = !hasSufficient
                ? "low"
                : acc.PrimaryWeightedQuestions >= 10.0 && acc.Sources.Count >= 2
                    ? "high"
                    : "medium";

            var label = JlptCurriculum.SkillLabel(code);
            var summary = status switch
            {
                "NOT_YET_ASSESSED" => $"{label}: Chưa đủ dữ liệu đánh giá. Hãy làm bài đánh giá năng lực hoặc hoàn thành bài luyện tập để hệ thống đo lường chính xác.",
                "NEEDS_REVIEW" => $"{label}: Đạt khoảng {pct:0.#}% dựa trên {acc.EvidenceCount} mục bằng chứng. Cần ưu tiên củng cố.",
                "DEVELOPING" => $"{label}: Đạt khoảng {pct:0.#}% và đang tiến bộ ổn định hướng tới {targetLevelCode}.",
                _ => $"{label}: Đạt {pct:0.#}%, nền tảng vững — duy trì ôn nhắc lại hợp lý.",
            };

            return new AdaptiveSkillProfileItemResponse
            {
                SkillCode = code,
                SkillLabel = label,
                ProficiencyStatus = status,
                ProficiencyPercent = pct,
                Confidence = confidence,
                Priority = priority,
                EvidenceCount = acc.EvidenceCount,
                WeightedQuestionCount = Math.Round(acc.PrimaryWeightedQuestions, 1),
                LastAssessedAtUtc = acc.LastAssessedAtUtc,
                EvidenceSources = acc.Sources.OrderBy(s => s).ToList(),
                WeakTopics = acc.WeakTopics.Take(5).ToList(),
                StrongTopics = acc.StrongTopics.Where(t => !acc.WeakTopics.Contains(t)).Take(5).ToList(),
                Summary = summary,
            };
        }).ToList();

        return new Result(
            skillItems,
            dueCount,
            daysSinceLastStudy,
            failedLessonIds,
            weakTopicIds,
            fallbackAttempt?.Id,
            fallbackResult,
            placementAssessments);
    }

    private static double DecayFactor(DateTime utcNow, DateTime assessedAtUtc, double halfLifeDays)
    {
        var ageDays = Math.Max(0d, (utcNow - assessedAtUtc).TotalDays);
        return Math.Max(0.35d, Math.Exp(-ageDays / halfLifeDays));
    }

    private static void UpdateLatest(SkillAccumulator acc, DateTime timestampUtc)
    {
        if (!acc.LastAssessedAtUtc.HasValue || timestampUtc > acc.LastAssessedAtUtc.Value)
            acc.LastAssessedAtUtc = timestampUtc;
    }
}
