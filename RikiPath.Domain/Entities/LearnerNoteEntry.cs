using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// Level 4 of Personal Notebook V3 (table <c>"LearnerNoteEntries"</c>): a Saved Item anchored to
    /// a parent <see cref="LearnerNote"/> (Notebook File) and <see cref="NotebookPage"/>.
    /// </summary>
    public class LearnerNoteEntry : Base
    {
        public int Id { get; set; }

        public int LearnerNoteId { get; set; }
        public LearnerNote LearnerNote { get; set; } = null!;

        public int? NotebookPageId { get; set; }
        public NotebookPage? NotebookPage { get; set; }

        public int? VocabularyId { get; set; }
        public Vocabulary? Vocabulary { get; set; }
        public int? KanjiId { get; set; }
        public Kanji? Kanji { get; set; }
        public int? GrammarPatternId { get; set; }
        public GrammarPattern? GrammarPattern { get; set; }

        // Manual / saved knowledge item fields
        public string? ManualWord { get; set; }
        public string? ManualReading { get; set; }
        public string? ManualMeaning { get; set; }

        public string? Note { get; set; }

        public string ItemKind { get; set; } = "manual";
        public string? HighlightColor { get; set; }
        public string? SourceType { get; set; }
        public int? SourceId { get; set; }
        public string? SourcePath { get; set; }
        public string? SourceTitle { get; set; }
        public string? ContextSnippet { get; set; }
        public string? ExampleSentence { get; set; }
        public string? ExampleMeaning { get; set; }
        public string TagsJson { get; set; } = "[]";
        public bool IsFavorite { get; set; }
        public int SortOrder { get; set; }
        public int Revision { get; set; } = 1;

        public List<ReviewCard>? ReviewCards { get; set; }
    }
}
