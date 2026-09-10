using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// A single word saved into a personal vocabulary list — either linked back to a
    /// shared bank entry (VocabularyEntry/KanjiEntry) or entered manually. Ownership is
    /// derived through <see cref="VocabularyList"/> (no direct UserId here, to avoid a
    /// redundant ownership path).
    /// </summary>
    public class VocabularyNoteEntry : Base
    {
        public int Id { get; set; }

        public int VocabularyListId { get; set; }
        public VocabularyList VocabularyList { get; set; }

        public int? VocabularyEntryId { get; set; }
        public VocabularyEntry? VocabularyEntry { get; set; }
        public int? KanjiEntryId { get; set; }
        public KanjiEntry? KanjiEntry { get; set; }

        // Manual entry, used when not linked to a shared bank entry
        public string? ManualWord { get; set; }
        public string? ManualReading { get; set; }
        public string? ManualMeaning { get; set; }

        public string? Note { get; set; }

        public List<ReviewItem>? ReviewItems { get; set; }
    }
}
