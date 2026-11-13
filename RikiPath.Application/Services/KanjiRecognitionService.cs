using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;

namespace RikiPath.Application.Services;

public sealed class KanjiRecognitionService(IKanjiOcrClient ocr, ILogger<KanjiRecognitionService> logger)
    : IKanjiRecognitionService
{
    public async Task<ApiResponse<List<string>>> RecognizeAsync(IFormFile image, CancellationToken cancellationToken)
    {
        if (image is null || image.Length is <= 0 or > 1024 * 1024 || image.ContentType != "image/png")
            return ApiResponse<List<string>>.Fail("Hãy vẽ một chữ Kanji. Ảnh phải là PNG và không quá 1 MB.");

        try
        {
            await using var buffer = new MemoryStream();
            await image.CopyToAsync(buffer, cancellationToken);
            var text = await ocr.RecognizeAsync(Convert.ToBase64String(buffer.ToArray()), cancellationToken);
            var candidates = (text ?? string.Empty)
                .Where(c => c is >= '\u3400' and <= '\u4DBF' or >= '\u4E00' and <= '\u9FFF')
                .Select(c => c.ToString()).Distinct().Take(5).ToList();
            return ApiResponse<List<string>>.Success(candidates);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning("Kanji recognition failed ({ErrorType}).", ex.GetType().Name);
            return ApiResponse<List<string>>.Fail("Chưa nhận dạng được chữ. Hãy thử lại sau.", HttpStatusCode.ServiceUnavailable);
        }
    }
}
