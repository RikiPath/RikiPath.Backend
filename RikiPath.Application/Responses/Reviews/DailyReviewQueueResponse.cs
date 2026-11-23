namespace RikiPath.Application.Responses.Reviews
{
    public sealed class ExcludedMcqCardReasonResponse
    {
        public int ReviewCardId { get; set; }
        public string Term { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class DailyReviewQueueResponse
    {
        public int TotalDue { get; set; }
        public int TotalDueCards { get; set; }
        public int ModeEligibleCount { get; set; }
        public int PlannedSessionCount { get; set; }
        public int RawDueCount { get; set; }
        public int NewInQueue { get; set; }
        public int ReviewInQueue { get; set; }
        public int RelearningInQueue { get; set; }
        public string StudyMode { get; set; } = "flip";
        public DateTime? NextDueAtUtc { get; set; }
        public string? EmptyReason { get; set; } // CompletedToday | NoCardsDueNow | NoCardsYet | DailyLimitReached | InsufficientMcqDistractors | InsufficientTypingData | FiltersEmpty
        public List<string> ActiveModesInQueue { get; set; } = new();
        public List<ReviewQueueItemResponse> Items { get; set; } = new();
        public List<ExcludedMcqCardReasonResponse> ExcludedMcqCards { get; set; } = new();
        public List<ExcludedMcqCardReasonResponse> ExcludedTypingCards { get; set; } = new();
    }
}
