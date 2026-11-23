using System.Net;
using System.Text.RegularExpressions;
using RikiPath.Application.DTOs.Highlights;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Reviews;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Reviews;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services;

public partial class LearnerHighlightService(
    IUnitOfWork unitOfWork,
    IReviewService reviewService) : ILearnerHighlightService
{
    public async Task<ApiResponse<List<LearnerHighlightResponse>>> GetHighlightsAsync(
        int userId,
        string? targetType,
        int? targetId,
        string? targetKey,
        CancellationToken cancellationToken = default)
    {
        var list = await unitOfWork.LearnerHighlights.GetByUserAndTargetAsync(
            userId, targetType, targetId, targetKey, cancellationToken);
        return ApiResponse<List<LearnerHighlightResponse>>.Success(list.Select(Map).ToList());
    }

    public async Task<ApiResponse<LearnerHighlightResponse>> CreateHighlightAsync(
        int userId,
        CreateHighlightRequest request,
        CancellationToken cancellationToken = default)
    {
        var selected = SanitizePlainText(request.SelectedText);
        if (string.IsNullOrWhiteSpace(selected))
            return ApiResponse<LearnerHighlightResponse>.Fail("Đoạn văn bản highlight không được trống.");

        var normalizedColor = NormalizeColor(request.Color);
        var utcNow = DateTime.UtcNow;

        var entity = new LearnerHighlight
        {
            UserId = userId,
            TargetType = NormalizeTargetType(request.TargetType),
            TargetId = request.TargetId,
            TargetKey = SanitizeOptional(request.TargetKey),
            SourcePath = SanitizeOptional(request.SourcePath),
            SourceTitle = SanitizeOptional(request.SourceTitle),
            SelectedText = selected,
            Color = normalizedColor,
            AnchorPrefix = SanitizeOptional(request.AnchorPrefix),
            AnchorSuffix = SanitizeOptional(request.AnchorSuffix),
            StartOffset = request.StartOffset,
            EndOffset = request.EndOffset,
            ContentVersionHash = SanitizeOptional(request.ContentVersionHash),
            Note = SanitizeOptional(request.Note),
            TranslationSnapshot = SanitizeOptional(request.TranslationSnapshot),
            LinkedReviewCardId = request.LinkedReviewCardId,
            LinkedNoteEntryId = request.LinkedNoteEntryId,
            CreatedDate = utcNow,
        };

        await unitOfWork.LearnerHighlights.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<LearnerHighlightResponse>.Created(Map(entity));
    }

    public async Task<ApiResponse<LearnerHighlightResponse>> UpdateHighlightAsync(
        int userId,
        int highlightId,
        UpdateHighlightRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await unitOfWork.LearnerHighlights.GetByIdForUserAsync(highlightId, userId, cancellationToken);
        if (entity is null)
        {
            var raw = await unitOfWork.LearnerHighlights.GetByIdAsync(highlightId, cancellationToken);
            if (raw is not null && !raw.IsDeleted && raw.UserId != userId)
                return ApiResponse<LearnerHighlightResponse>.Fail("Không có quyền sửa highlight này.", HttpStatusCode.Forbidden);
            return ApiResponse<LearnerHighlightResponse>.NotFound("Không tìm thấy highlight.");
        }

        if (!string.IsNullOrWhiteSpace(request.Color))
            entity.Color = NormalizeColor(request.Color);
        if (request.Note is not null)
            entity.Note = SanitizeOptional(request.Note);
        if (request.TranslationSnapshot is not null)
            entity.TranslationSnapshot = SanitizeOptional(request.TranslationSnapshot);
        if (request.LinkedReviewCardId.HasValue)
            entity.LinkedReviewCardId = request.LinkedReviewCardId;
        if (request.LinkedNoteEntryId.HasValue)
            entity.LinkedNoteEntryId = request.LinkedNoteEntryId;

        entity.ModifiedDate = DateTime.UtcNow;
        unitOfWork.LearnerHighlights.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<LearnerHighlightResponse>.Success(Map(entity));
    }

    public async Task<ApiResponse<bool>> DeleteHighlightAsync(
        int userId,
        int highlightId,
        CancellationToken cancellationToken = default)
    {
        var entity = await unitOfWork.LearnerHighlights.GetByIdForUserAsync(highlightId, userId, cancellationToken);
        if (entity is null)
        {
            var raw = await unitOfWork.LearnerHighlights.GetByIdAsync(highlightId, cancellationToken);
            if (raw is not null && !raw.IsDeleted && raw.UserId != userId)
                return ApiResponse<bool>.Fail("Không có quyền xóa highlight này.", HttpStatusCode.Forbidden);
            return ApiResponse<bool>.NotFound("Không tìm thấy highlight.");
        }

        entity.IsDeleted = true;
        entity.ModifiedDate = DateTime.UtcNow;
        unitOfWork.LearnerHighlights.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<SrsEnrollOutcomeResponse>> PromoteToSrsAsync(
        int userId,
        int highlightId,
        HighlightToSrsRequest? request,
        CancellationToken cancellationToken = default)
    {
        var entity = await unitOfWork.LearnerHighlights.GetByIdForUserAsync(highlightId, userId, cancellationToken);
        if (entity is null)
            return ApiResponse<SrsEnrollOutcomeResponse>.NotFound("Không tìm thấy highlight.");

        var front = SanitizeOptional(request?.FrontText) ?? entity.SelectedText;
        var back = SanitizeOptional(request?.BackText)
            ?? SanitizeOptional(request?.Meaning)
            ?? entity.TranslationSnapshot
            ?? entity.Note;
        if (string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(back))
            return ApiResponse<SrsEnrollOutcomeResponse>.Fail("Vui lòng nhập nghĩa/mặt sau trước khi lưu vào SRS.");

        var enrolled = await reviewService.EnrollCardAsync(userId, new EnrollSrsCardRequest
        {
            SourceType = "Reading",
            SourceId = entity.TargetId,
            SourceLabel = entity.SourceTitle ?? entity.TargetType,
            LessonId = string.Equals(entity.TargetType, "Lesson", StringComparison.OrdinalIgnoreCase) ? entity.TargetId : null,
            CardType = "Custom",
            FrontText = front,
            BackText = back,
            Reading = SanitizeOptional(request?.Reading),
            Meaning = SanitizeOptional(request?.Meaning) ?? back,
            ContextSnippet = string.Join(" ", new[] { entity.AnchorPrefix, entity.SelectedText, entity.AnchorSuffix }.Where(s => !string.IsNullOrWhiteSpace(s))),
            JlptLevel = request?.JlptLevel,
            PersonalNote = SanitizeOptional(request?.PersonalNote) ?? entity.Note,
        }, cancellationToken);

        if (enrolled.IsSuccess && enrolled.Result is not null)
        {
            entity.LinkedReviewCardId = enrolled.Result.Card.ReviewCardId;
            entity.ModifiedDate = DateTime.UtcNow;
            unitOfWork.LearnerHighlights.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return enrolled;
    }

    private static string NormalizeColor(string? color)
    {
        var c = (color ?? string.Empty).Trim().ToLowerInvariant();
        return c switch
        {
            "sakura" or "rose" or "pink" or "#f5a9c4" => "sakura",
            "mint" or "green" or "sky" or "#8edac4" => "mint",
            _ => "yellow",
        };
    }

    private static string ResolveColorHex(string normalizedColor) => normalizedColor switch
    {
        "sakura" => "#F5A9C4",
        "mint" => "#8EDAC4",
        _ => "#F6D56D",
    };

    private static string NormalizeTargetType(string? targetType)
    {
        var t = (targetType ?? string.Empty).Trim();
        return t is "Lesson" or "Grammar" or "Vocabulary" or "Kanji" or "Reading" or "Note" ? t : "Custom";
    }

    private static string SanitizePlainText(string? input)
        => string.IsNullOrWhiteSpace(input) ? string.Empty : HtmlTagRegex().Replace(input.Trim(), string.Empty);

    private static string? SanitizeOptional(string? input)
    {
        var clean = SanitizePlainText(input);
        return string.IsNullOrWhiteSpace(clean) ? null : clean;
    }

    private static LearnerHighlightResponse Map(LearnerHighlight x)
    {
        var normColor = NormalizeColor(x.Color);
        return new LearnerHighlightResponse
        {
            Id = x.Id,
            TargetType = x.TargetType,
            TargetId = x.TargetId,
            TargetKey = x.TargetKey,
            SourcePath = x.SourcePath,
            SourceTitle = x.SourceTitle,
            SelectedText = x.SelectedText,
            Color = normColor,
            ColorHex = ResolveColorHex(normColor),
            AnchorPrefix = x.AnchorPrefix,
            AnchorSuffix = x.AnchorSuffix,
            StartOffset = x.StartOffset,
            EndOffset = x.EndOffset,
            ContentVersionHash = x.ContentVersionHash,
            Note = x.Note,
            TranslationSnapshot = x.TranslationSnapshot,
            LinkedReviewCardId = x.LinkedReviewCardId,
            LinkedNoteEntryId = x.LinkedNoteEntryId,
            CreatedDate = x.CreatedDate,
            ModifiedDate = x.ModifiedDate,
        };
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTagRegex();
}
