using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    /// <summary>Vocabulary bank entry — word, reading, meaning, example sentence and audio.</summary>
    public class VocabularyEntry : Base
    {
        public int Id { get; set; }
        public string Word { get; set; }
        public string Reading { get; set; }
        public string Meaning { get; set; }
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }
        public string? AudioUrl { get; set; }

        public ContentStatus Status { get; set; } = ContentStatus.Draft;
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedDate { get; set; }

        public int JlptLevelId { get; set; }
        public JlptLevel JlptLevel { get; set; }
        public int ContentAuthorId { get; set; }
        public UserAccount ContentAuthor { get; set; }
        public int? ReviewedById { get; set; }
        public UserAccount? ReviewedBy { get; set; }

        public List<LessonVocabulary>? LessonVocabularies { get; set; }
        public List<VocabularyNoteEntry>? VocabularyNoteEntries { get; set; }
    }
}
