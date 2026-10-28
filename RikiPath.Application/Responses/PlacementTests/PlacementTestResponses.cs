using System.Text.Json.Serialization;
using RikiPath.Application.Responses.MockTests;

namespace RikiPath.Application.Responses.PlacementTests
{
    public class PlacementTestStatusResponse
    {
        public string AttemptType { get; set; } = "placement";
        public string PlacementDecision { get; set; } = "NOT_TAKEN";
        public bool HasActiveAttempt { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? ActiveAttemptId { get; set; }

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

        public bool IsReadyForLearningPath { get; set; }
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

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? AudioUrl { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? ImageUrl { get; set; }

        public List<PlacementTestOptionResponse> Options { get; set; } = [];
    }

    public class PlacementTestOptionResponse
    {
        public int OptionId { get; set; }
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

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public DateTime? SubmittedAt { get; set; }

        public double TotalScore { get; set; }
        public int CorrectCount { get; set; }
        public int TotalQuestions { get; set; }
        public int AssessedJlptLevelId { get; set; }
        public string AssessedJlptLevelName { get; set; } = string.Empty;
        public int RecommendedTargetJlptLevelId { get; set; }
        public string RecommendedTargetJlptLevelName { get; set; } = string.Empty;
        public List<int> WrongQuestionIds { get; set; } = [];
        public List<PlacementLevelBreakdownItem> LevelBreakdowns { get; set; } = [];
        public List<SectionResultItem> Sections { get; set; } = [];
    }
}
