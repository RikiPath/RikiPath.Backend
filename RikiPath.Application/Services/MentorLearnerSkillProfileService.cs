using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class MentorLearnerSkillProfileService(
    IUnitOfWork uow,
    IClaimService claims,
    IMentorGroupPolicyService? groupPolicies = null) : IMentorLearnerSkillProfileService
{
    private static readonly JsonSerializerOptions FeedbackJsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record FeedbackEnvelope(string? Feedback);
    private sealed record SkillSample(double Score, DateTime At, string Source);
    private sealed record SkillDescriptor(string Code, string Label);

    private static string SkillLabelVi(string codeOrName, string? fallbackName = null)
    {
        var key = (codeOrName ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "vocabulary" or "vocab" or "từ vựng" => "Từ vựng",
            "kanji" or "hán tự" => "Hán tự",
            "grammar" or "ngữ pháp" => "Ngữ pháp",
            "listening" or "nghe" or "nghe hiểu" => "Nghe hiểu",
            "reading" or "đọc" or "đọc hiểu" => "Đọc hiểu",
            "writing" or "viết" => "Viết",
            "speaking" or "nói" => "Nói",
            _ => string.IsNullOrWhiteSpace(fallbackName) ? codeOrName : fallbackName.Trim(),
        };
    }

    private static string NormalizeSkillKey(int? skillId, string? skillCode, string? skillName)
    {
        var resolved = JlptCurriculum.ResolveSkillCode(skillId, skillCode, skillName);
        if (!string.IsNullOrWhiteSpace(resolved))
            return resolved.ToLowerInvariant();
        return (skillCode ?? skillName ?? "vocabulary").Trim().ToLowerInvariant();
    }

    private static int SkillSortOrder(string? code) => (code ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "vocabulary" or "vocab" => 1,
        "kanji" => 2,
        "grammar" => 3,
        "reading" => 4,
        "listening" => 5,
        "writing" => 6,
        "speaking" => 7,
        _ => 99,
    };

    private static string? ExtractPlacementLevelCode(
        string? explicitCode,
        string? sectionTitle,
        IReadOnlyList<Responses.PlacementTests.PlacementLevelBreakdownItem> levelBreakdowns)
    {
        var code = (explicitCode ?? string.Empty).Trim().ToUpperInvariant();
        if (JlptCurriculum.Rank(code) > 0)
            return code;
        if (!string.IsNullOrWhiteSpace(sectionTitle))
        {
            var m = Regex.Match(sectionTitle.ToUpperInvariant(), @"\bN[1-5]\b");
            if (m.Success && JlptCurriculum.Rank(m.Value) > 0)
                return m.Value;
        }
        if (levelBreakdowns.Count == 1)
        {
            var single = (levelBreakdowns[0].JlptLevelName ?? string.Empty).Trim().ToUpperInvariant();
            if (JlptCurriculum.Rank(single) > 0)
                return single;
        }
        return null;
    }

    private static string? TryExtractMentorFeedbackText(string? explicitFeedback, string? gradingFeedbackJson)
    {
        if (!string.IsNullOrWhiteSpace(explicitFeedback))
            return explicitFeedback.Trim();
        if (string.IsNullOrWhiteSpace(gradingFeedbackJson))
            return null;
        try
        {
            var parsed = JsonSerializer.Deserialize<FeedbackEnvelope>(gradingFeedbackJson, FeedbackJsonOptions);
            if (!string.IsNullOrWhiteSpace(parsed?.Feedback))
                return parsed.Feedback.Trim();
        }
        catch (JsonException)
        {
            // Keep plaintext feedback readable if not serialized JSON.
        }
        return gradingFeedbackJson.TrimStart().StartsWith("{", StringComparison.Ordinal) ? null : gradingFeedbackJson.Trim();
    }

    public async Task<ApiResponse<MentorLearnerSkillProfileResponse>> GetSkillProfileAsync(
        int learnerId, int? cohortId = null, CancellationToken cancellationToken = default)
    {
        var actor = claims.GetUserClaim();
        var isMentorActor = actor.Role == Role.Mentor;
        var isSelfLearnerActor = actor.Role == Role.Learner && actor.Id == learnerId;
        if (!isMentorActor && !isSelfLearnerActor)
            return ApiResponse<MentorLearnerSkillProfileResponse>.Fail(
                "Bạn không có quyền xem hồ sơ năng lực học viên này.", HttpStatusCode.Forbidden);
        if (learnerId <= 0)
            return ApiResponse<MentorLearnerSkillProfileResponse>.Fail("Học viên không hợp lệ.");

        var nowUtc = DateTime.UtcNow;
        int enrollmentId;
        string scopeType;
        StudyCohort? cohort = null;
        StudyCourse? learnerEnrollment = null;

        if (cohortId.HasValue)
        {
            cohort = await uow.StudyCohorts.Query().AsNoTracking()
                .Include(x => x.Plan).ThenInclude(p => p.CertificateLevel)
                .Include(x => x.GroupPolicy)
                .Include(x => x.Enrollments)
                .Include(x => x.Sessions).ThenInclude(s => s.Availability).ThenInclude(a => a.GroupPolicy)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == cohortId.Value, cancellationToken);

            if (cohort is null || (isMentorActor && cohort.MentorId != actor.Id))
                return ApiResponse<MentorLearnerSkillProfileResponse>.Fail(
                    "Bạn chỉ được xem hồ sơ học viên thuộc lớp nhóm do mình phụ trách.", HttpStatusCode.Forbidden);

            var cohortEnrollmentSummary = cohort.Enrollments
                .Where(c => c.LearnerId == learnerId
                    && c.PaymentStatus is "Paid" or "PartiallyRefunded"
                    && c.Status is "Active" or "Completed" or "Ready")
                .OrderByDescending(c => c.Id)
                .FirstOrDefault();

            if (cohortEnrollmentSummary is null)
                return ApiResponse<MentorLearnerSkillProfileResponse>.Fail(
                    isMentorActor
                        ? "Bạn chỉ được xem hồ sơ học viên đã ghi danh thuộc lớp do mình phụ trách."
                        : "Không tìm thấy ghi danh hợp lệ tại lớp học này.",
                    HttpStatusCode.Forbidden);

            enrollmentId = cohortEnrollmentSummary.Id;
            learnerEnrollment = await uow.StudyCourses.Query().AsNoTracking()
                .Include(c => c.CertificateLevel)
                .Include(c => c.Sessions)
                .Include(c => c.Homework)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == enrollmentId, cancellationToken);
            scopeType = "Cohort";
        }
        else
        {
            learnerEnrollment = await uow.StudyCourses.Query().AsNoTracking()
                .Include(c => c.CertificateLevel)
                .Include(c => c.Cohort)
                .Include(c => c.Sessions)
                .Include(c => c.Homework)
                .AsSplitQuery()
                .Where(c => c.LearnerId == learnerId
                    && (c.PaymentStatus == "Paid" || c.PaymentStatus == "PartiallyRefunded")
                    && (c.Status == "Active" || c.Status == "Completed" || c.Status == "Ready")
                    && (isSelfLearnerActor || c.MentorId == actor.Id || (c.Cohort != null && c.Cohort.MentorId == actor.Id)))
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (learnerEnrollment is null)
                return ApiResponse<MentorLearnerSkillProfileResponse>.Fail(
                    "Không tìm thấy hồ sơ học tập hợp lệ.", HttpStatusCode.Forbidden);

            enrollmentId = learnerEnrollment.Id;
            scopeType = "Course";
        }

        var learner = await uow.UserAccounts.Query().AsNoTracking()
            .Where(u => u.Id == learnerId && !u.IsDeleted)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.AvatarUrl })
            .SingleOrDefaultAsync(cancellationToken);
        if (learner is null)
            return ApiResponse<MentorLearnerSkillProfileResponse>.NotFound("Không tìm thấy học viên.");

        // 1. Dynamic system skills from DB configuration (never hardcode a fixed count like 7).
        var dbSkills = uow.LanguageSkills is null
            ? []
            : await uow.LanguageSkills.Query().AsNoTracking()
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Id)
                .Select(s => new { s.Id, s.Code, s.Name })
                .ToListAsync(cancellationToken);

        var skillDescriptors = new Dictionary<string, SkillDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var ds in dbSkills)
        {
            var key = NormalizeSkillKey(ds.Id, ds.Code, ds.Name);
            if (!skillDescriptors.ContainsKey(key))
                skillDescriptors[key] = new SkillDescriptor(key, SkillLabelVi(key, ds.Name));
        }

        // 2. Cohort lifecycle & baseline cutoff (baseline preserved once class starts).
        int? targetLevelId = cohort?.Plan?.CertificateLevelId ?? learnerEnrollment?.CertificateLevelId;
        string? targetLevelCode = cohort?.Plan?.CertificateLevel?.Code ?? learnerEnrollment?.CertificateLevel?.Code;
        var hasClassStarted = false;
        var baselineCutoffUtc = nowUtc;
        var totalScheduledSessionsCount = 0;
        var completedSessionsCount = 0;
        LearnerCompetencyCohortContextResponse? cohortContext = null;

        var deliveredLearnerSessions = (learnerEnrollment?.Sessions ?? [])
            .Where(s => s.Status is "Present" or "Absent" or "ExcusedAbsent" or "Completed")
            .ToList();
        var deliveredSessionNumbers = deliveredLearnerSessions.Select(s => s.Number).Distinct().Count();
        DateTime? earliestDeliveredSessionUtc = deliveredLearnerSessions.Count > 0
            ? deliveredLearnerSessions.Select(s => BusinessDateHelper.EnsureUtc(s.ScheduledAt)).Min()
            : null;

        if (cohort is not null)
        {
            var validSessions = cohort.Sessions
                .Where(s => s.Availability is not null
                    && s.Availability.IsApproved
                    && !s.Availability.IsDeleted
                    && s.Availability.RejectedAt == null)
                .OrderBy(s => s.Availability.StartTime)
                .ToList();
            var firstValidStartUtc = BusinessDateHelper.EnsureUtc(
                validSessions.FirstOrDefault()?.Availability.StartTime ?? cohort.StartsAt);

            var confirmedLearnerCount = cohort.Enrollments
                .Where(c => c.PaymentStatus is "Paid" or "PartiallyRefunded"
                    && c.Status is "Ready" or "Active" or "Completed")
                .Select(c => c.LearnerId)
                .Distinct()
                .Count();

            var hasDeliveredLearnerSession = deliveredLearnerSessions.Count > 0;
            var cohortNotAborted = !string.Equals(cohort.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(cohort.Status, "Draft", StringComparison.OrdinalIgnoreCase);

            hasClassStarted = string.Equals(cohort.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                || hasDeliveredLearnerSession
                || (cohortNotAborted && confirmedLearnerCount >= 1 && firstValidStartUtc <= nowUtc);

            baselineCutoffUtc = hasClassStarted
                ? (earliestDeliveredSessionUtc.HasValue && earliestDeliveredSessionUtc.Value < firstValidStartUtc
                    ? earliestDeliveredSessionUtc.Value
                    : firstValidStartUtc)
                : nowUtc;

            totalScheduledSessionsCount = Math.Max(validSessions.Count, learnerEnrollment?.TotalSessions ?? 0);
            var endedCohortSessionsCount = hasClassStarted
                ? validSessions.Count(s => BusinessDateHelper.EnsureUtc(s.Availability.EndTime) <= nowUtc)
                : 0;
            completedSessionsCount = totalScheduledSessionsCount > 0
                ? Math.Min(totalScheduledSessionsCount, Math.Max(endedCohortSessionsCount, deliveredSessionNumbers))
                : Math.Max(endedCohortSessionsCount, deliveredSessionNumbers);

            var enrolledAtUtc = BusinessDateHelper.EnsureUtc(
                learnerEnrollment?.PaymentConfirmedAtUtc ?? learnerEnrollment?.CreatedAt ?? cohort.CreatedAtUtc);

            cohortContext = new LearnerCompetencyCohortContextResponse(
                cohort.Id,
                cohort.Name,
                targetLevelCode,
                cohort.Status,
                BusinessDateHelper.EnsureUtc(cohort.StartsAt),
                BusinessDateHelper.EnsureUtc(cohort.EndsAt),
                firstValidStartUtc,
                baselineCutoffUtc,
                enrollmentId,
                learnerEnrollment?.Status ?? "Ready",
                enrolledAtUtc,
                learner.AvatarUrl);
        }
        else if (learnerEnrollment is not null)
        {
            var firstStartUtc = BusinessDateHelper.EnsureUtc(learnerEnrollment.StartsAt ?? nowUtc);
            hasClassStarted = string.Equals(learnerEnrollment.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                || deliveredLearnerSessions.Count > 0
                || firstStartUtc <= nowUtc;
            baselineCutoffUtc = hasClassStarted
                ? (earliestDeliveredSessionUtc.HasValue && earliestDeliveredSessionUtc.Value < firstStartUtc
                    ? earliestDeliveredSessionUtc.Value
                    : firstStartUtc)
                : nowUtc;
            totalScheduledSessionsCount = learnerEnrollment.TotalSessions;
            completedSessionsCount = totalScheduledSessionsCount > 0
                ? Math.Min(totalScheduledSessionsCount, deliveredSessionNumbers)
                : deliveredSessionNumbers;
        }

        // 3. A — Official Baseline Placement (strictly before class start once started; never overwritten by later study).
        var allPlacements = await uow.MockTestAttempts.GetPlacementByUserAsync(learnerId, cancellationToken);
        var preStartPlacements = allPlacements
            .Where(a => a.IsCompleted && a.TotalScore.HasValue)
            .Where(a => !hasClassStarted || BusinessDateHelper.EnsureUtc(a.SubmittedAt ?? a.StartedAt) < baselineCutoffUtc)
            .ToList();

        var baselineCompatibility = PracticeTestService.EvaluatePlacementCompatibility(
            preStartPlacements, targetLevelId, targetLevelCode);
        var chosenPlacementAttempt = baselineCompatibility.CompatibleAttempt ?? baselineCompatibility.LatestAttempt;
        var chosenPlacementResult = baselineCompatibility.CompatibleResult ?? baselineCompatibility.LatestResult;

        BaselinePlacementSummaryResponse? baselinePlacement = null;
        var baselineByJlptSkill = new Dictionary<string, (double ScorePercent, string Label)>(StringComparer.OrdinalIgnoreCase);

        if (chosenPlacementAttempt is not null && chosenPlacementResult is not null)
        {
            var audioEvidence = PracticeTestService.TryGetSkillEvidenceFromSnapshot(chosenPlacementAttempt.QuestionSnapshotJson);
            var rawEvidenceSections = audioEvidence ?? chosenPlacementResult.Sections;
            var evalLevel = !string.IsNullOrWhiteSpace(targetLevelCode)
                ? targetLevelCode
                : (!string.IsNullOrWhiteSpace(chosenPlacementResult.AssessedJlptLevelName) ? chosenPlacementResult.AssessedJlptLevelName : "N5");
            var assessed = JlptSkillAssessmentPolicy.Assess(chosenPlacementResult, evalLevel, audioEvidence);

            var structureStudySkills = chosenPlacementResult.Sections
                .Select(sec => JlptSkillAssessmentPolicy.Classify(
                    string.IsNullOrWhiteSpace(sec.LanguageSkillName) ? sec.SectionTitle : sec.LanguageSkillName))
                .Where(sk => sk.HasValue)
                .Select(sk => sk!.Value.ToString().ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var baselineSkills = new List<BaselineSkillItemResponse>();
            var baselineStrengths = new List<string>();
            var baselineFocusAreas = new List<string>();
            var handledSkillCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in assessed)
            {
                // Preserve assessed skills OR skills declared in the test structure (e.g. Listening lacking audio), without fabricating missing sections.
                if (!item.HasData && !structureStudySkills.Contains(item.SkillCode))
                    continue;

                handledSkillCodes.Add(item.SkillCode);
                if (item.HasData && item.ScorePercent.HasValue)
                    baselineByJlptSkill[item.SkillCode] = (item.ScorePercent.Value, SkillLabelVi(item.SkillCode, item.SectionTitle));

                var labelVi = SkillLabelVi(item.SkillCode, item.SectionTitle);
                baselineSkills.Add(new BaselineSkillItemResponse(
                    item.SkillCode,
                    labelVi,
                    item.HasData ? item.ScorePercent : null,
                    item.CorrectCount,
                    item.TotalCount,
                    item.Priority,
                    item.HasData ? (item.HasSufficientEvidence ? "Assessed" : "LowSample") : "Unassessed",
                    item.EstimatedLevelCode,
                    item.Comment,
                    item.Recommendation));

                if (item.HasData && item.HasSufficientEvidence && item.Priority == "maintenance")
                    baselineStrengths.Add(labelVi);
                if (item.HasData && item.Priority == "high")
                    baselineFocusAreas.Add(labelVi);
            }

            // Include dynamic additional skills in placement structure (e.g., Kanji, Writing, Speaking) if present.
            foreach (var extraGroup in rawEvidenceSections
                .Select(sec => new
                {
                    Section = sec,
                    StudySkill = JlptSkillAssessmentPolicy.Classify(
                        string.IsNullOrWhiteSpace(sec.LanguageSkillName) ? sec.SectionTitle : sec.LanguageSkillName),
                    Key = NormalizeSkillKey(
                        null,
                        sec.LanguageSkillCode,
                        string.IsNullOrWhiteSpace(sec.LanguageSkillName) ? sec.SectionTitle : sec.LanguageSkillName)
                })
                .Where(x => !x.StudySkill.HasValue && !handledSkillCodes.Contains(x.Key))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var total = extraGroup.Sum(x => x.Section.TotalCount);
                var correct = extraGroup.Sum(x => x.Section.CorrectCount);
                var labelVi = SkillLabelVi(extraGroup.Key, extraGroup.First().Section.LanguageSkillName);
                double? pct = total > 0 ? Math.Round(correct * 100d / total, 1) : null;
                var sufficient = total >= PlacementAssessmentModePolicy.MinimumQuestionsPerSkillAndLevel;
                var priority = !sufficient ? "needs_assessment" : pct >= 80d ? "maintenance" : "high";
                var status = total <= 0 ? "Unassessed" : (sufficient ? "Assessed" : "LowSample");

                if (pct.HasValue)
                    baselineByJlptSkill[extraGroup.Key] = (pct.Value, labelVi);

                baselineSkills.Add(new BaselineSkillItemResponse(
                    extraGroup.Key,
                    labelVi,
                    pct,
                    correct,
                    total,
                    priority,
                    status,
                    null,
                    total <= 0
                        ? $"{labelVi}: chưa có câu hỏi hợp lệ để chấm điểm."
                        : (!sufficient ? $"{labelVi}: chưa đủ câu hỏi hợp lệ để kết luận mức độ." : null),
                    null));

                if (total > 0 && sufficient && priority == "maintenance")
                    baselineStrengths.Add(labelVi);
                if (total > 0 && priority == "high")
                    baselineFocusAreas.Add(labelVi);
            }

            baselineSkills = baselineSkills
                .OrderBy(s => SkillSortOrder(s.SkillCode))
                .ThenBy(s => s.SkillLabel)
                .ToList();

            // Enforce strict consistency between valid skill breakdown denominator and overall score.
            var assessedSkillRows = baselineSkills.Where(s => s.Status != "Unassessed" && s.TotalCount > 0).ToList();
            int validCorrectCount;
            int validTotalQuestions;
            double validOverallScorePercent;
            if (audioEvidence is not null)
            {
                validCorrectCount = assessedSkillRows.Sum(s => s.CorrectCount);
                validTotalQuestions = assessedSkillRows.Sum(s => s.TotalCount);
                validOverallScorePercent = validTotalQuestions > 0
                    ? Math.Round(validCorrectCount * 100d / validTotalQuestions, 1)
                    : 0d;
            }
            else if (assessedSkillRows.Count > 0 && assessedSkillRows.Sum(s => s.TotalCount) != chosenPlacementResult.TotalQuestions)
            {
                validCorrectCount = assessedSkillRows.Sum(s => s.CorrectCount);
                validTotalQuestions = assessedSkillRows.Sum(s => s.TotalCount);
                validOverallScorePercent = validTotalQuestions > 0
                    ? Math.Round(validCorrectCount * 100d / validTotalQuestions, 1)
                    : 0d;
            }
            else
            {
                validCorrectCount = chosenPlacementResult.CorrectCount;
                validTotalQuestions = chosenPlacementResult.TotalQuestions;
                validOverallScorePercent = validTotalQuestions > 0
                    ? Math.Round(chosenPlacementResult.TotalScore, 1)
                    : 0d;
            }
            var excludedInvalidQuestionCount = Math.Max(0, chosenPlacementResult.TotalQuestions - validTotalQuestions);

            // Derive level achieved honestly from valid tested level breakdowns instead of trusting legacy fallback lower-bounds.
            var validLevelBreakdowns = (audioEvidence is not null
                ? audioEvidence
                    .Where(sec =>
                    {
                        var sk = JlptSkillAssessmentPolicy.Classify(
                            string.IsNullOrWhiteSpace(sec.LanguageSkillName) ? sec.SectionTitle : sec.LanguageSkillName);
                        return (sk != JlptStudySkill.Listening || sec.HasAudioEvidence == true) && sec.TotalCount > 0;
                    })
                    .Select(sec => new
                    {
                        Section = sec,
                        LevelCode = ExtractPlacementLevelCode(sec.JlptLevelName, sec.SectionTitle, chosenPlacementResult.LevelBreakdowns),
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.LevelCode))
                    .GroupBy(x => x.LevelCode!, StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var t = g.Sum(x => x.Section.TotalCount);
                        var c = g.Sum(x => x.Section.CorrectCount);
                        return new
                        {
                            LevelCode = g.Key.ToUpperInvariant(),
                            CorrectCount = c,
                            TotalCount = t,
                            ScorePercent = t > 0 ? Math.Round(c * 100d / t, 2) : 0d,
                        };
                    })
                : chosenPlacementResult.LevelBreakdowns
                    .Where(b => b.TotalCount > 0 && JlptCurriculum.Rank(b.JlptLevelName) > 0)
                    .Select(b => new
                    {
                        LevelCode = b.JlptLevelName.Trim().ToUpperInvariant(),
                        b.CorrectCount,
                        b.TotalCount,
                        b.ScorePercent,
                    }))
                .OrderBy(x => JlptCurriculum.Rank(x.LevelCode))
                .ToList();

            var testedLevelCodes = validLevelBreakdowns
                .Select(b => b.LevelCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (testedLevelCodes.Count == 0 && !string.IsNullOrWhiteSpace(chosenPlacementResult.ScreeningLevelName))
                testedLevelCodes.Add(chosenPlacementResult.ScreeningLevelName.Trim().ToUpperInvariant());
            var testedLevelScope = testedLevelCodes.Count > 0 ? string.Join("–", testedLevelCodes) : null;

            var assessedSkillCount = baselineSkills.Count(s => s.Status != "Unassessed");
            var sufficientSkillCount = baselineSkills.Count(s => s.Status == "Assessed");
            string? assessedJlptLevel;
            string assessedLevelStatus;
            if (validTotalQuestions == 0 || assessedSkillCount == 0 || (baselineSkills.Count >= 2 && sufficientSkillCount == 0))
            {
                assessedJlptLevel = null;
                assessedLevelStatus = "InsufficientData";
            }
            else
            {
                var passedTestedLevels = validLevelBreakdowns
                    .Where(b => b.TotalCount >= PlacementAssessmentModePolicy.MinimumQuestionsPerSkillAndLevel
                        && b.ScorePercent >= 60.0)
                    .OrderBy(b => JlptCurriculum.Rank(b.LevelCode))
                    .ToList();
                if (passedTestedLevels.Count > 0)
                {
                    assessedJlptLevel = passedTestedLevels[^1].LevelCode;
                    assessedLevelStatus = "Assessed";
                }
                else
                {
                    assessedJlptLevel = null;
                    assessedLevelStatus = "BelowTestedLevel";
                }
            }

            baselinePlacement = new BaselinePlacementSummaryResponse(
                chosenPlacementAttempt.Id,
                targetLevelCode,
                assessedJlptLevel,
                string.IsNullOrWhiteSpace(chosenPlacementResult.RecommendedTargetJlptLevelName) ? null : chosenPlacementResult.RecommendedTargetJlptLevelName,
                validOverallScorePercent,
                validCorrectCount,
                validTotalQuestions,
                BusinessDateHelper.EnsureUtc(chosenPlacementAttempt.SubmittedAt ?? chosenPlacementAttempt.StartedAt),
                baselineCompatibility.Status,
                baselineCompatibility.Reason,
                baselineSkills,
                baselineStrengths.Distinct().ToList(),
                baselineFocusAreas.Distinct().ToList(),
                assessedLevelStatus,
                testedLevelScope,
                assessedSkillCount,
                baselineSkills.Count,
                excludedInvalidQuestionCount);
        }

        // 4. Separate Pre-Course vs Post-Start Independent Practice / Mock Tests / AI Speaking-Writing.
        var nonPlacementMockSections = await uow.MockTestSectionResults.Query().AsNoTracking()
            .Where(r => !r.IsDeleted
                && r.TotalCount > 0
                && !r.MockTestAttempt.IsDeleted
                && r.MockTestAttempt.UserId == learnerId
                && r.MockTestAttempt.IsCompleted
                && r.MockTestAttempt.AttemptKind != AttemptKind.Placement
                && !r.MockTestAttempt.IsPlacementAssessment)
            .Select(r => new
            {
                r.MockTestAttemptId,
                CertificateLevelId = (int?)r.MockTestSection.MockTest.CertificateLevelId,
                ExamKind = r.MockTestSection.MockTest.ExamKind,
                AttemptTotalScore = r.MockTestAttempt.TotalScore,
                SkillId = (int?)r.MockTestSection.LanguageSkillId,
                SkillCode = r.MockTestSection.LanguageSkill.Code,
                SkillName = r.MockTestSection.LanguageSkill.Name,
                SectionTitle = r.MockTestSection.Title,
                r.CorrectCount,
                r.TotalCount,
                r.ScorePercent,
                At = r.MockTestAttempt.SubmittedAt ?? r.MockTestAttempt.StartedAt
            })
            .ToListAsync(cancellationToken);

        var nonHomeworkPracticeAttempts = await uow.PracticeExerciseAttempts.Query().AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.UserId == learnerId
                && a.HomeworkAssignmentId == null
                && a.IsCompleted
                && a.ScorePercent.HasValue)
            .Select(a => new
            {
                SkillId = (int?)a.PracticeExercise.LanguageSkillId,
                SkillCode = a.PracticeExercise.LanguageSkill.Code,
                SkillName = a.PracticeExercise.LanguageSkill.Name,
                Score = a.ScorePercent!.Value,
                At = a.SubmittedAt ?? a.StartedAt
            })
            .ToListAsync(cancellationToken);

        var practiceAiGrades = await uow.GradingResults.Query().AsNoTracking()
            .Where(g => !g.IsDeleted
                && g.PracticeSubmission != null
                && !g.PracticeSubmission.IsDeleted
                && g.PracticeSubmission.UserId == learnerId
                && (g.PracticeSubmission.Type == SubmissionType.Writing || g.PracticeSubmission.Type == SubmissionType.Speaking))
            .Select(g => new
            {
                Type = g.PracticeSubmission!.Type,
                g.OverallScore,
                At = g.GradedAt
            })
            .ToListAsync(cancellationToken);

        var preCourseBuckets = new Dictionary<string, List<SkillSample>>(StringComparer.OrdinalIgnoreCase);
        var parallelSelfStudyBuckets = new Dictionary<string, List<SkillSample>>(StringComparer.OrdinalIgnoreCase);
        var legacySamples = new Dictionary<string, List<SkillSample>>(StringComparer.OrdinalIgnoreCase);

        void AddBucketSample(
            Dictionary<string, List<SkillSample>> target,
            string key,
            string? fallbackName,
            double score,
            DateTime atUtc,
            string source)
        {
            if (!skillDescriptors.ContainsKey(key))
                skillDescriptors[key] = new SkillDescriptor(key, SkillLabelVi(key, fallbackName));
            if (!target.TryGetValue(key, out var list))
                target[key] = list = [];
            list.Add(new SkillSample(score, atUtc, source));

            if (!legacySamples.TryGetValue(key, out var leg))
                legacySamples[key] = leg = [];
            leg.Add(new SkillSample(score, atUtc, source));
        }

        foreach (var m in nonPlacementMockSections)
        {
            var atUtc = BusinessDateHelper.EnsureUtc(m.At);
            var key = NormalizeSkillKey(m.SkillId, m.SkillCode, m.SkillName ?? m.SectionTitle);
            if (!hasClassStarted || atUtc < baselineCutoffUtc)
                AddBucketSample(preCourseBuckets, key, m.SkillName ?? m.SectionTitle, m.ScorePercent, atUtc, "MockTest");
            else
                AddBucketSample(parallelSelfStudyBuckets, key, m.SkillName ?? m.SectionTitle, m.ScorePercent, atUtc, "MockTest");
        }

        foreach (var p in nonHomeworkPracticeAttempts)
        {
            var atUtc = BusinessDateHelper.EnsureUtc(p.At);
            var key = NormalizeSkillKey(p.SkillId, p.SkillCode, p.SkillName);
            if (!hasClassStarted || atUtc < baselineCutoffUtc)
                AddBucketSample(preCourseBuckets, key, p.SkillName, p.Score, atUtc, "PracticeExercise");
            else
                AddBucketSample(parallelSelfStudyBuckets, key, p.SkillName, p.Score, atUtc, "PracticeExercise");
        }

        foreach (var g in practiceAiGrades)
        {
            var atUtc = BusinessDateHelper.EnsureUtc(g.At);
            var key = g.Type == SubmissionType.Speaking ? "speaking" : "writing";
            var clamped = Math.Clamp(g.OverallScore, 0, 100);
            if (!hasClassStarted || atUtc < baselineCutoffUtc)
                AddBucketSample(preCourseBuckets, key, SkillLabelVi(key), clamped, atUtc, "AiPracticeGrading");
            else
                AddBucketSample(parallelSelfStudyBuckets, key, SkillLabelVi(key), clamped, atUtc, "AiPracticeGrading");
        }

        var preCoursePracticeSkills = preCourseBuckets
            .Select(kv => new PreCoursePracticeSkillResponse(
                kv.Key,
                SkillLabelVi(kv.Key, skillDescriptors.GetValueOrDefault(kv.Key)?.Label),
                Math.Round(kv.Value.Average(s => s.Score), 1),
                kv.Value.Count,
                kv.Value.Max(s => s.At),
                string.Join(",", kv.Value.Select(s => s.Source).Distinct().OrderBy(x => x))))
            .OrderByDescending(x => x.SampleCount)
            .ThenBy(x => x.SkillLabel)
            .ToList();

        var parallelSelfStudySkills = hasClassStarted
            ? parallelSelfStudyBuckets
                .Select(kv => new ParallelSelfStudySkillResponse(
                    kv.Key,
                    SkillLabelVi(kv.Key, skillDescriptors.GetValueOrDefault(kv.Key)?.Label),
                    Math.Round(kv.Value.Average(s => s.Score), 1),
                    kv.Value.Count,
                    kv.Value.Max(s => s.At),
                    string.Join(",", kv.Value.Select(s => s.Source).Distinct().OrderBy(x => x))))
                .OrderByDescending(x => x.SampleCount)
                .ThenBy(x => x.SkillLabel)
                .ToList()
            : [];

        // 5. B — Strictly Cohort-Scoped In-Class Competency & Assessments.
        var courseHw = (learnerEnrollment?.Homework ?? []).ToList();
        var cohortAssignmentsQuery = uow.HomeworkAssignments.Query().AsNoTracking()
            .Where(h => !h.IsDeleted && h.LearnerId == learnerId);
        cohortAssignmentsQuery = cohortId.HasValue
            ? cohortAssignmentsQuery.Where(h => h.CohortId == cohortId.Value && (h.StudyCourseId == enrollmentId || h.StudyCourseId == null))
            : cohortAssignmentsQuery.Where(h => !h.CohortId.HasValue && (h.StudyCourseId == enrollmentId || h.StudyCourseId == null));

        var hwRows = await cohortAssignmentsQuery
            .Include(h => h.HomeworkAssignmentLessons).ThenInclude(l => l.Lesson).ThenInclude(ls => ls.LanguageSkill)
            .Include(h => h.Exercises).ThenInclude(e => e.PracticeExercise).ThenInclude(pe => pe.LanguageSkill)
            .Include(h => h.Submission).ThenInclude(s => s!.GradingResult)
            .AsSplitQuery()
            .OrderByDescending(h => h.PublishedFeedbackAtUtc ?? h.CreatedDate)
            .ToListAsync(cancellationToken);

        var hwAssignmentIds = hwRows.Select(h => h.Id).ToArray();
        var hwPracticeAttempts = hwAssignmentIds.Length == 0
            ? []
            : await uow.PracticeExerciseAttempts.Query().AsNoTracking()
                .Where(a => !a.IsDeleted
                    && a.UserId == learnerId
                    && a.HomeworkAssignmentId.HasValue
                    && hwAssignmentIds.Contains(a.HomeworkAssignmentId.Value)
                    && a.IsCompleted
                    && a.ScorePercent.HasValue)
                .Select(a => new
                {
                    AssignmentId = a.HomeworkAssignmentId!.Value,
                    SkillId = (int?)a.PracticeExercise.LanguageSkillId,
                    SkillCode = a.PracticeExercise.LanguageSkill.Code,
                    SkillName = a.PracticeExercise.LanguageSkill.Name,
                    Score = a.ScorePercent!.Value,
                    At = a.SubmittedAt ?? a.StartedAt
                })
                .ToListAsync(cancellationToken);

        var assignedCount = courseHw.Count + hwRows.Count;
        var submittedCount = courseHw.Count(h => h.SubmittedAt.HasValue)
            + hwRows.Count(h => h.Submission != null && !h.Submission.IsDeleted);
        var onTimeCount = courseHw.Count(h => h.SubmittedAt.HasValue && h.SubmittedAt.Value <= h.DueAt)
            + hwRows.Count(h => h.Submission != null && !h.Submission.IsDeleted
                && (!h.DueAt.HasValue || h.Submission.SubmittedAt <= h.DueAt.Value));
        var passedCount = courseHw.Count(h => h.Passed == true) + hwRows.Count(h => h.Passed == true);

        var activityDates = courseHw
            .SelectMany(h => new DateTime?[] { h.SubmittedAt, h.ReviewedAt })
            .Concat(hwRows.SelectMany(h => new DateTime?[]
            {
                h.Submission != null && !h.Submission.IsDeleted ? h.Submission.SubmittedAt : null,
                h.PublishedFeedbackAtUtc
            }))
            .Where(d => d.HasValue)
            .Select(d => BusinessDateHelper.EnsureUtc(d!.Value))
            .ToList();
        DateTime? latestHomeworkAt = activityDates.Count > 0 ? activityDates.Max() : null;
        double? onTimePercent = assignedCount > 0 ? Math.Round(onTimeCount * 100d / assignedCount, 2) : null;

        var hwStats = new LearnerCohortHomeworkStatsResponse(
            assignedCount, submittedCount, onTimeCount, passedCount, onTimePercent, latestHomeworkAt);

        // In-class attendance rate (marked sessions only; null if 0 sessions recorded).
        var sessionLatestByNum = (learnerEnrollment?.Sessions ?? [])
            .Where(s => s.Status is "Present" or "Absent" or "ExcusedAbsent")
            .GroupBy(s => s.Number)
            .Select(g => g.OrderByDescending(x => x.Attempt).First())
            .ToList();
        var attendanceMarkedCount = sessionLatestByNum.Count;
        var attendancePresentCount = sessionLatestByNum.Count(s => s.Status == "Present");
        double? attendanceRatePercent = attendanceMarkedCount > 0
            ? Math.Round(attendancePresentCount * 100d / attendanceMarkedCount, 1)
            : null;

        var inClassBuckets = new Dictionary<string, List<SkillSample>>(StringComparer.OrdinalIgnoreCase);
        var inClassAssessments = new List<InClassAssessmentEventResponse>();
        var inClassTrendPoints = new List<CompetencyTrendPointResponse>();
        var assignmentTitleById = hwRows.ToDictionary(h => h.Id, h => h.Title);

        if (hasClassStarted)
        {
            foreach (var ha in hwPracticeAttempts)
            {
                var atUtc = BusinessDateHelper.EnsureUtc(ha.At);
                var key = NormalizeSkillKey(ha.SkillId, ha.SkillCode, ha.SkillName);
                var labelVi = SkillLabelVi(key, ha.SkillName);
                if (!skillDescriptors.ContainsKey(key))
                    skillDescriptors[key] = new SkillDescriptor(key, labelVi);
                if (!inClassBuckets.TryGetValue(key, out var list))
                    inClassBuckets[key] = list = [];
                list.Add(new SkillSample(ha.Score, atUtc, "CohortHomeworkExercise"));

                inClassTrendPoints.Add(new CompetencyTrendPointResponse(
                    atUtc,
                    assignmentTitleById.GetValueOrDefault(ha.AssignmentId) ?? "Bài tập luyện tập tại lớp",
                    "CohortHomeworkExercise",
                    "InClassHomeworkExercise",
                    key,
                    labelVi,
                    Math.Round(ha.Score, 1)));
            }

            foreach (var h in hwRows)
            {
                var skillKeysForAssignment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var l in h.HomeworkAssignmentLessons)
                {
                    if (l.Lesson?.LanguageSkill is not null)
                    {
                        var key = NormalizeSkillKey(l.Lesson.LanguageSkillId, l.Lesson.LanguageSkill.Code, l.Lesson.LanguageSkill.Name);
                        skillKeysForAssignment[key] = SkillLabelVi(key, l.Lesson.LanguageSkill.Name);
                    }
                }
                foreach (var e in h.Exercises)
                {
                    if (e.PracticeExercise?.LanguageSkill is not null)
                    {
                        var key = NormalizeSkillKey(e.PracticeExercise.LanguageSkillId, e.PracticeExercise.LanguageSkill.Code, e.PracticeExercise.LanguageSkill.Name);
                        skillKeysForAssignment[key] = SkillLabelVi(key, e.PracticeExercise.LanguageSkill.Name);
                    }
                }

                var sub = h.Submission != null && !h.Submission.IsDeleted ? h.Submission : null;
                var validGrade = sub?.GradingResult != null && !sub.GradingResult.IsDeleted ? sub.GradingResult : null;
                var exerciseScores = hwPracticeAttempts.Where(a => a.AssignmentId == h.Id).ToList();
                double? eventScore = h.MentorScore
                    ?? (validGrade is not null ? Math.Round(Math.Clamp(validGrade.OverallScore, 0, 100), 1) : (double?)null)
                    ?? (exerciseScores.Count > 0 ? Math.Round(exerciseScores.Average(x => x.Score), 1) : (double?)null);

                var gradedAt = h.PublishedFeedbackAtUtc ?? validGrade?.GradedAt;
                var mentorNote = TryExtractMentorFeedbackText(h.MentorFeedback, validGrade?.FeedbackJson);

                // Attribute assignment-level mentor score strictly to assignment skills when no individual exercise score already logged for that skill.
                if (eventScore.HasValue && gradedAt.HasValue && exerciseScores.Count == 0)
                {
                    var gradedAtUtc = BusinessDateHelper.EnsureUtc(gradedAt.Value);
                    var roundedEventScore = Math.Round(eventScore.Value, 1);
                    if (skillKeysForAssignment.Count > 0)
                    {
                        foreach (var sk in skillKeysForAssignment)
                        {
                            if (!skillDescriptors.ContainsKey(sk.Key))
                                skillDescriptors[sk.Key] = new SkillDescriptor(sk.Key, sk.Value);
                            if (!inClassBuckets.TryGetValue(sk.Key, out var list))
                                inClassBuckets[sk.Key] = list = [];
                            list.Add(new SkillSample(roundedEventScore, gradedAtUtc, "CohortHomework"));

                            inClassTrendPoints.Add(new CompetencyTrendPointResponse(
                                gradedAtUtc,
                                h.Title,
                                "CohortHomework",
                                "InClassCohortHomework",
                                sk.Key,
                                sk.Value,
                                roundedEventScore));
                        }
                    }
                    else
                    {
                        inClassTrendPoints.Add(new CompetencyTrendPointResponse(
                            gradedAtUtc,
                            h.Title,
                            "CohortHomework",
                            "InClassCohortHomework",
                            null,
                            null,
                            roundedEventScore));
                    }
                }

                inClassAssessments.Add(new InClassAssessmentEventResponse(
                    h.Id,
                    h.Title,
                    h.AssignmentScope == "CohortIndividual" ? "Bài giao riêng" : "Bài tập lớp",
                    h.DueAt.HasValue ? BusinessDateHelper.EnsureUtc(h.DueAt.Value) : null,
                    sub is not null ? BusinessDateHelper.EnsureUtc(sub.SubmittedAt) : null,
                    gradedAt.HasValue ? BusinessDateHelper.EnsureUtc(gradedAt.Value) : null,
                    eventScore,
                    h.Passed,
                    skillKeysForAssignment.Values.Distinct().ToList(),
                    h.PublishedFeedbackAtUtc.HasValue ? mentorNote : null));
            }
        }

        var inClassSkills = inClassBuckets
            .Select(kv => new InClassSkillItemResponse(
                kv.Key,
                SkillLabelVi(kv.Key, skillDescriptors.GetValueOrDefault(kv.Key)?.Label),
                Math.Round(kv.Value.Average(s => s.Score), 1),
                kv.Value.Count,
                kv.Value.Max(s => s.At),
                kv.Value.Select(s => s.Source).Distinct().ToList()))
            .OrderBy(x => SkillSortOrder(x.SkillCode))
            .ThenBy(x => x.SkillLabel)
            .ToList();

        var scoredAssessments = inClassAssessments.Where(a => a.ScorePercent.HasValue).ToList();
        var gradedHomeworkCount = scoredAssessments.Count;
        double? averageGradedHomeworkScorePercent = gradedHomeworkCount > 0
            ? Math.Round(scoredAssessments.Average(a => a.ScorePercent!.Value), 1)
            : null;
        var scoredAssessmentCount = inClassBuckets.Values.Sum(v => v.Count);
        var inClassStrengths = inClassSkills.Where(s => s.AverageScorePercent >= 80d).Select(s => s.SkillLabel).ToList();
        var inClassFocusAreas = inClassSkills.Where(s => s.AverageScorePercent < 60d).Select(s => s.SkillLabel).ToList();
        var orderedInClassTrend = inClassTrendPoints.OrderBy(p => p.DateUtc).ToList();

        var inClassHasData = hasClassStarted && (assignedCount > 0 || inClassSkills.Count > 0 || attendanceMarkedCount > 0);
        var inClassCompetency = new InClassCompetencyResponse(
            hasClassStarted,
            inClassHasData,
            hwStats,
            inClassSkills,
            inClassAssessments,
            attendancePresentCount,
            attendanceMarkedCount,
            attendanceRatePercent,
            completedSessionsCount,
            totalScheduledSessionsCount,
            gradedHomeworkCount,
            averageGradedHomeworkScorePercent,
            scoredAssessmentCount,
            inClassStrengths,
            inClassFocusAreas,
            orderedInClassTrend);

        // 6. C — Compatible Scale-Matched Comparison (only same JLPT level & official JLPT section scale).
        CompetencyComparisonResponse comparison;
        if (!hasClassStarted)
        {
            comparison = new CompetencyComparisonResponse(
                false, "Lớp học chưa bắt đầu; so sánh tiến bộ sẽ khả dụng khi có dữ liệu đánh giá cùng cấp độ trong quá trình học.",
                targetLevelCode, null, baselinePlacement?.AssessedAtUtc, null, baselinePlacement?.OverallScorePercent, null, null, [], [], []);
        }
        else if (baselinePlacement is null || baselineCompatibility.Status != "COMPATIBLE")
        {
            comparison = new CompetencyComparisonResponse(
                false,
                baselinePlacement is null
                    ? "Học viên chưa có bài đánh giá năng lực đầu vào tương thích để đối chiếu."
                    : baselineCompatibility.Reason ?? "Bài đánh giá đầu vào chưa khớp cấp độ mục tiêu của lớp để so sánh trực tiếp.",
                targetLevelCode, null, baselinePlacement?.AssessedAtUtc, null, baselinePlacement?.OverallScorePercent, null, null, [], [], []);
        }
        else
        {
            var postStartPlacements = allPlacements
                .Where(a => a.IsCompleted && a.TotalScore.HasValue && BusinessDateHelper.EnsureUtc(a.SubmittedAt ?? a.StartedAt) >= baselineCutoffUtc)
                .ToList();
            var postCompat = PracticeTestService.EvaluatePlacementCompatibility(postStartPlacements, targetLevelId, targetLevelCode);

            var currentByJlptSkill = new Dictionary<string, (double ScorePercent, string Label)>(StringComparer.OrdinalIgnoreCase);
            DateTime? currentAssessedAtUtc = null;
            double? currentOverallPercent = null;
            string? assessmentScale = null;

            if (postCompat.CompatibleAttempt is not null && postCompat.CompatibleResult is not null && !string.IsNullOrWhiteSpace(targetLevelCode))
            {
                var postAudio = PracticeTestService.TryGetSkillEvidenceFromSnapshot(postCompat.CompatibleAttempt.QuestionSnapshotJson);
                var postAssessed = JlptSkillAssessmentPolicy.Assess(postCompat.CompatibleResult, targetLevelCode, postAudio);
                foreach (var sec in postAssessed.Where(s => s.HasData && s.ScorePercent.HasValue))
                    currentByJlptSkill[sec.SkillCode] = (sec.ScorePercent!.Value, SkillLabelVi(sec.SkillCode, sec.SectionTitle));
                currentAssessedAtUtc = BusinessDateHelper.EnsureUtc(postCompat.CompatibleAttempt.SubmittedAt ?? postCompat.CompatibleAttempt.StartedAt);
                var validPostSections = postAssessed.Where(s => s.HasData && s.TotalCount > 0).ToList();
                currentOverallPercent = postAudio is not null && validPostSections.Sum(s => s.TotalCount) > 0
                    ? Math.Round(validPostSections.Sum(s => s.CorrectCount) * 100d / validPostSections.Sum(s => s.TotalCount), 1)
                    : Math.Round(postCompat.CompatibleResult.TotalScore, 1);
                assessmentScale = $"Đánh giá JLPT {targetLevelCode}";
            }
            else if (targetLevelId.HasValue)
            {
                var latestTargetMockId = nonPlacementMockSections
                    .Where(m => m.CertificateLevelId == targetLevelId.Value
                        && m.ExamKind == ExamKind.FullMockTest
                        && BusinessDateHelper.EnsureUtc(m.At) >= baselineCutoffUtc)
                    .OrderByDescending(m => m.At)
                    .ThenByDescending(m => m.MockTestAttemptId)
                    .Select(m => (int?)m.MockTestAttemptId)
                    .FirstOrDefault();

                if (latestTargetMockId.HasValue)
                {
                    var rowsForAttempt = nonPlacementMockSections.Where(m => m.MockTestAttemptId == latestTargetMockId.Value).ToList();
                    foreach (var grp in rowsForAttempt
                        .Select(r => new { Row = r, Skill = JlptSkillAssessmentPolicy.Classify(string.IsNullOrWhiteSpace(r.SkillName) ? r.SectionTitle : r.SkillName) })
                        .Where(x => x.Skill.HasValue && x.Row.TotalCount > 0)
                        .GroupBy(x => x.Skill!.Value))
                    {
                        var correct = grp.Sum(x => x.Row.CorrectCount);
                        var total = grp.Sum(x => x.Row.TotalCount);
                        if (total <= 0) continue;
                        var code = grp.Key.ToString().ToLowerInvariant();
                        currentByJlptSkill[code] = (Math.Round(correct * 100d / total, 1), SkillLabelVi(code));
                    }
                    var head = rowsForAttempt[0];
                    currentAssessedAtUtc = BusinessDateHelper.EnsureUtc(head.At);
                    var totalQ = rowsForAttempt.Sum(r => r.TotalCount);
                    var totalC = rowsForAttempt.Sum(r => r.CorrectCount);
                    currentOverallPercent = totalQ > 0
                        ? Math.Round(totalC * 100d / totalQ, 1)
                        : (head.AttemptTotalScore.HasValue ? Math.Round(head.AttemptTotalScore.Value, 1) : null);
                    assessmentScale = $"Đề thi thử chuẩn JLPT {targetLevelCode}";
                }
            }

            var comparisons = new List<SkillComparisonItemResponse>();
            foreach (var b in baselineByJlptSkill)
            {
                if (!currentByJlptSkill.TryGetValue(b.Key, out var cur)) continue;
                var delta = Math.Round(cur.ScorePercent - b.Value.ScorePercent, 1);
                var status = delta >= 2d ? "Improved" : delta <= -2d ? "Declined" : "Stable";
                comparisons.Add(new SkillComparisonItemResponse(
                    b.Key,
                    SkillLabelVi(b.Key, cur.Label),
                    Math.Round(b.Value.ScorePercent, 1),
                    Math.Round(cur.ScorePercent, 1),
                    delta,
                    status));
            }

            comparisons = comparisons
                .OrderBy(c => SkillSortOrder(c.SkillCode))
                .ThenBy(c => c.SkillLabel)
                .ToList();

            if (comparisons.Count == 0 || !currentAssessedAtUtc.HasValue)
            {
                comparison = new CompetencyComparisonResponse(
                    false,
                    $"Chưa có bài kiểm tra hoặc thi thử JLPT {targetLevelCode} sau ngày khai giảng cùng thang đo để đối chiếu với năng lực đầu vào.",
                    targetLevelCode, null, baselinePlacement.AssessedAtUtc, null, baselinePlacement.OverallScorePercent, null, null, [], [], []);
            }
            else
            {
                var unmatchedSkills = new List<SkillComparisonGapResponse>();
                foreach (var bs in baselinePlacement.Skills)
                {
                    if (comparisons.Any(c => string.Equals(c.SkillCode, bs.SkillCode, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    currentByJlptSkill.TryGetValue(bs.SkillCode, out var curSkill);
                    var hasBase = bs.ScorePercent.HasValue && bs.Status != "Unassessed";
                    var hasCur = currentByJlptSkill.ContainsKey(bs.SkillCode);
                    var reason = (!hasBase && !hasCur)
                        ? "Chưa được đánh giá ở cả hai mốc"
                        : (!hasBase ? "Chưa có dữ liệu đầu vào" : "Chưa có dữ liệu hiện tại");
                    unmatchedSkills.Add(new SkillComparisonGapResponse(
                        bs.SkillCode,
                        bs.SkillLabel,
                        hasBase ? Math.Round(bs.ScorePercent!.Value, 1) : null,
                        hasCur ? Math.Round(curSkill.ScorePercent, 1) : null,
                        reason));
                }

                // Chronological trend points strictly on the same compatible target-level JLPT scale.
                var comparisonTrendPoints = new List<CompetencyTrendPointResponse>();
                var jlptScaleKey = !string.IsNullOrWhiteSpace(targetLevelCode) ? $"JLPT_{targetLevelCode}" : "JLPT_COMPATIBLE";
                comparisonTrendPoints.Add(new CompetencyTrendPointResponse(
                    baselinePlacement.AssessedAtUtc,
                    "Đầu vào",
                    "Placement",
                    jlptScaleKey,
                    null,
                    null,
                    baselinePlacement.OverallScorePercent,
                    baselinePlacement.CorrectCount,
                    baselinePlacement.TotalQuestions));
                foreach (var bs in baselinePlacement.Skills.Where(s => s.ScorePercent.HasValue && s.Status != "Unassessed"))
                {
                    comparisonTrendPoints.Add(new CompetencyTrendPointResponse(
                        baselinePlacement.AssessedAtUtc,
                        "Đầu vào",
                        "Placement",
                        jlptScaleKey,
                        bs.SkillCode,
                        bs.SkillLabel,
                        Math.Round(bs.ScorePercent!.Value, 1),
                        bs.CorrectCount,
                        bs.TotalCount));
                }

                foreach (var postAtt in postStartPlacements.OrderBy(a => a.SubmittedAt ?? a.StartedAt))
                {
                    var check = PracticeTestService.EvaluatePlacementCompatibility([postAtt], targetLevelId, targetLevelCode);
                    if (check.Status != "COMPATIBLE" || check.CompatibleResult is null || string.IsNullOrWhiteSpace(targetLevelCode))
                        continue;
                    var postAt = BusinessDateHelper.EnsureUtc(postAtt.SubmittedAt ?? postAtt.StartedAt);
                    var postEv = PracticeTestService.TryGetSkillEvidenceFromSnapshot(postAtt.QuestionSnapshotJson);
                    var postSkills = JlptSkillAssessmentPolicy.Assess(check.CompatibleResult, targetLevelCode, postEv)
                        .Where(s => s.HasData && s.TotalCount > 0 && s.ScorePercent.HasValue)
                        .ToList();
                    var postCorrect = postSkills.Sum(s => s.CorrectCount);
                    var postTotal = postSkills.Sum(s => s.TotalCount);
                    var postOverall = postEv is not null && postTotal > 0
                        ? Math.Round(postCorrect * 100d / postTotal, 1)
                        : Math.Round(check.CompatibleResult.TotalScore, 1);

                    comparisonTrendPoints.Add(new CompetencyTrendPointResponse(
                        postAt,
                        $"Đánh giá {targetLevelCode}",
                        "Placement",
                        jlptScaleKey,
                        null,
                        null,
                        postOverall,
                        postEv is not null ? postCorrect : check.CompatibleResult.CorrectCount,
                        postEv is not null ? postTotal : check.CompatibleResult.TotalQuestions));

                    foreach (var ps in postSkills)
                    {
                        comparisonTrendPoints.Add(new CompetencyTrendPointResponse(
                            postAt,
                            $"Đánh giá {targetLevelCode}",
                            "Placement",
                            jlptScaleKey,
                            ps.SkillCode,
                            SkillLabelVi(ps.SkillCode, ps.SectionTitle),
                            Math.Round(ps.ScorePercent!.Value, 1),
                            ps.CorrectCount,
                            ps.TotalCount));
                    }
                }

                if (targetLevelId.HasValue)
                {
                    var mockGroups = nonPlacementMockSections
                        .Where(m => m.CertificateLevelId == targetLevelId.Value
                            && m.ExamKind == ExamKind.FullMockTest
                            && BusinessDateHelper.EnsureUtc(m.At) >= baselineCutoffUtc)
                        .GroupBy(m => m.MockTestAttemptId)
                        .Select(g => new { AttemptId = g.Key, At = BusinessDateHelper.EnsureUtc(g.First().At), Rows = g.ToList() })
                        .OrderBy(g => g.At);

                    foreach (var mg in mockGroups)
                    {
                        var totalQ = mg.Rows.Sum(r => r.TotalCount);
                        var totalC = mg.Rows.Sum(r => r.CorrectCount);
                        if (totalQ <= 0) continue;

                        comparisonTrendPoints.Add(new CompetencyTrendPointResponse(
                            mg.At,
                            $"Thi thử {targetLevelCode}",
                            "MockTest",
                            jlptScaleKey,
                            null,
                            null,
                            Math.Round(totalC * 100d / totalQ, 1),
                            totalC,
                            totalQ));

                        foreach (var sg in mg.Rows
                            .Select(r => new { Row = r, Skill = JlptSkillAssessmentPolicy.Classify(string.IsNullOrWhiteSpace(r.SkillName) ? r.SectionTitle : r.SkillName) })
                            .Where(x => x.Skill.HasValue && x.Row.TotalCount > 0)
                            .GroupBy(x => x.Skill!.Value))
                        {
                            var sc = sg.Sum(x => x.Row.CorrectCount);
                            var st = sg.Sum(x => x.Row.TotalCount);
                            if (st <= 0) continue;
                            var skCode = sg.Key.ToString().ToLowerInvariant();
                            comparisonTrendPoints.Add(new CompetencyTrendPointResponse(
                                mg.At,
                                $"Thi thử {targetLevelCode}",
                                "MockTest",
                                jlptScaleKey,
                                skCode,
                                SkillLabelVi(skCode),
                                Math.Round(sc * 100d / st, 1),
                                sc,
                                st));
                        }
                    }
                }

                double? overallDelta = currentOverallPercent.HasValue
                    ? Math.Round(currentOverallPercent.Value - baselinePlacement.OverallScorePercent, 1)
                    : null;
                var improved = comparisons.Where(x => x.Status == "Improved").Select(x => x.SkillLabel).ToList();
                var focusItems = comparisons.Where(x => x.CurrentScorePercent < 60d || x.Status == "Declined").ToList();
                var focus = focusItems.Select(x => x.SkillLabel).ToList();
                var topImproved = comparisons.Where(x => x.DeltaPercent > 0).OrderByDescending(x => x.DeltaPercent).FirstOrDefault();
                var priorityFocus = focusItems.OrderBy(x => x.CurrentScorePercent).ThenBy(x => x.DeltaPercent).FirstOrDefault();
                var currentStrengths = comparisons.Where(x => x.CurrentScorePercent >= 80d).Select(x => x.SkillLabel).ToList();
                var nextRecommendations = baselinePlacement.Skills
                    .Where(bs => focusItems.Any(fi => string.Equals(fi.SkillCode, bs.SkillCode, StringComparison.OrdinalIgnoreCase))
                        && !string.IsNullOrWhiteSpace(bs.Recommendation))
                    .Select(bs => bs.Recommendation!)
                    .Distinct()
                    .ToList();

                comparison = new CompetencyComparisonResponse(
                    true,
                    null,
                    targetLevelCode,
                    assessmentScale,
                    baselinePlacement.AssessedAtUtc,
                    currentAssessedAtUtc,
                    baselinePlacement.OverallScorePercent,
                    currentOverallPercent,
                    overallDelta,
                    comparisons,
                    improved,
                    focus,
                    comparisonTrendPoints.OrderBy(p => p.DateUtc).ToList(),
                    unmatchedSkills,
                    topImproved?.SkillLabel,
                    topImproved?.DeltaPercent,
                    priorityFocus?.SkillLabel,
                    currentStrengths,
                    nextRecommendations);
            }
        }

        // Legacy compatibility list preserving dynamic skill rows from DB.
        var skillMetrics = skillDescriptors.Values.Select(desc =>
        {
            legacySamples.TryGetValue(desc.Code, out var list);
            if (list is null || list.Count == 0)
                return new LearnerSkillMetricResponse(desc.Label, null, 0, null, null, desc.Label);
            var avg = Math.Round(list.Average(x => x.Score), 2);
            var latestAt = list.Max(x => x.At);
            var source = string.Join(",", list.Select(x => x.Source).Distinct().OrderBy(x => x));
            return new LearnerSkillMetricResponse(desc.Label, avg, list.Count, latestAt, source, desc.Label);
        }).ToList();

        return ApiResponse<MentorLearnerSkillProfileResponse>.Success(new MentorLearnerSkillProfileResponse(
            learner.Id,
            $"{learner.FirstName} {learner.LastName}".Trim(),
            cohortId,
            scopeType,
            enrollmentId,
            baselinePlacement?.AssessedJlptLevel,
            skillMetrics,
            hwStats,
            cohortContext,
            baselinePlacement,
            preCoursePracticeSkills,
            inClassCompetency,
            comparison,
            parallelSelfStudySkills));
    }
}
