using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.Services;

namespace RikiPath.Tests;

public sealed class KanjiRecognitionTests
{
    private static IFormFile Image(string contentType = "image/png", int length = 8) =>
        new FormFile(new MemoryStream(new byte[length]), 0, length, "image", "kanji.png")
        { Headers = new HeaderDictionary(), ContentType = contentType };

    [Fact]
    public async Task Recognition_filters_non_kanji_and_duplicates_without_persisting_images()
    {
        var ocr = new Mock<IKanjiOcrClient>();
        ocr.Setup(x => x.RecognizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("水 水\n月 abc みず");
        var service = new KanjiRecognitionService(ocr.Object, NullLogger<KanjiRecognitionService>.Instance);
        var result = await service.RecognizeAsync(Image(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "水", "月" }, result.Result);
    }

    [Theory]
    [InlineData("image/jpeg", 8)]
    [InlineData("image/png", 0)]
    [InlineData("image/png", 1048577)]
    public async Task Invalid_drawing_never_calls_provider(string type, int length)
    {
        var ocr = new Mock<IKanjiOcrClient>(MockBehavior.Strict);
        var result = await new KanjiRecognitionService(ocr.Object, NullLogger<KanjiRecognitionService>.Instance)
            .RecognizeAsync(Image(type, length), CancellationToken.None);
        Assert.False(result.IsSuccess);
        ocr.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Provider_failure_does_not_expose_details()
    {
        var ocr = new Mock<IKanjiOcrClient>();
        ocr.Setup(x => x.RecognizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("provider-secret"));
        var result = await new KanjiRecognitionService(ocr.Object, NullLogger<KanjiRecognitionService>.Instance)
            .RecognizeAsync(Image(), CancellationToken.None);
        Assert.Equal(503, result.StatusCode);
        Assert.DoesNotContain("provider-secret", result.ErrorMessage);
    }
}
