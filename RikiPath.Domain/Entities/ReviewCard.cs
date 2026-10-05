using RikiPath.Domain.Enums;
using System;
using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// One item in a learner's spaced-repetition review queue — a personal notebook
    /// word, or a previously studied kanji/grammar item. Exactly one of
    /// LearnerNoteEntryId / KanjiId / GrammarPatternId is set.
    /// EaseFactor/IntervalDays/Repetitions follow the SM-2 algorithm.
    /// </summary>
    public class ReviewCard : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }

        public int? LearnerNoteEntryId { get; set; }
        public LearnerNoteEntry? LearnerNoteEntry { get; set; }
        public int? KanjiId { get; set; }
        public Kanji? Kanji { get; set; }
        public int? GrammarPatternId { get; set; }
        public GrammarPattern? GrammarPattern { get; set; }
        public ReviewMode Mode { get; set; } = ReviewMode.Recognition;
        public double EaseFactor { get; set; } = 2.5;
        public int IntervalDays { get; set; } = 0;
        public int Repetitions { get; set; } = 0;
        public DateTime NextReviewDate { get; set; }
        public DateTime? LastReviewedAt { get; set; }
        public int? WritingScore { get; set; }
        public int? WritingCorrectStrokeCount { get; set; }
        public int? WritingTotalStrokeCount { get; set; }
        public string? WritingPracticeMode { get; set; }
        public DateTime? WritingScoreUpdatedAt { get; set; }

        public List<ReviewHistory>? ReviewHistories { get; set; }
    }
}
