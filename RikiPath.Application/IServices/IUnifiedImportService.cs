using RikiPath.Application.DTOs.ContentAuthoring;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices;

public enum UnifiedImportKind
{
    Auto = 0,
    Vocabulary = 1,
    Kanji = 2,
    Grammar = 3,
    Reading = 4,
    Listening = 5,
    MixedPractice = 6,
    FullMockTest = 7,
}

public sealed class UnifiedImportRowPreviewDto
{
    public int RowNumber { get; set; }
    public string RecordType { get; set; } = "Question"; // Passage | ListeningGroup | Question | ExamBlueprint
    public string RowKey { get; set; } = string.Empty;
    public string LevelCode { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public string TemplateKind { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
    public string Status { get; set; } = "Valid"; // Valid | Warning | Error | Duplicate
    public bool IncompleteMedia { get; set; }
    public bool NeedsReview { get; set; }
    public List<CanonicalValidationIssue> Issues { get; set; } = [];
    public List<DuplicateMatchDto> Duplicates { get; set; } = [];
    public CanonicalQuestionDto? QuestionPayload { get; set; }
    public CanonicalPassageDto? PassagePayload { get; set; }
}

public sealed class UnifiedImportExamPreviewDto
{
    public string Title { get; set; } = string.Empty;
    public string ExamKind { get; set; } = "FullMockTest"; // PracticeSet | MixedPractice | FullMockTest
    public string CertificateLevelCode { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public int TimeLimitMinutes { get; set; }
    public string? Description { get; set; }
    public string? SourceReference { get; set; }
    public string? RightsStatus { get; set; }
    public List<string> IncludedSkills { get; set; } = [];
    public List<UnifiedImportSectionPreviewDto> Sections { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class UnifiedImportSectionPreviewDto
{
    public int SortOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public string? CertificateSectionCode { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Instructions { get; set; }
    public List<int> QuestionBankItemIds { get; set; } = [];
}

public sealed class UnifiedImportMediaAssetDto
{
    public string ArchivePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class UnifiedImportPreviewResponse
{
    public Guid PreviewId { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string DetectedFormat { get; set; } = string.Empty;
    public string DetectedKind { get; set; } = string.Empty;
    public bool CanConfirm { get; set; }
    public int TotalItems { get; set; }
    public int ValidCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public int DuplicateCount { get; set; }
    public List<string> FileErrors { get; set; } = [];
    public List<string> FileWarnings { get; set; } = [];
    public List<UnifiedImportMediaAssetDto> MediaAssets { get; set; } = [];
    public List<UnifiedImportRowPreviewDto> Rows { get; set; } = [];
    public UnifiedImportExamPreviewDto? ExamBlueprint { get; set; }
}

public sealed class ConfirmUnifiedImportRequest
{
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceFormat { get; set; } = "Excel"; // Excel | Json | Zip | Pdf
    public string? IdempotencyKey { get; set; }
    public UnifiedImportExamPreviewDto? ExamBlueprint { get; set; }
    public List<UnifiedImportRowPreviewDto> Rows { get; set; } = [];
}

public sealed class ConfirmUnifiedImportResponse
{
    public Guid BatchId { get; set; }
    public bool DeduplicatedByIdempotency { get; set; }
    public int PassagesDrafted { get; set; }
    public int QuestionsDrafted { get; set; }
    public int? ExamDraftId { get; set; }
    public ContentStatus ResultingStatus { get; set; } = ContentStatus.Draft;
    public List<string> Warnings { get; set; } = [];
}

public sealed class RoundTripVerificationResponse
{
    public bool Passed { get; set; }
    public int SourceQuestionCount { get; set; }
    public int ReparsedQuestionCount { get; set; }
    public int MissingCount { get; set; }
    public List<string> Discrepancies { get; set; } = [];
    public UnifiedImportPreviewResponse? ReparsedPreview { get; set; }
}

public interface IUnifiedImportService
{
    Task<ApiResponse<UnifiedImportPreviewResponse>> PreviewAsync(
        Stream fileStream, string fileName, UnifiedImportKind expectedKind, CancellationToken ct);

    Task<ApiResponse<ConfirmUnifiedImportResponse>> ConfirmAsync(
        ConfirmUnifiedImportRequest request,
        IReadOnlyDictionary<string, (Stream Content, string ContentType, string FileName)>? mediaFiles,
        int actorUserId,
        CancellationToken ct);

    Task<ApiResponse<ConfirmUnifiedImportResponse>> ConfirmFromArchiveAsync(
        Stream zipOrDataStream,
        string fileName,
        UnifiedImportKind expectedKind,
        string? idempotencyKey,
        int actorUserId,
        CancellationToken ct);
}
