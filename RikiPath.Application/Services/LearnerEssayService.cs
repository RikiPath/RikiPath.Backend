using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Essay;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services;

public class LearnerEssayService(
    IUnitOfWork unitOfWork,
    IClaimService claimService,
    IFileStorageService fileStorage,
    IOcrService ocrService) : ILearnerEssayService
{
    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp"];
    private const long MaxImageSizeBytes = 10 * 1024 * 1024;

    public async Task<ApiResponse<EssayScanResponse>> ScanAsync(
        IFormFile image,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateImage(image);
        if (validation is not null)
            return ApiResponse<EssayScanResponse>.Fail(validation);

        try
        {
            await using var ocrStream = image.OpenReadStream();
            var text = await ocrService.RecognizeJapaneseAsync(
                ocrStream, image.FileName, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
                return ApiResponse<EssayScanResponse>.Fail(
                    "Không nhận diện được chữ tiếng Nhật trong ảnh. Hãy dùng ảnh rõ hơn và chụp đủ sáng.");

            await using var uploadStream = image.OpenReadStream();
            var uploaded = await fileStorage.UploadAsync(
                uploadStream, image.FileName, image.ContentType, "essays", cancellationToken);

            var now = DateTime.UtcNow;
            var essay = new LearnerEssay
            {
                UserId = claimService.GetUserClaim().Id,
                Title = BuildTitle(text),
                ImageUrl = uploaded.Url,
                ImageStoragePath = uploaded.StoredFileName,
                OriginalOcrText = text,
                ContentText = text,
                OcrLanguage = "jpn",
                CreatedDate = now,
                ModifiedDate = now,
            };

            await unitOfWork.LearnerEssays.AddAsync(essay, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<EssayScanResponse>.Created(MapScan(essay));
        }
        catch (Exception ex)
        {
            return ApiResponse<EssayScanResponse>.Fail(
                $"Quét văn bản thất bại: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<List<EssayListItemResponse>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var essays = await unitOfWork.LearnerEssays.GetByUserIdAsync(userId, cancellationToken);
            return ApiResponse<List<EssayListItemResponse>>.Success(
                essays.Select(MapList).ToList());
        }
        catch (Exception ex)
        {
            return ApiResponse<List<EssayListItemResponse>>.Fail(
                $"Không thể tải danh sách bài viết: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<EssayDetailResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var essay = await GetOwnedEssayAsync(id, cancellationToken);
        return essay is null
            ? ApiResponse<EssayDetailResponse>.NotFound("Không tìm thấy bài viết.")
            : ApiResponse<EssayDetailResponse>.Success(MapDetail(essay));
    }

    public async Task<ApiResponse<EssayDetailResponse>> UpdateAsync(
        int id,
        string contentText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentText))
            return ApiResponse<EssayDetailResponse>.Fail("Nội dung bài viết không được để trống.");

        var essay = await GetOwnedEssayAsync(id, cancellationToken);
        if (essay is null)
            return ApiResponse<EssayDetailResponse>.NotFound("Không tìm thấy bài viết.");

        essay.ContentText = contentText.Trim();
        essay.Title = BuildTitle(essay.ContentText);
        essay.ModifiedDate = DateTime.UtcNow;
        unitOfWork.LearnerEssays.Update(essay);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<EssayDetailResponse>.Success(MapDetail(essay));
    }

    public async Task<ApiResponse> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var essay = await GetOwnedEssayAsync(id, cancellationToken);
        if (essay is null)
            return ApiResponse.NotFound("Không tìm thấy bài viết.");

        await fileStorage.DeleteAsync(essay.ImageStoragePath, "essays", cancellationToken);
        essay.IsDeleted = true;
        essay.ModifiedDate = DateTime.UtcNow;
        unitOfWork.LearnerEssays.Update(essay);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success();
    }

    public async Task<ApiResponse<EssayScanResponse>> RescanAsync(
        int id,
        IFormFile image,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateImage(image);
        if (validation is not null)
            return ApiResponse<EssayScanResponse>.Fail(validation);

        var essay = await GetOwnedEssayAsync(id, cancellationToken);
        if (essay is null)
            return ApiResponse<EssayScanResponse>.NotFound("Không tìm thấy bài viết.");

        try
        {
            await using var ocrStream = image.OpenReadStream();
            var text = await ocrService.RecognizeJapaneseAsync(
                ocrStream, image.FileName, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
                return ApiResponse<EssayScanResponse>.Fail(
                    "Không nhận diện được chữ tiếng Nhật trong ảnh. Hãy dùng ảnh rõ hơn và chụp đủ sáng.");

            await using var uploadStream = image.OpenReadStream();
            var uploaded = await fileStorage.UploadAsync(
                uploadStream, image.FileName, image.ContentType, "essays", cancellationToken);
            var oldPath = essay.ImageStoragePath;

            essay.ImageUrl = uploaded.Url;
            essay.ImageStoragePath = uploaded.StoredFileName;
            essay.OriginalOcrText = text;
            essay.ContentText = text;
            essay.Title = BuildTitle(text);
            essay.ModifiedDate = DateTime.UtcNow;
            unitOfWork.LearnerEssays.Update(essay);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                await fileStorage.DeleteAsync(oldPath, "essays", cancellationToken);
            }
            catch
            {
                // The database now points to the new image; orphan cleanup can be retried separately.
            }

            return ApiResponse<EssayScanResponse>.Success(MapScan(essay));
        }
        catch (Exception ex)
        {
            return ApiResponse<EssayScanResponse>.Fail(
                $"Quét lại văn bản thất bại: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    private async Task<LearnerEssay?> GetOwnedEssayAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = claimService.GetUserClaim().Id;
        var essay = await unitOfWork.LearnerEssays.GetByIdAsync(id, cancellationToken);
        return essay is { IsDeleted: false } && essay.UserId == userId ? essay : null;
    }

    private static string? ValidateImage(IFormFile? image)
    {
        if (image is null || image.Length == 0)
            return "Vui lòng chọn ảnh bài viết.";
        if (image.Length > MaxImageSizeBytes)
            return "Ảnh bài viết tối đa 10MB.";
        if (!AllowedContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
            return "Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.";
        return null;
    }

    private static string BuildTitle(string text)
    {
        var normalized = string.Join(' ', text.Split(
            [' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 80 ? normalized : normalized[..80].TrimEnd() + "…";
    }

    private static EssayScanResponse MapScan(LearnerEssay essay) => new()
    {
        Id = essay.Id,
        Title = essay.Title,
        ImageUrl = essay.ImageUrl,
        OriginalOcrText = essay.OriginalOcrText,
        ContentText = essay.ContentText,
        ScannedAt = essay.CreatedDate,
    };

    private static EssayDetailResponse MapDetail(LearnerEssay essay) => new()
    {
        Id = essay.Id,
        Title = essay.Title,
        ImageUrl = essay.ImageUrl,
        OriginalOcrText = essay.OriginalOcrText,
        ContentText = essay.ContentText,
        ScannedAt = essay.CreatedDate,
    };

    private static EssayListItemResponse MapList(LearnerEssay essay) => new()
    {
        Id = essay.Id,
        Title = essay.Title,
        ImageUrl = essay.ImageUrl,
        ScannedAt = essay.CreatedDate,
        PreviewText = essay.ContentText.Length <= 140
            ? essay.ContentText
            : essay.ContentText[..140] + "…",
    };
}
