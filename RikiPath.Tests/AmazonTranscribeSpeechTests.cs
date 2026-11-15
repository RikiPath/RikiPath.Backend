using RikiPath.Infrastructure.Options;
using Amazon.Runtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Infrastructure.Clients;
using RikiPath.WebApi.Controllers;

namespace RikiPath.Tests;

public sealed class AmazonTranscribeSpeechTests
{
    [Fact]
    public void Signed_session_uses_Japanese_PCM_and_short_expiry_without_exposing_secret()
    {
        var uri = TranscribeWebSocketSigner.Sign(new ImmutableCredentials("TESTKEY", "test-secret", "temp+token="), new(), DateTimeOffset.Parse("2026-11-09T00:00:00Z"));
        Assert.StartsWith("wss://transcribestreaming.ap-southeast-1.amazonaws.com:8443/", uri);
        Assert.Contains("language-code=ja-JP", uri);
        Assert.Contains("media-encoding=pcm", uri);
        Assert.Contains("sample-rate=16000", uri);
        Assert.Contains("X-Amz-Expires=60", uri);
        Assert.Contains("X-Amz-Date=20261109T000000Z", uri);
        Assert.Contains("temp%2Btoken%253D", uri);
        Assert.DoesNotContain("test-secret", uri);
        Assert.Matches(@"X-Amz-Signature=[a-f0-9]{64}$", uri);
    }

    [Theory]
    [InlineData("malicious.example.com", "ja-JP", 16000, 60)]
    [InlineData("ap-southeast-1", "en-US", 16000, 60)]
    [InlineData("ap-southeast-1", "ja-JP", 44100, 60)]
    [InlineData("ap-southeast-1", "ja-JP", 16000, 900)]
    public void Invalid_contract_cannot_generate_a_signed_endpoint(string region, string language, int rate, int expiry)
        => Assert.Throws<InvalidOperationException>(() => TranscribeWebSocketSigner.Sign(new("TEST", "SECRET", null),
            new() { Region = region, Language = language, SampleRate = rate, SessionUrlLifetimeSeconds = expiry }, DateTimeOffset.UtcNow));

    [Fact]
    public void Batch_result_keeps_text_and_recognition_confidence_without_treating_it_as_pronunciation()
    {
        var result = AmazonTranscribeSpeechClient.ParseTranscript("""
            {"results":{"transcripts":[{"transcript":"日本語です。"}],"items":[
              {"alternatives":[{"confidence":"0.9"}]},{"alternatives":[{"confidence":"0.7"}]},
              {"type":"punctuation","alternatives":[{"content":"。"}]}]}}
            """);
        Assert.Equal("日本語です。", result.TranscribedText);
        Assert.Equal(0.8, result.Confidence, 6);
        Assert.Contains("transcripts", result.RawResponseJson);
    }

    [Fact]
    public void Empty_transcript_is_an_error_instead_of_successful_empty_grading_input()
        => Assert.Throws<InvalidOperationException>(() => AmazonTranscribeSpeechClient.ParseTranscript("""{"results":{"transcripts":[{"transcript":""}],"items":[]}}"""));

    [Fact]
    public async Task Credential_failures_are_controlled_without_leaking_provider_secrets()
    {
        var transcribe = new Mock<ITranscribeSpeechClient>();
        transcribe.Setup(x => x.CreateStreamingSessionAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("SECRET credentials detail"));
        var service = new SpeechToTextService(Mock.Of<IAzureSpeechClient>(), new(), NullLogger<SpeechToTextService>.Instance, transcribe.Object);
        var result = await service.CreateStreamingSessionAsync(default);
        Assert.Equal(503, result.StatusCode);
        Assert.DoesNotContain("SECRET", result.ErrorMessage);
    }

    [Fact]
    public void Streaming_session_requires_Learner_and_rate_limit_while_mobile_legacy_route_is_retained()
    {
        var method = typeof(SpeechController).GetMethod(nameof(SpeechController.CreateStreamingSession))!;
        Assert.Equal("Learner", Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Roles);
        Assert.Single(method.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true));
        Assert.NotNull(typeof(SpeechController).GetMethod(nameof(SpeechController.GetToken)));
    }
}
