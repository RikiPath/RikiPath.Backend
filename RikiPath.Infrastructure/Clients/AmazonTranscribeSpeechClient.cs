using RikiPath.Infrastructure.Options;
using System.Globalization;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RikiPath.Application.IClients;
using RikiPath.Application.Responses.SpeechToText;
using RikiPath.Domain;

namespace RikiPath.Infrastructure.Clients;

/// <summary>Streaming for the browser and batch transcription for previously uploaded practice audio.</summary>
public sealed class AmazonTranscribeSpeechClient(
    IHttpClientFactory httpClients,
    IOptions<AmazonTranscribeSettings> options,
    AppSettings appSettings,
    ILogger<AmazonTranscribeSpeechClient> logger) : ITranscribeSpeechClient
{
    private const int MaxAudioBytes = 10 * 1024 * 1024;

    public async Task<SpeechStreamingSessionResponse> CreateStreamingSessionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = options.Value;
        var credentials = await ResolveCredentials(settings).GetCredentialsAsync();
        var url = TranscribeWebSocketSigner.Sign(credentials, settings, DateTimeOffset.UtcNow);
        return new("AmazonTranscribe", url, settings.Language, settings.SampleRate, "pcm", settings.SessionUrlLifetimeSeconds);
    }

    public async Task<SpeechToTextResult> TranscribeFromUrlAsync(string audioUrl, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.AudioBucket))
            throw new InvalidOperationException("AmazonTranscribe:AudioBucket is required for saved audio files.");
        // Submitted URLs must refer to our configured storage, never arbitrary hosts or redirects.
        if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !Uri.TryCreate(appSettings.Supabase?.Url, UriKind.Absolute, out var storageUri) ||
            uri.GetLeftPart(UriPartial.Authority) != storageUri.GetLeftPart(UriPartial.Authority) ||
            !uri.AbsolutePath.StartsWith("/storage/v1/object/", StringComparison.Ordinal))
            throw new ArgumentException("Audio must be an HTTPS object from the configured storage.", nameof(audioUrl));
        var format = Path.GetExtension(uri.AbsolutePath).TrimStart('.').ToLowerInvariant();
        if (!new[] { "mp3", "mp4", "wav", "flac", "ogg", "amr", "webm", "m4a" }.Contains(format))
            throw new ArgumentException("Unsupported audio format.", nameof(audioUrl));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.JobTimeoutSeconds, 15, 300)));
        var ct = timeout.Token;
        using var download = await httpClients.CreateClient("TranscribeAudioDownload")
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        download.EnsureSuccessStatusCode();
        if (download.Content.Headers.ContentLength > MaxAudioBytes)
            throw new ArgumentException("Audio exceeds 10 MB.", nameof(audioUrl));
        await using var audio = new MemoryStream();
        await using (var input = await download.Content.ReadAsStreamAsync(ct))
        {
            var buffer = new byte[81920];
            int length;
            while ((length = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (audio.Length + length > MaxAudioBytes) throw new ArgumentException("Audio exceeds 10 MB.");
                await audio.WriteAsync(buffer.AsMemory(0, length), ct);
            }
        }
        if (audio.Length == 0) throw new ArgumentException("Empty audio.");
        audio.Position = 0;

        var credentials = ResolveCredentials(settings);
        var region = RegionEndpoint.GetBySystemName(settings.Region);
        using var s3 = new AmazonS3Client(credentials, region);
        using var transcribe = new AmazonTranscribeServiceClient(credentials, region);
        var jobName = "rikipath-" + Guid.NewGuid().ToString("N");
        var key = settings.AudioPrefix.Trim('/') + "/" + jobName + "." + format;
        var uploaded = false;
        var started = false;
        try
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = settings.AudioBucket, Key = key, InputStream = audio,
                AutoCloseStream = false, ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
            }, ct);
            uploaded = true;
            await transcribe.StartTranscriptionJobAsync(new StartTranscriptionJobRequest
            {
                TranscriptionJobName = jobName,
                LanguageCode = settings.Language,
                MediaFormat = format,
                Media = new Media { MediaFileUri = $"s3://{settings.AudioBucket}/{key}" },
            }, ct);
            started = true;
            while (true)
            {
                var response = await transcribe.GetTranscriptionJobAsync(new GetTranscriptionJobRequest { TranscriptionJobName = jobName }, ct);
                var job = response.TranscriptionJob;
                if (job.TranscriptionJobStatus == TranscriptionJobStatus.FAILED)
                    throw new InvalidOperationException("Amazon Transcribe could not transcribe the audio.");
                if (job.TranscriptionJobStatus == TranscriptionJobStatus.COMPLETED)
                {
                    var transcriptUri = new Uri(job.Transcript.TranscriptFileUri);
                    if (transcriptUri.Scheme != "https") throw new InvalidOperationException("Invalid transcript URL.");
                    var json = await httpClients.CreateClient("TranscribeTranscriptDownload").GetStringAsync(transcriptUri, ct);
                    return ParseTranscript(json);
                }
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (started)
            {
                try { await transcribe.DeleteTranscriptionJobAsync(new DeleteTranscriptionJobRequest { TranscriptionJobName = jobName }, cleanup.Token); }
                catch { logger.LogWarning("A temporary Transcribe job could not be deleted; check speech-job retention."); }
            }
            if (uploaded)
            {
                try { await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = settings.AudioBucket, Key = key }, cleanup.Token); }
                catch { logger.LogWarning("Temporary speech audio cleanup failed; check the S3 lifecycle rule."); }
            }
        }
    }

    private static AWSCredentials ResolveCredentials(AmazonTranscribeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AccessKeyId) &&
            string.IsNullOrWhiteSpace(settings.SecretAccessKey) &&
            string.IsNullOrWhiteSpace(settings.SessionToken))
            return FallbackCredentialsFactory.GetCredentials();

        if (string.IsNullOrWhiteSpace(settings.AccessKeyId) || string.IsNullOrWhiteSpace(settings.SecretAccessKey))
            throw new InvalidOperationException("Both AmazonTranscribe credentials must be configured.");

        return string.IsNullOrWhiteSpace(settings.SessionToken)
            ? new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey)
            : new SessionAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey, settings.SessionToken);
    }

    public static SpeechToTextResult ParseTranscript(string json)
    {
        using var document = JsonDocument.Parse(json);
        var results = document.RootElement.GetProperty("results");
        var text = string.Join(" ", results.GetProperty("transcripts").EnumerateArray()
            .Select(x => x.GetProperty("transcript").GetString()).Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("No speech was recognized.");
        var confidences = new List<double>();
        if (results.TryGetProperty("items", out var items))
            foreach (var item in items.EnumerateArray())
                if (item.TryGetProperty("alternatives", out var alternatives) && alternatives.GetArrayLength() > 0 &&
                    alternatives[0].TryGetProperty("confidence", out var confidence) &&
                    double.TryParse(confidence.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    confidences.Add(value);
        // Recognition confidence is not a pronunciation assessment.
        return new(text, confidences.Count == 0 ? 0 : confidences.Average(), json);
    }
}
