namespace RikiPath.Application.Responses.MentorMeetings;

public sealed record LearnerSkillMetricResponse(
    string Skill,
    double? AverageScorePercent,
    int SampleCount,
    DateTime? LatestAssessedAtUtc,
    string? DataSource,
    string? SkillLabel = null);

public sealed record LearnerCohortHomeworkStatsResponse(
    int AssignedCount,
    int SubmittedCount,
    int OnTimeCount,
    int PassedCount,
    double? OnTimePercent,
    DateTime? LatestActivityAtUtc);

public sealed record LearnerCompetencyCohortContextResponse(
    int CohortId,
    string CohortName,
    string? TargetLevelCode,
    string CohortStatus,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime FirstSessionStartUtc,
    DateTime BaselineCutoffUtc,
    int EnrollmentId,
    string EnrollmentStatus,
    DateTime EnrolledAtUtc,
    string? LearnerAvatarUrl);

public sealed record BaselineSkillItemResponse(
    string SkillCode,
    string SkillLabel,
    double? ScorePercent,
    int CorrectCount,
    int TotalCount,
    string Priority,
    string Status,
    string? EstimatedLevelCode,
    string? Comment,
    string? Recommendation = null);

public sealed record BaselinePlacementSummaryResponse(
    int AttemptId,
    string? TargetLevelCode,
    string? AssessedJlptLevel,
    string? RecommendedTargetLevel,
    double OverallScorePercent,
    int CorrectCount,
    int TotalQuestions,
    DateTime AssessedAtUtc,
    string CompatibilityStatus,
    string? CompatibilityNote,
    List<BaselineSkillItemResponse> Skills,
    List<string> Strengths,
    List<string> FocusAreas,
    string AssessedLevelStatus = "Assessed",
    string? TestedLevelScope = null,
    int AssessedSkillCount = 0,
    int TotalSkillCount = 0,
    int ExcludedInvalidQuestionCount = 0);

public sealed record CompetencyTrendPointResponse(
    DateTime DateUtc,
    string Label,
    string SourceType,
    string ScaleKey,
    string? SkillCode,
    string? SkillLabel,
    double ScorePercent,
    int? CorrectCount = null,
    int? TotalCount = null);

public sealed record SkillComparisonGapResponse(
    string SkillCode,
    string SkillLabel,
    double? BaselineScorePercent,
    double? CurrentScorePercent,
    string Reason);

public sealed record PreCoursePracticeSkillResponse(
    string SkillCode,
    string SkillLabel,
    double AverageScorePercent,
    int SampleCount,
    DateTime LatestAtUtc,
    string DataSource);

public sealed record InClassSkillItemResponse(
    string SkillCode,
    string SkillLabel,
    double AverageScorePercent,
    int SampleCount,
    DateTime LatestAtUtc,
    List<string> DataSources);

public sealed record InClassAssessmentEventResponse(
    int Id,
    string Title,
    string Kind,
    DateTime? DueAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? GradedAtUtc,
    double? ScorePercent,
    bool? Passed,
    List<string> Skills,
    string? MentorFeedback);

public sealed record InClassCompetencyResponse(
    bool HasClassStarted,
    bool HasData,
    LearnerCohortHomeworkStatsResponse HomeworkStats,
    List<InClassSkillItemResponse> Skills,
    List<InClassAssessmentEventResponse> Assessments,
    int AttendancePresentCount,
    int AttendanceMarkedCount,
    double? AttendanceRatePercent,
    int CompletedSessionsCount = 0,
    int TotalScheduledSessionsCount = 0,
    int GradedHomeworkCount = 0,
    double? AverageGradedHomeworkScorePercent = null,
    int ScoredAssessmentCount = 0,
    List<string>? Strengths = null,
    List<string>? FocusAreas = null,
    List<CompetencyTrendPointResponse>? TrendPoints = null);

public sealed record SkillComparisonItemResponse(
    string SkillCode,
    string SkillLabel,
    double BaselineScorePercent,
    double CurrentScorePercent,
    double DeltaPercent,
    string Status);

public sealed record CompetencyComparisonResponse(
    bool IsComparable,
    string? ReasonIfNotComparable,
    string? TargetLevelCode,
    string? AssessmentScale,
    DateTime? BaselineAssessedAtUtc,
    DateTime? CurrentAssessedAtUtc,
    double? BaselineOverallPercent,
    double? CurrentOverallPercent,
    double? OverallDeltaPercent,
    List<SkillComparisonItemResponse> SkillComparisons,
    List<string> ImprovedSkills,
    List<string> FocusSkills,
    List<CompetencyTrendPointResponse>? TrendPoints = null,
    List<SkillComparisonGapResponse>? UnmatchedSkills = null,
    string? MostImprovedSkill = null,
    double? MostImprovedDeltaPercent = null,
    string? PriorityFocusSkill = null,
    List<string>? CurrentStrengths = null,
    List<string>? NextRecommendations = null);

public sealed record ParallelSelfStudySkillResponse(
    string SkillCode,
    string SkillLabel,
    double AverageScorePercent,
    int SampleCount,
    DateTime LatestAtUtc,
    string DataSource);

public sealed record MentorLearnerSkillProfileResponse(
    int LearnerId,
    string LearnerName,
    int? CohortId,
    string ScopeType,
    int? EnrollmentId,
    string? AssessedJlptLevel,
    List<LearnerSkillMetricResponse> Skills,
    LearnerCohortHomeworkStatsResponse HomeworkStats,
    LearnerCompetencyCohortContextResponse? CohortContext = null,
    BaselinePlacementSummaryResponse? BaselinePlacement = null,
    List<PreCoursePracticeSkillResponse>? PreCoursePracticeSkills = null,
    InClassCompetencyResponse? InClassCompetency = null,
    CompetencyComparisonResponse? Comparison = null,
    List<ParallelSelfStudySkillResponse>? ParallelSelfStudySkills = null);
