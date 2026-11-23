using System;

using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>History of individual review sessions for a ReviewCard — drives SM-2 recalculation.</summary>
    public class ReviewHistory : Base
    {
        public int Id { get; set; }

        public int ReviewCardId { get; set; }
        public ReviewCard ReviewCard { get; set; } = null!;

        public ReviewRating Rating { get; set; }
        public int Quality { get; set; }
        public DateTime ReviewedAt { get; set; }

        public string StudyMode { get; set; } = "flip";
        public string? UserAnswer { get; set; }
        public bool? IsCorrect { get; set; }
        public int? ResponseTimeMs { get; set; }
        public int PreviousIntervalDays { get; set; }
        public int NewIntervalDays { get; set; }
        public double PreviousEaseFactor { get; set; } = 2.5;
        public double NewEaseFactor { get; set; } = 2.5;
        public SrsLearningState PreviousState { get; set; } = SrsLearningState.New;
        public SrsLearningState NewState { get; set; } = SrsLearningState.Learning;
        public string? IdempotencyKey { get; set; }
        public string? SessionReference { get; set; }
    }
}
