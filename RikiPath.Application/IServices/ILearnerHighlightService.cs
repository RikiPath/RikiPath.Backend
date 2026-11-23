using RikiPath.Application.DTOs.Highlights;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Reviews;

namespace RikiPath.Application.IServices;

public interface ILearnerHighlightService
{
    Task<ApiResponse<List<LearnerHighlightResponse>>> GetHighlightsAsync(
        int userId,
        string? targetType,
        int? targetId,
        string? targetKey,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<LearnerHighlightResponse>> CreateHighlightAsync(
        int userId,
        CreateHighlightRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<LearnerHighlightResponse>> UpdateHighlightAsync(
        int userId,
        int highlightId,
        UpdateHighlightRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteHighlightAsync(
        int userId,
        int highlightId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<SrsEnrollOutcomeResponse>> PromoteToSrsAsync(
        int userId,
        int highlightId,
        HighlightToSrsRequest? request,
        CancellationToken cancellationToken = default);
}
