namespace RikiPath.Application.Responses.MentorMeetings;

public sealed record LearnerSkillMetricResponse(
    string Skill,
    double? AverageScorePercent,
    int SampleCount,
    DateTime? LatestAssessedAtUtc,
    string? DataSource);

public sealed record LearnerCohortHomeworkStatsResponse(
    int AssignedCount,
    int SubmittedCount,
    int OnTimeCount,
    int PassedCount,
    double? OnTimePercent,
    DateTime? LatestActivityAtUtc);

public sealed record MentorLearnerSkillProfileResponse(
    int LearnerId,
    string LearnerName,
    int? CohortId,
    string ScopeType,
    int? EnrollmentId,
    string? AssessedJlptLevel,
    List<LearnerSkillMetricResponse> Skills,
    LearnerCohortHomeworkStatsResponse HomeworkStats);
