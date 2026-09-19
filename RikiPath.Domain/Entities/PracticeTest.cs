using Domain.Enums;
using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>A timed, auto-scored mock JLPT practice test reproducing the official JLPT section structure.</summary>
    public class PracticeTest : Base
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public int TimeLimitMinutes { get; set; }

        public ContentStatus Status { get; set; } = ContentStatus.Draft;
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public string? ReviewedByName { get; set; }

        public int CertificationLevelId { get; set; }
        public CertificationLevel CertificationLevel { get; set; }
        public int ContentAuthorId { get; set; }
        public UserAccount ContentAuthor { get; set; }

        public List<PracticeTestSection>? Sections { get; set; }
        public List<PracticeTestAttempt>? Attempts { get; set; }
    }
}
