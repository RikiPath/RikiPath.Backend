using Domain.Enums;
using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
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
        public string? ReviewedByName { get; set; }

        public int CertificationLevelId { get; set; }
        public CertificationLevel CertificationLevel { get; set; }
        public int ContentAuthorId { get; set; }
        public UserAccount ContentAuthor { get; set; }

        public List<LessonGrammar>? LessonGrammars { get; set; }
        public List<ReviewItem>? ReviewItems { get; set; }
    }
}
