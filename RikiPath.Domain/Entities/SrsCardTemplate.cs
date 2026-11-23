using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities;

/// <summary>Curated SRS flashcard template authored by Content Author and approved by Admin before Learner consumption.</summary>
public class SrsCardTemplate : Base
{
    public int Id { get; set; }

    public string CardType { get; set; } = "Vocabulary";
    public string FrontText { get; set; } = string.Empty;
    public string BackText { get; set; } = string.Empty;
    public string? Reading { get; set; }
    public string? Meaning { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ExampleMeaning { get; set; }
    public string? UsageNotes { get; set; }
    public string? AcceptedReadingsJson { get; set; }
    public string? SynonymsJson { get; set; }
    public string? AudioUrl { get; set; }

    public int? CertificateLevelId { get; set; }
    public CertificateLevel? CertificateLevel { get; set; }
    public string? JlptLevel { get; set; }

    public int? LessonId { get; set; }
    public Lesson? Lesson { get; set; }
    public int? VocabularyId { get; set; }
    public Vocabulary? Vocabulary { get; set; }
    public int? KanjiId { get; set; }
    public Kanji? Kanji { get; set; }
    public int? GrammarPatternId { get; set; }
    public GrammarPattern? GrammarPattern { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public int Version { get; set; } = 1;

    public int? SubmittedByUserId { get; set; }
    public UserAccount? SubmittedByUser { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public UserAccount? ReviewedByAdmin { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }

    public DateTime? UnpublishedAtUtc { get; set; }
    public string? UnpublishedReason { get; set; }

    public string NormalizedContentKey { get; set; } = string.Empty;
}
