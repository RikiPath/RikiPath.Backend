using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.Reviews
{
    public class SubmitReviewResultResponse
    {
        public int ReviewCardId { get; set; }
        public int QualitySubmitted { get; set; }
        public ReviewRating RatingSubmitted { get; set; }
        public string StudyMode { get; set; } = "flip";
        public string SessionKind { get; set; } = "Scheduled";
        public bool ProgressMutated { get; set; } = true;
        public bool SessionExpired { get; set; }
        /// <summary>True when the frontend should add this incorrect card to the current session's retry queue.</summary>
        public bool ShouldRepeatInSession { get; set; }
        public bool IsCorrect { get; set; }
        public bool AutoEvaluated { get; set; }
        public string? MatchedAgainst { get; set; }
        public string? ExpectedAnswer { get; set; }
        public string? ExpectedReading { get; set; }
        public double NewEaseFactor { get; set; }
        public int NewIntervalDays { get; set; }
        public int Repetitions { get; set; }
        public int Lapses { get; set; }
        public SrsLearningState LearningState { get; set; }
        public DateTime NextReviewDate { get; set; }
        public int CurrentStreakDays { get; set; }
        public int LongestStreakDays { get; set; }
        public bool Deduplicated { get; set; }
    }
}
