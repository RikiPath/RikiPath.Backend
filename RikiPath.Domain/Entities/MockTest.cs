using RikiPath.Domain.Enums;
using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>A timed, auto-scored mock JLPT practice test reproducing the official JLPT section structure.</summary>
    public class MockTest : Base
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public int TimeLimitMinutes { get; set; }
        /// <summary>Indicates this published mock test is used for onboarding placement.</summary>

        public int CertificateLevelId { get; set; }
        public CertificateLevel CertificateLevel { get; set; }

        public ExamKind ExamKind { get; set; } = ExamKind.FullMockTest;
        public ContentStatus PublishStatus { get; set; } = ContentStatus.Draft;
        public int Version { get; set; } = 1;
        public int? ParentExamId { get; set; }
        public bool IsArchivedVersion { get; set; }
        public bool IsManagedWorkflow { get; set; }
        public int? AuthorUserId { get; set; }
        public int? FocusSkillId { get; set; }
        public string? IncludedSkillsJson { get; set; }
        public string? TagsJson { get; set; }
        public string? SourceType { get; set; }
        public string? SourceReference { get; set; }
        public string? RightsStatus { get; set; }
        public string? BlueprintPolicyJson { get; set; }
        public string? AiProvenanceJson { get; set; }
        public string? ReviewNote { get; set; }
        public string? ReviewedByName { get; set; }
        public string? ReviewHistoryJson { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime? PublishedAt { get; set; }

        public List<MockTestSection>? Sections { get; set; }
    }
}
