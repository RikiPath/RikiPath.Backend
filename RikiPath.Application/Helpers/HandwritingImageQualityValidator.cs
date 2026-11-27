using SkiaSharp;

namespace RikiPath.Application.Helpers;

/// <summary>
/// Performs a cheap local quality gate before an uploaded handwriting image is sent to OCR.
/// This is deliberately deterministic so blurred/empty photos do not consume AI quota.
/// </summary>
public static class HandwritingImageQualityValidator
{
    private const int MinimumShortEdge = 480;
    private const long MinimumPixelCount = 300_000;
    private const int SampleEdge = 800;
    private const double MinimumSharpness = 6d;
    private const double MinimumContrast = 12d;
    // A normal sheet of paper is mostly white, so the extreme-tone checks
    // intentionally require almost every sampled pixel to be clipped.
    private const double NearlyBlackRatio = 0.995d;
    private const double NearlyWhiteRatio = 0.995d;

    public static HandwritingImageQualityResult Validate(
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes.Length == 0)
            return HandwritingImageQualityResult.Invalid(
                "HANDWRITING_IMAGE_EMPTY",
                "Ảnh trống. Vui lòng chụp lại bài viết.");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap is null)
            {
                return HandwritingImageQualityResult.Invalid(
                    "HANDWRITING_IMAGE_INVALID",
                    "Không đọc được ảnh. Vui lòng tải lên JPG, PNG hoặc WEBP hợp lệ.");
            }

            var width = bitmap.Width;
            var height = bitmap.Height;
            var shortEdge = Math.Min(width, height);
            if (shortEdge < MinimumShortEdge || (long)width * height < MinimumPixelCount)
            {
                return HandwritingImageQualityResult.Invalid(
                    "HANDWRITING_IMAGE_RESOLUTION_TOO_LOW",
                    $"Ảnh quá nhỏ ({width}x{height}). Vui lòng chụp ảnh có độ phân giải cao hơn.");
            }

            var sampleWidth = Math.Min(width, SampleEdge);
            var sampleHeight = Math.Min(height, SampleEdge);
            var luminance = new double[sampleHeight, sampleWidth];
            var sum = 0d;
            var sumSquares = 0d;
            var nearBlack = 0;
            var nearWhite = 0;
            for (var y = 0; y < sampleHeight; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceY = Math.Min(height - 1, y * height / sampleHeight);
                for (var x = 0; x < sampleWidth; x++)
                {
                    var sourceX = Math.Min(width - 1, x * width / sampleWidth);
                    var pixel = bitmap.GetPixel(sourceX, sourceY);
                    var value = 0.2126d * pixel.Red
                        + 0.7152d * pixel.Green
                        + 0.0722d * pixel.Blue;
                    luminance[y, x] = value;
                    sum += value;
                    sumSquares += value * value;
                    if (value <= 15d) nearBlack++;
                    if (value >= 245d) nearWhite++;
                }
            }

            var sampleCount = sampleWidth * sampleHeight;
            var mean = sum / sampleCount;
            var contrast = Math.Sqrt(Math.Max(0d, sumSquares / sampleCount - mean * mean));
            var blackRatio = (double)nearBlack / sampleCount;
            var whiteRatio = (double)nearWhite / sampleCount;

            if (mean < 15d || (blackRatio >= NearlyBlackRatio && contrast < MinimumContrast))
            {
                return HandwritingImageQualityResult.Invalid(
                    "HANDWRITING_IMAGE_TOO_DARK",
                    "Ảnh quá tối, không thể đọc rõ chữ. Vui lòng chụp lại ở nơi đủ sáng.", mean, 0d);
            }

            if (mean > 253d || (whiteRatio >= NearlyWhiteRatio && contrast < MinimumContrast))
            {
                return HandwritingImageQualityResult.Invalid(
                    "HANDWRITING_IMAGE_TOO_BRIGHT",
                    "Ảnh quá sáng hoặc bị lóa. Vui lòng tránh đèn phản chiếu và chụp lại.", mean, 0d);
            }

            if (contrast < MinimumContrast)
            {
                return HandwritingImageQualityResult.Invalid(
                    "HANDWRITING_IMAGE_LOW_CONTRAST",
                    "Ảnh thiếu tương phản nên không nhìn rõ chữ. Vui lòng chụp lại rõ hơn.", mean, 0d);
            }

            var sharpness = CalculateLaplacianVariance(luminance, sampleWidth, sampleHeight);
            if (sharpness < MinimumSharpness)
            {
                return HandwritingImageQualityResult.Invalid(
                    "HANDWRITING_IMAGE_TOO_BLURRY",
                    "Ảnh bị mờ hoặc rung. Vui lòng giữ điện thoại cố định và chụp lại rõ nét.",
                    mean, sharpness);
            }

            return HandwritingImageQualityResult.Valid(mean, contrast, sharpness);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (imageBytes.Length > 0)
        {
            return HandwritingImageQualityResult.Invalid(
                "HANDWRITING_IMAGE_INVALID",
                "Không đọc được ảnh. Vui lòng tải lên JPG, PNG hoặc WEBP hợp lệ.");
        }
    }

    public static bool ContainsUnreadableMarker(string text)
        => text.Contains("[không rõ]", StringComparison.OrdinalIgnoreCase)
            || text.Contains("[unclear]", StringComparison.OrdinalIgnoreCase)
            || text.Contains("[illegible]", StringComparison.OrdinalIgnoreCase);

    public static bool ContainsJapaneseText(string text)
        => text.Any(character => character is (>= '\u3040' and <= '\u30ff')
            or (>= '\u3400' and <= '\u4dbf')
            or (>= '\u4e00' and <= '\u9fff'));

    private static double CalculateLaplacianVariance(double[,] luminance, int width, int height)
    {
        if (width < 3 || height < 3) return 0d;

        var count = 0;
        var sum = 0d;
        var sumSquares = 0d;
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var center = luminance[y, x];
                var laplacian = luminance[y - 1, x] + luminance[y + 1, x]
                    + luminance[y, x - 1] + luminance[y, x + 1] - (4d * center);
                sum += laplacian;
                sumSquares += laplacian * laplacian;
                count++;
            }
        }

        return count == 0 ? 0d : Math.Max(0d, sumSquares / count - Math.Pow(sum / count, 2));
    }
}

public sealed record HandwritingImageQualityResult(
    bool IsValid,
    string? ErrorCode,
    string? ErrorMessage,
    double MeanLuminance,
    double Contrast,
    double Sharpness)
{
    public static HandwritingImageQualityResult Valid(double mean, double contrast, double sharpness)
        => new(true, null, null, mean, contrast, sharpness);

    public static HandwritingImageQualityResult Invalid(
        string errorCode,
        string errorMessage,
        double mean = 0d,
        double sharpness = 0d)
        => new(false, errorCode, errorMessage, mean, 0d, sharpness);
}
