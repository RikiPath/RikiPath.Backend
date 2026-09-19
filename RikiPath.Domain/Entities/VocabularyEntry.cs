using Domain.Enums;
using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
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
        public string? ReviewedByName { get; set; }

        public int CertificationLevelId { get; set; }
        public CertificationLevel CertificationLevel { get; set; }
        public int ContentAuthorId { get; set; }
        public UserAccount ContentAuthor { get; set; }

        public List<LessonVocabulary>? LessonVocabularies { get; set; }
        public List<VocabularyNoteEntry>? VocabularyNoteEntries { get; set; }
    }
}
