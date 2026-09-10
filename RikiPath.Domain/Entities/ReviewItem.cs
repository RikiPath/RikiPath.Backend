using System;
using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// One item in a learner's spaced-repetition review queue — a personal notebook
    /// word, or a previously studied kanji/grammar item. Exactly one of
    /// VocabularyNoteEntryId / KanjiEntryId / GrammarPointId is set.
    /// EaseFactor/IntervalDays/Repetitions follow the SM-2 algorithm.
    /// </summary>
    public class ReviewItem : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }

        public int? VocabularyNoteEntryId { get; set; }
        public VocabularyNoteEntry? VocabularyNoteEntry { get; set; }
        public int? KanjiEntryId { get; set; }
        public KanjiEntry? KanjiEntry { get; set; }
        public int? GrammarPointId { get; set; }
        public GrammarPoint? GrammarPoint { get; set; }

        public double EaseFactor { get; set; } = 2.5;
        public int IntervalDays { get; set; } = 0;
        public int Repetitions { get; set; } = 0;
        public DateTime NextReviewDate { get; set; }
        public DateTime? LastReviewedAt { get; set; }

        public List<ReviewLog>? ReviewLogs { get; set; }
    }
}
