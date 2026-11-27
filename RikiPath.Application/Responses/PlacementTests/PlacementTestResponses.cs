using System.Text.Json.Serialization;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.LearningPaths;

namespace RikiPath.Application.Responses.PlacementTests
{
    public sealed record PlacementAttemptAllowance(int Limit, int Started, int Remaining, int? PendingSubmissionAttemptId);

    public class PlacementTestStatusResponse
    {
        public string AttemptType { get; set; } = "placement";
        public string PlacementDecision { get; set; } = "NOT_TAKEN";
        public bool HasActiveAttempt { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public PlacementAttemptAllowance? AttemptAllowance { get; set; }

        public bool QuotaInitialized { get; set; } = true;
        public bool CanStartNewAttempt { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? CannotStartReason { get; set; }

        public string AssessmentMode { get; set; } = "TARGETED";
        public bool IsGoalConfirmed { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? LatestAssessmentLevelId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? LatestAssessmentLevelName { get; set; }

        public bool LatestAssessmentIsComprehensive { get; set; }

        /// <summary>NONE | GOAL_NOT_SET | COMPATIBLE | INSUFFICIENT_FOR_TARGET</summary>
        public string PlacementCompatibilityStatus { get; set; } = "NONE";

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? PlacementCompatibilityReason { get; set; }

        public List<int> AvailableAssessmentLevelIds { get; set; } = [];

        /// <summary>JLPT level codes covered by the general assessment for the confirmed goal (lower comparison level first).</summary>
        public List<string> PlannedAssessmentLevelCodes { get; set; } = [];

        /// <summary>Skills the general assessment measures (vocabulary, grammar, reading, listening).</summary>
        public List<string> PlannedSkillCodes { get; set; } = [];

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? ActiveAttemptId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? ActiveAttemptAssessmentLevelId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? ActiveAttemptAssessmentLevelName { get; set; }

        public bool ActiveAttemptIsComprehensive { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public DateTime? ActiveAttemptExpiresAt { get; set; }

        public bool HasCompletedPlacementTest { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? LatestCompletedAttemptId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? LatestAttemptId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public DateTime? LatestSubmittedAt { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public double? LatestScorePercent { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? AssessedJlptLevelId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? AssessedJlptLevelName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? RecommendedTargetJlptLevelId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? RecommendedTargetJlptLevelName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? TargetJlptLevelId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? TargetJlptLevelName { get; set; }

        public int DailyStudyMinutes { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public DateTime? TargetExamDate { get; set; }

        public bool RequiresMiniAssessment => false;

        public bool IsReadyForLearningPath { get; set; }
        public bool IsComprehensiveAssessmentRequired => false;
    }

    public class PlacementAttemptSnapshotResponse
    {
        public int AttemptId { get; set; }
        public string AttemptType { get; set; } = "placement";
        public string Status { get; set; } = "inProgress";
        public bool IsResumed { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime ServerNow { get; set; }
        public int TimeLimitMinutes { get; set; }
        public int? ParentAssessmentAttemptId { get; set; }
        public bool IsMiniAssessment => ParentAssessmentAttemptId.HasValue;
        public bool IsComprehensiveAssessment { get; set; }
        public bool IsShortenedAssessment { get; set; }
        public int TotalQuestions { get; set; }
        public List<PlacementTestQuestionResponse> Questions { get; set; } = [];
    }

    public class StartPlacementTestResponse : PlacementAttemptSnapshotResponse
    {
    }

    public class PlacementTestQuestionResponse
    {
        public int QuestionId { get; set; }
        public int MockTestId { get; set; }
        public int MockTestSectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public int SectionOrder { get; set; }
        public int JlptLevelId { get; set; }
        public string JlptLevelName { get; set; } = string.Empty;
        public int Order { get; set; }
        public string Content { get; set; } = string.Empty;

        /// <summary>Last saved learner choice; null if unanswered.</summary>
        public int? SelectedOptionId { get; set; }
        public string QuestionType { get; set; } = "SingleChoice";
        public System.Text.Json.JsonElement? InteractionData { get; set; }
        public System.Text.Json.JsonElement? Response { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? AudioUrl { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? ImageUrl { get; set; }

        public List<PlacementTestOptionResponse> Options { get; set; } = [];
    }

    public sealed class PlacementAnswersSavedResponse
    {
        public int AttemptId { get; set; }
        public DateTime SavedAt { get; set; }
        public int SavedQuestionCount { get; set; }
    }

    public class PlacementTestOptionResponse
    {
        public int OptionId { get; set; }
        public string? ImageUrl { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    public class PlacementLevelBreakdownItem
    {
        public int JlptLevelId { get; set; }
        public string JlptLevelName { get; set; } = string.Empty;
        public int CorrectCount { get; set; }
        public int TotalCount { get; set; }
        public double ScorePercent { get; set; }
    }

    public class PlacementTestResultResponse
    {
        public int AttemptId { get; set; }
        public string AttemptType { get; set; } = "placement";
        public string Status { get; set; } = "completed";
        public DateTime StartedAt { get; set; }

        /// <summary>Elapsed time from server start until submission, in seconds.</summary>
        public int DurationSeconds { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public DateTime? SubmittedAt { get; set; }

        public int? AssessmentTargetLevelId { get; set; }
        public int? ParentAssessmentAttemptId { get; set; }
        public bool IsMiniAssessment => ParentAssessmentAttemptId.HasValue;
        public bool IsComprehensiveAssessment { get; set; }
        public bool IsShortenedAssessment { get; set; }
        public bool RequiresMiniAssessment => false;
        public string? ScreeningLevelName => AssessmentTargetLevelId is int targetId && !IsMiniAssessment && !IsComprehensiveAssessment
            ? LevelBreakdowns.FirstOrDefault(level => level.JlptLevelId == targetId)?.JlptLevelName
            : null;
        public string? AssessmentMessage => ScreeningLevelName is { } level
            ? TotalScore < 50
                ? level == "N5"
                    ? "Bạn có thể bắt đầu lộ trình N5 từ nền tảng, ưu tiên các kỹ năng còn yếu."
                    : $"Lộ trình ôn {level} sẽ ưu tiên củng cố những kỹ năng còn yếu trước khi luyện đề."
                : $"Bạn có thể lập lộ trình ôn {level} theo kết quả từng kỹ năng."
            : null;

        internal bool CanRetake { get; set; }
        public string? FollowUpUnavailableReason { get; internal set; }
        public string[] NextActions => CanRetake ? ["RETAKE_TARGET_ASSESSMENT"] : [];
        public double TotalScore { get; set; }
        public int CorrectCount { get; set; }
        public int NotStudiedCount { get; set; }
        public int TotalQuestions { get; set; }
        public int AssessedJlptLevelId { get; set; }
        public string AssessedJlptLevelName { get; set; } = string.Empty;
        public int RecommendedTargetJlptLevelId { get; set; }
        public string RecommendedTargetJlptLevelName { get; set; } = string.Empty;
        public List<int> WrongQuestionIds { get; set; } = [];
        public List<PlacementLevelBreakdownItem> LevelBreakdowns { get; set; } = [];
        public List<SectionResultItem> Sections { get; set; } = [];
        public List<LearningPathSectionAssessmentResponse> SkillAssessments { get; set; } = [];
    }
}
