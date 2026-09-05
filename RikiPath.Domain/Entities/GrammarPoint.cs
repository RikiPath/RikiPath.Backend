using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    /// <summary>Grammar bank entry — structure, usage notes, example sentence.</summary>
    public class GrammarPoint : Base
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Structure { get; set; }
        public string? UsageNotes { get; set; }
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }

        public ContentStatus Status { get; set; } = ContentStatus.Draft;
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedDate { get; set; }

        public int JlptLevelId { get; set; }
        public JlptLevel JlptLevel { get; set; }
        public int ContentAuthorId { get; set; }
        public UserAccount ContentAuthor { get; set; }
        public int? ReviewedById { get; set; }
        public UserAccount? ReviewedBy { get; set; }

        public List<LessonGrammar>? LessonGrammars { get; set; }
        public List<ReviewItem>? ReviewItems { get; set; }
    }
}
