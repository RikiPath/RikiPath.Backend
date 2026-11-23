using System.Text.Json.Serialization;
using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>Ngân hàng câu hỏi thống nhất cho biên soạn thủ công, AI hỗ trợ và import đa định dạng.</summary>
    public class QuestionBankItem : Base
    {
        public int Id { get; set; }
        public Guid QuestionIdentity { get; set; } = Guid.NewGuid();
        public int CertificateLevelId { get; set; }
        public CertificateLevel CertificateLevel { get; set; } = null!;
        public int LanguageSkillId { get; set; }
        public LanguageSkill LanguageSkill { get; set; } = null!;
        public int? PassageId { get; set; }
        public ContentPassage? Passage { get; set; }

        public string? QuestionGroupTitle { get; set; }
        public int GroupSortOrder { get; set; }
        public string TemplateKind { get; set; } = "VocabMeaning";
        public string QuestionType { get; set; } = "SingleChoice";
        public string QuestionText { get; set; } = string.Empty;

        public string? SkillMetadataJson { get; set; }
        public string? InteractionDataJson { get; set; }
        public string? AnswerKeyJson { get; set; }
        public string? OptionsJson { get; set; }

        public string? AudioUrl { get; set; }
        public int? AudioDurationSeconds { get; set; }
        [JsonIgnore]
        public string? AudioTranscript { get; set; }
        public string? PlaybackPolicyJson { get; set; }
        public string? ImageUrl { get; set; }
        public string? Explanation { get; set; }

        public string? Difficulty { get; set; }
        public string? Topic { get; set; }
        public string? TagsJson { get; set; }

        public string SourceType { get; set; } = "Manual";
        public string? SourceReference { get; set; }
        public string? RightsStatus { get; set; }
        public string? AiProvenanceJson { get; set; }
        public string ContentFingerprint { get; set; } = string.Empty;

        public ContentStatus PublishStatus { get; set; } = ContentStatus.Draft;
        public int Version { get; set; } = 1;
        public int? ParentQuestionId { get; set; }
        public bool IsArchivedVersion { get; set; }
        public int? AuthorUserId { get; set; }

        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? ReviewedByName { get; set; }
        public string? ReviewNote { get; set; }
        public string? ReviewHistoryJson { get; set; }
    }
}
