using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// A single word saved into a personal vocabulary list — either linked back to a
    /// shared bank entry (Vocabulary/Kanji) or entered manually. Ownership is
    /// derived through <see cref="LearnerNote"/> (no direct UserId here, to avoid a
    /// redundant ownership path).
    /// </summary>
    public class LearnerNoteEntry : Base
    {
        public int Id { get; set; }

        public int LearnerNoteId { get; set; }
        public LearnerNote LearnerNote { get; set; }

        public int? VocabularyId { get; set; }
        public Vocabulary? Vocabulary { get; set; }
        public int? KanjiId { get; set; }
        public Kanji? Kanji { get; set; }

        // Manual entry, used when not linked to a shared bank entry
        public string? ManualWord { get; set; }
        public string? ManualReading { get; set; }
        public string? ManualMeaning { get; set; }

        public string? Note { get; set; }

        public List<ReviewCard>? ReviewCards { get; set; }
    }
}
