using System.Text.Json.Serialization;

namespace RikiPath.Application.Responses.Profile;

/// <summary>
/// Authoritative backend state for Learner onboarding, learning goal, placement assessment,
/// roadmap eligibility, AI roadmap generation status, active roadmap selection, and scoped progress.
/// </summary>
public sealed class LearnerSetupStateResponse
{
    // Step 1 & 2 — Profile & Confirmed Goal
    public bool OnboardingCompleted { get; set; }
    public bool HasConfirmedGoal { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? CertificateTypeId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? CertificateTypeCode { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? TargetJlptLevelId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? TargetJlptLevelName { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? StartingJlptLevelId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? StartingJlptLevelName { get; set; }
    public int DailyStudyMinutes { get; set; }
    public int? DailyStudyMinutesMin { get; set; }
    public int? DailyStudyMinutesMax { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public DateTime? TargetExamDate { get; set; }
    public bool HasValidExamDate { get; set; }
    public int LearningGoalVersion { get; set; }

    // Step 3 — Placement Assessment (kept strictly separate from roadmap eligibility and AI generation)
    /// <summary>NOT_STARTED | IN_PROGRESS | PENDING_SUBMISSION | EXPLICITLY_SKIPPED | COMPLETED</summary>
    public string PlacementStatus { get; set; } = "NOT_STARTED";
    /// <summary>NOT_TAKEN | SKIPPED | COMPLETED</summary>
    public string PlacementDecision { get; set; } = "NOT_TAKEN";
    public bool HasActivePlacementAttempt { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? ActivePlacementAttemptId { get; set; }
    public bool HasPendingPlacementSubmission { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? PendingSubmissionAttemptId { get; set; }
    public bool HasValidCompletedPlacement { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? LatestValidPlacementAttemptId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public double? LatestPlacementScorePercent { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? AssessedJlptLevelId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? AssessedJlptLevelName { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? LatestPlacementAssessmentLevelId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? LatestPlacementAssessmentLevelName { get; set; }
    public bool LatestPlacementIsComprehensive { get; set; }
    /// <summary>NONE | GOAL_NOT_SET | COMPATIBLE | INSUFFICIENT_FOR_TARGET</summary>
    public string PlacementCompatibilityStatus { get; set; } = "NONE";
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? PlacementCompatibilityReason { get; set; }
    public bool IsPlacementExplicitlySkipped { get; set; }

    // Quota & Start Eligibility (never fabricated as 0 when uninitialized)
    public bool PlacementQuotaInitialized { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? PlacementAttemptLimit { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? PlacementAttemptsStarted { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? PlacementAttemptsRemaining { get; set; }
    public bool PlacementCanStart { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? PlacementCannotStartReason { get; set; }

    // Step 4 — Roadmap Eligibility, Generation State & Active Selection (separated)
    public bool StandardRoadmapEligible { get; set; }
    public bool AiRoadmapEligible { get; set; }
    public bool HasInitializedRoadmap { get; set; }
    /// <summary>NOT_ELIGIBLE | NOT_GENERATED | DRAFT_PREVIEW | READY</summary>
    public string AiRoadmapState { get; set; } = "NOT_ELIGIBLE";
    public bool HasActiveAiRoadmap { get; set; }
    public bool ActiveAiRoadmapMatchesGoal { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? ActiveAiRoadmapId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? ActiveAiRoadmapTargetLevelId { get; set; }
    public bool HasDraftAiRoadmapPreview { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? DraftAiRoadmapPreviewId { get; set; }
    /// <summary>NONE | STANDARD | AI_PERSONALIZED</summary>
    public string ActiveRoadmapType { get; set; } = "NONE";
    public bool HasAnyValidRoadmap { get; set; }

    // Step & Next Action
    /// <summary>GOAL_REQUIRED | PLACEMENT_REQUIRED | PLACEMENT_IN_PROGRESS | STANDARD_READY | AI_READY_TO_GENERATE | ROADMAP_ACTIVE</summary>
    public string SetupStage { get; set; } = "GOAL_REQUIRED";
    /// <summary>COMPLETE_ONBOARDING | CHOOSE_PLACEMENT_OR_SKIP | RESUME_PLACEMENT | SUBMIT_EXPIRED_PLACEMENT | OPEN_STANDARD_ROADMAP | GENERATE_AI_ROADMAP | CONTINUE_ROADMAP</summary>
    public string NextAction { get; set; } = "COMPLETE_ONBOARDING";
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? BlockingErrorCode { get; set; }

    // Authoritative Roadmap-Scoped Progress (null when no valid roadmap exists)
    public bool HasValidRoadmapProgress { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? RoadmapCompletedLessons { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? RoadmapTotalLessons { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public double? RoadmapCompletionPercent { get; set; }
}
