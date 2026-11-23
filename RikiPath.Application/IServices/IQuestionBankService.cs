using RikiPath.Application.Common;
using RikiPath.Application.DTOs.ContentAuthoring;
using RikiPath.Application.Responses;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices;

public sealed class QuestionBankQueryFilter
{
    public int? CertificateLevelId { get; set; }
    public int? LanguageSkillId { get; set; }
    public string? QuestionType { get; set; }
    public string? TemplateKind { get; set; }
    public ContentStatus? PublishStatus { get; set; }
    public string? SourceType { get; set; }
    public int? PassageId { get; set; }
    public bool? HasMedia { get; set; }
    public bool OnlyMine { get; set; }
    public bool IncludeArchived { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class CanonicalPassageWithQuestionsDto
{
    public CanonicalPassageDto Passage { get; set; } = new();
    public List<CanonicalQuestionDto> Questions { get; set; } = [];
    public string? QuestionGroupTitle { get; set; }
}

public sealed class DuplicateCheckResponseDto
{
    public string Fingerprint { get; set; } = string.Empty;
    public bool HasExactDuplicate => Matches.Any(m => m.MatchKind == "ExactFingerprint");
    public List<DuplicateMatchDto> Matches { get; set; } = [];
}

public interface IQuestionBankService
{
    Task<ApiResponse<PagedResult<CanonicalQuestionDto>>> SearchAsync(
        QuestionBankQueryFilter filter, int actorUserId, bool isAdmin, CancellationToken ct);

    Task<ApiResponse<CanonicalQuestionDto>> GetDetailAsync(
        int id, int actorUserId, bool isAdmin, CancellationToken ct);

    Task<ApiResponse<CanonicalQuestionDto>> SaveDraftAsync(
        CanonicalQuestionDto request, int actorUserId, CancellationToken ct);

    Task<ApiResponse<CanonicalQuestionDto>> CreateNewVersionAsync(
        int sourceQuestionId, CanonicalQuestionDto? updates, int actorUserId, CancellationToken ct);

    Task<ApiResponse<CanonicalQuestionDto>> SubmitQuestionAsync(
        int id, int actorUserId, CancellationToken ct);

    Task<ApiResponse> DeleteDraftAsync(int id, int actorUserId, bool isAdmin, CancellationToken ct);

    Task<ApiResponse<DuplicateCheckResponseDto>> CheckDuplicatesAsync(
        CanonicalQuestionDto candidate, int? excludeId, CancellationToken ct);

    Task<ApiResponse<List<CanonicalPassageDto>>> ListPassagesAsync(
        int? levelId, int? skillId, ContentStatus? status, string? keyword,
        int actorUserId, bool isAdmin, CancellationToken ct);

    Task<ApiResponse<CanonicalPassageWithQuestionsDto>> GetPassageGroupDetailAsync(
        int passageId, int actorUserId, bool isAdmin, CancellationToken ct);

    Task<ApiResponse<CanonicalPassageWithQuestionsDto>> SavePassageGroupDraftAsync(
        CanonicalPassageWithQuestionsDto request, int actorUserId, CancellationToken ct);

    Task<ApiResponse<CanonicalPassageWithQuestionsDto>> SubmitPassageGroupAsync(
        int passageId, int actorUserId, CancellationToken ct);
}
