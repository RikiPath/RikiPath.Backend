using RikiPath.Application.Common;
using RikiPath.Application.DTOs.ContentAuthoring;
using RikiPath.Application.Responses;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices;

public enum ModerationDecision
{
    Approve = 1,
    ApproveAndPublish = 2,
    RequestChanges = 3,
    Reject = 4,
    Publish = 5,
}

public sealed class ModerateContentV2Request
{
    public ModerationDecision Decision { get; set; }
    public string? ReviewNote { get; set; }
    public bool RightsConfirmed { get; set; }
}

public sealed class UnpublishContentRequest
{
    public string TargetType { get; set; } = "Exam"; // Exam | Question | Passage
    public int TargetId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class ModerationQueueItemDto
{
    public int RequestId { get; set; }
    public Guid BatchId { get; set; }
    public ImportEntityType EntityType { get; set; }
    public string RowKey { get; set; } = string.Empty;
    public int? TargetEntityId { get; set; }
    public ContentApprovalStatus Status { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int AuthorUserId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public string? RightsStatus { get; set; }
    public bool RightsConfirmed { get; set; }
    public string? LevelCode { get; set; }
    public string? SkillName { get; set; }
    public AiProvenanceDto? AiProvenance { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}

public sealed class ModerationDetailDto
{
    public ModerationQueueItemDto Summary { get; set; } = new();
    public string PayloadJson { get; set; } = string.Empty;
    public object? StructuredPreview { get; set; }
    public string? ValidationSummaryJson { get; set; }
    public List<ReviewHistoryEntryDto> ReviewHistory { get; set; } = [];
    public ContentStatus? CurrentEntityPublishStatus { get; set; }
    public int? CurrentEntityVersion { get; set; }
}

public interface IContentModerationV2Service
{
    Task<ApiResponse<PagedResult<ModerationQueueItemDto>>> ListQueueAsync(
        ContentApprovalStatus? status, ImportEntityType? entityType, string? keyword,
        int page, int pageSize, CancellationToken ct);

    Task<ApiResponse<ModerationDetailDto>> GetDetailAsync(int requestId, CancellationToken ct);

    Task<ApiResponse<ModerationDetailDto>> ModerateAsync(
        int requestId, ModerateContentV2Request request, int adminUserId, string adminName, CancellationToken ct);

    Task<ApiResponse> UnpublishAsync(
        UnpublishContentRequest request, int adminUserId, CancellationToken ct);
}
