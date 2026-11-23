using RikiPath.Application.Common;
using RikiPath.Application.DTOs.ContentAuthoring;
using RikiPath.Application.Responses;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices;

public sealed class ExamBlueprintPolicyDto
{
    public bool ShowAnswersAfterSubmit { get; set; } = true;
    public bool ShowExplanationsAfterSubmit { get; set; } = true;
    public string ScoringPolicy { get; set; } = "SimulatedJlpt";
    public int? PassingScore { get; set; }
    public ListeningPlaybackPolicyDto? DefaultListeningPolicy { get; set; }
}

public sealed class ExamSectionBuilderDto
{
    public int? Id { get; set; }
    public int SortOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    public int LanguageSkillId { get; set; }
    public string? SkillName { get; set; }
    public int CertificateSectionId { get; set; }
    public string? CertificateSectionCode { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Instructions { get; set; }
    /// <summary>Danh sách câu hỏi theo đúng thứ tự: hoặc tham chiếu QuestionBankItemId có sẵn, hoặc câu hỏi soạn trực tiếp.</summary>
    public List<ExamQuestionSlotDto> Questions { get; set; } = [];
}

public sealed class ExamQuestionSlotDto
{
    public int? MockQuestionId { get; set; }
    public int? QuestionBankItemId { get; set; }
    public int SortOrder { get; set; }
    public CanonicalQuestionDto? InlineQuestion { get; set; }
}

public sealed class ExamBuilderDto
{
    public int? Id { get; set; }
    public ExamKind ExamKind { get; set; } = ExamKind.FullMockTest;
    public ContentStatus PublishStatus { get; set; } = ContentStatus.Draft;
    public int Version { get; set; } = 1;
    public int? ParentExamId { get; set; }
    public bool IsArchivedVersion { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CertificateLevelId { get; set; }
    public string? CertificateLevelCode { get; set; }
    public int TimeLimitMinutes { get; set; } = 30;
    public int? FocusSkillId { get; set; }
    public List<string> IncludedSkills { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string SourceType { get; set; } = "Manual";
    public string? SourceReference { get; set; }
    public string? RightsStatus { get; set; }
    public ExamBlueprintPolicyDto? BlueprintPolicy { get; set; }
    public AiProvenanceDto? AiProvenance { get; set; }
    public string? ReviewNote { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public List<ExamSectionBuilderDto> Sections { get; set; } = [];
}

public sealed class ExamBlueprintValidationResponse
{
    public bool IsValid { get; set; }
    public bool CanPublish { get; set; }
    public int TotalQuestions { get; set; }
    public int MissingAudioCount { get; set; }
    public List<string> CoveredSkills { get; set; } = [];
    public List<CanonicalValidationIssue> Issues { get; set; } = [];
}

public interface IExamBuilderService
{
    Task<ApiResponse<PagedResult<ExamBuilderDto>>> ListAuthorExamsAsync(
        ExamKind? examKind, int? levelId, ContentStatus? status, string? keyword,
        int page, int pageSize, int actorUserId, bool isAdmin, CancellationToken ct);

    Task<ApiResponse<ExamBuilderDto>> GetDetailAsync(int examId, int actorUserId, bool isAdmin, CancellationToken ct);

    Task<ApiResponse<ExamBuilderDto>> SaveDraftAsync(ExamBuilderDto request, int actorUserId, CancellationToken ct);

    Task<ApiResponse<ExamBuilderDto>> CreateNewVersionAsync(int sourceExamId, int actorUserId, CancellationToken ct);

    Task<ApiResponse<ExamBlueprintValidationResponse>> ValidateExamAsync(int examId, CancellationToken ct);

    Task<ApiResponse<ExamBuilderDto>> SubmitExamAsync(int examId, int actorUserId, CancellationToken ct);

    Task<ApiResponse> DeleteDraftExamAsync(int examId, int actorUserId, bool isAdmin, CancellationToken ct);
}
