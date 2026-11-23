using System.Text.Json.Serialization;
using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>Đoạn đọc (Reading Passage) hoặc nhóm nghe hiểu (Listening Stimulus Group) dùng chung cho nhiều câu hỏi.</summary>
    public class ContentPassage : Base
    {
        public int Id { get; set; }
        public Guid PassageIdentity { get; set; } = Guid.NewGuid();
        public string PassageCode { get; set; } = string.Empty;
        public int LanguageSkillId { get; set; }
        public LanguageSkill LanguageSkill { get; set; } = null!;
        public int CertificateLevelId { get; set; }
        public CertificateLevel CertificateLevel { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string? Topic { get; set; }
        public string? PassageText { get; set; }
        public string? Instructions { get; set; }
        public string? ListeningSection { get; set; }

        public string? AudioUrl { get; set; }
        public int? AudioDurationSeconds { get; set; }
        [JsonIgnore]
        public string? AudioTranscript { get; set; }
        public string? ImageUrl { get; set; }
        public string? PlaybackPolicyJson { get; set; }

        public string SourceType { get; set; } = "Manual";
        public string? SourceReference { get; set; }
        public string? RightsStatus { get; set; }
        public string? AiProvenanceJson { get; set; }
        public string ContentFingerprint { get; set; } = string.Empty;

        public ContentStatus PublishStatus { get; set; } = ContentStatus.Draft;
        public int Version { get; set; } = 1;
        public int? ParentPassageId { get; set; }
        public bool IsArchivedVersion { get; set; }
        public int? AuthorUserId { get; set; }

        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? ReviewedByName { get; set; }
        public string? ReviewNote { get; set; }
        public string? ReviewHistoryJson { get; set; }

        public List<QuestionBankItem>? Questions { get; set; }
    }
}
