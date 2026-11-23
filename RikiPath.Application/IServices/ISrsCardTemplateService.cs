using RikiPath.Application.DTOs.SrsTemplates;
using RikiPath.Application.Responses;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices;

public interface ISrsCardTemplateService
{
    Task<ApiResponse<List<SrsCardTemplateResponse>>> GetPublishedForLearnerAsync(
        int userId,
        string? jlptLevel,
        string? cardType,
        int? lessonId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<SrsCardTemplateResponse>>> GetMyTemplatesAsync(
        int authorId,
        ContentStatus? status,
        string? cardType = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<SrsCardTemplateResponse>>> GetForAdminAsync(
        ContentStatus? status,
        string? jlptLevel,
        string? cardType,
        string? search,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<SrsCardTemplateResponse>> CreateDraftAsync(
        int authorId,
        SaveSrsCardTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<SrsCardTemplateResponse>> UpdateDraftAsync(
        int authorId,
        bool isAdmin,
        int templateId,
        SaveSrsCardTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<SrsCardTemplateResponse>> SubmitForReviewAsync(
        int authorId,
        bool isAdmin,
        int templateId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteDraftAsync(
        int authorId,
        bool isAdmin,
        int templateId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<SrsCardTemplateResponse>> ReviewTemplateAsync(
        int adminId,
        int templateId,
        ReviewSrsCardTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<SrsCardTemplateResponse>> TogglePublishAsync(
        int adminId,
        int templateId,
        bool publish,
        string? reason,
        CancellationToken cancellationToken = default);
}
