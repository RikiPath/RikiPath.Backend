using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Essay;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services;

public class LearnerEssayService(
    IUnitOfWork unitOfWork,
    IClaimService claimService,
    IFileStorageService fileStorage,
    IGeminiOcrClient geminiOcrClient) : ILearnerEssayService
{
    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp"];
    private const long MaxImageSizeBytes = 10 * 1024 * 1024;

    // Prompt chuyên sâu cho OCR chữ viết tay tiếng Nhật (作文/Sakubun)
    private const string JapaneseOcrPrompt = @"
You are a specialized OCR system for handwritten Japanese text extraction.

STRICT INSTRUCTIONS:
1. Extract ONLY HANDWRITTEN Japanese characters written by the learner.
2. Completely IGNORE and EXCLUDE all PRINTED text (e.g., printed questions, task instructions, section headers, page numbers, grid lines, or pre-printed form elements).
3. If both printed and handwritten Japanese text exist on the page, DO NOT include any word from the printed text.
4. Preserve the natural line breaks and order of the handwritten text.
5. Output ONLY the extracted handwritten text. Do NOT add any introduction, explanation, markdown formatting, or extra commentary.
";

    public async Task<ApiResponse<EssayScanResponse>> ScanAsync(
        IFormFile image,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateImage(image);
        if (validation is not null)
            return ApiResponse<EssayScanResponse>.Fail(validation);

        try
        {
            // 1. Chuyển đổi file ảnh sang Base64
            var (base64Image, mimeType) = await ConvertFormFileToBase64Async(image, cancellationToken);

            // 2. Gọi Gemini AI OCR Client
            var text = await geminiOcrClient.ScanHandwritingAsync(
                JapaneseOcrPrompt, base64Image, mimeType, cancellationToken);

            if (string.IsNullOrWhiteSpace(text))
                return ApiResponse<EssayScanResponse>.Fail(
                    "Không nhận diện được chữ tiếng Nhật trong ảnh. Hãy dùng ảnh rõ hơn và chụp đủ sáng.");

            // 3. Trả về kết quả OCR preview (không lưu DB / không upload Cloud)
            var response = new EssayScanResponse
            {

                Text = text,
                ScannedAt = DateTime.UtcNow,
            };

            return ApiResponse<EssayScanResponse>.Success(response);
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
            // 1. Chuyển đổi file ảnh sang Base64
            var (base64Image, mimeType) = await ConvertFormFileToBase64Async(image, cancellationToken);

            // 2. Scan lại bằng Gemini AI OCR Client
            var text = await geminiOcrClient.ScanHandwritingAsync(
                JapaneseOcrPrompt, base64Image, mimeType, cancellationToken);

            if (string.IsNullOrWhiteSpace(text))
                return ApiResponse<EssayScanResponse>.Fail(
                    "Không nhận diện được chữ tiếng Nhật trong ảnh. Hãy dùng ảnh rõ hơn và chụp đủ sáng.");

            // 3. Upload ảnh mới
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
                // File cũ xóa thất bại có thể dọn dẹp sau
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

    private static async Task<(string Base64, string MimeType)> ConvertFormFileToBase64Async(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();
        var base64 = Convert.ToBase64String(bytes);
        var mimeType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;
        return (base64, mimeType);
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
        Text = essay.OriginalOcrText,
        ScannedAt = essay.CreatedDate,
    };

    private static EssayDetailResponse MapDetail(LearnerEssay essay) => new()
    {
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