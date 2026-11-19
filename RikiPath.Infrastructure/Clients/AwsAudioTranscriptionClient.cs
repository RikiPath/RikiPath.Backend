using Amazon;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses.SpeechToText;
using RikiPath.Application.Services;
using RikiPath.Domain;

namespace RikiPath.Infrastructure.Clients;

public sealed class AwsAudioTranscriptionClient(
    AppSettings settings, IAwsAudioFileStorageService files, IHttpClientFactory http,
    ILogger<AwsAudioTranscriptionClient> logger) : IContentAudioTranscriber, ISpeechRecognitionClient
{
    public bool IsConfigured => files.IsConfigured;
    public int TimeoutSeconds => Math.Clamp(settings.AwsAudio.JobTimeoutSeconds, 30, 3600);

    public Task<string> IssueTokenAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Bản thu âm được nhận dạng qua backend AWS.");

    public Task<SpeechToTextResult> TranscribeListeningAsync(string audioUrl, CancellationToken ct)
        => TranscribeFromUrlAsync(audioUrl, ct);

    public async Task<SpeechToTextResult> TranscribeFromUrlAsync(string audioUrl, CancellationToken ct)
    {
        if (files.TryGetObjectKey(audioUrl, out var key))
            return await TranscribeObjectAsync(files.S3Reference(key), ct);

        // Read existing Supabase references through the configured origin while they are migrated.
        if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !Uri.TryCreate(settings.Supabase?.Url, UriKind.Absolute, out var legacy) ||
            uri.GetLeftPart(UriPartial.Authority) != legacy.GetLeftPart(UriPartial.Authority) ||
            string.IsNullOrWhiteSpace(settings.Supabase?.Bucket) || uri.Query.Length != 0 ||
            uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 ||
            !uri.AbsolutePath.StartsWith("/storage/v1/object/public/" +
                Uri.EscapeDataString(settings.Supabase.Bucket) + "/", StringComparison.Ordinal))
            throw new ArgumentException("Audio không thuộc kho lưu trữ của hệ thống.");
        using var response = await http.CreateClient("TranscribeAudioDownload")
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > ListeningAudioFilePolicy.MaxBytes)
            throw new ArgumentException("File audio tối đa 25 MB.");
        await using var audio = await response.Content.ReadAsStreamAsync(ct);
        return await TranscribeTemporaryAsync(audio, Path.GetFileName(uri.AbsolutePath), ct);
    }

    public async Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken ct) =>
        (await TranscribeTemporaryAsync(audio, fileName, ct)).TranscribedText;

    private async Task<SpeechToTextResult> TranscribeTemporaryAsync(Stream audio, string fileName, CancellationToken ct)
    {
        var uploaded = await files.UploadAsync(audio, fileName, null, false, ct);
        try { return await TranscribeObjectAsync(uploaded.Url, ct); }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await files.DeleteAsync(uploaded.Url, cleanup.Token); }
            catch { logger.LogWarning("AWS temporary audio cleanup failed."); }
        }
    }

    private async Task<SpeechToTextResult> TranscribeObjectAsync(string reference, CancellationToken ct)
    {
        if (!files.TryGetObjectKey(reference, out var key)) throw new ArgumentException("Đường dẫn AWS audio không hợp lệ.");
        var config = settings.AwsAudio;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        var token = timeout.Token;
        using var transcribe = new AmazonTranscribeServiceClient(AwsAudioCredentials.Resolve(config),
            RegionEndpoint.GetBySystemName(config.Region));
        var jobName = "rikipath-audio-" + Guid.NewGuid().ToString("N");
        var started = false;
        try
        {
            await transcribe.StartTranscriptionJobAsync(new StartTranscriptionJobRequest
            {
                TranscriptionJobName = jobName, LanguageCode = LanguageCode.JaJP,
                MediaFormat = Path.GetExtension(key).TrimStart('.').ToLowerInvariant(),
                Media = new Media { MediaFileUri = files.S3Reference(key) }
            }, token);
            started = true;
            while (true)
            {
                var response = await transcribe.GetTranscriptionJobAsync(new GetTranscriptionJobRequest
                    { TranscriptionJobName = jobName }, token);
                var job = response.TranscriptionJob;
                if (job.TranscriptionJobStatus == TranscriptionJobStatus.FAILED)
                    throw new InvalidOperationException("AWS chưa nhận dạng được bản thu tiếng Nhật.");
                if (job.TranscriptionJobStatus == TranscriptionJobStatus.COMPLETED)
                {
                    if (!Uri.TryCreate(job.Transcript?.TranscriptFileUri, UriKind.Absolute, out var uri) ||
                        uri.Scheme != "https") throw new InvalidOperationException("AWS trả bản chép lời không hợp lệ.");
                    var json = await http.CreateClient("TranscribeTranscriptDownload").GetStringAsync(uri, token);
                    return AmazonTranscribeSpeechClient.ParseTranscript(json);
                }
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
        }
        finally
        {
            if (started)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await transcribe.DeleteTranscriptionJobAsync(new DeleteTranscriptionJobRequest
                    { TranscriptionJobName = jobName }, cleanup.Token); }
                catch { logger.LogWarning("AWS audio transcription job cleanup failed."); }
            }
        }
    }
}
