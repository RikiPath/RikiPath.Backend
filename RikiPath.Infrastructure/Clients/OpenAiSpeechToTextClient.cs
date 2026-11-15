using System.Net.Http.Headers;
using System.Text.Json;
using RikiPath.Application.IClients;
using RikiPath.Application.Responses.SpeechToText;

namespace RikiPath.Infrastructure.Clients;

// The legacy interface remains for existing practice submission callers.
public sealed class OpenAiSpeechToTextClient(OpenAiClient client, IHttpClientFactory httpFactory) : ISpeechRecognitionClient
{
    public Task<string> IssueTokenAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Dùng POST /api/speech/transcribe để gửi audio tới backend OpenAI.");

    public async Task<SpeechToTextResult> TranscribeFromUrlAsync(string audioUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new ArgumentException("audioUrl phải là URL HTTP/HTTPS hợp lệ.", nameof(audioUrl));
        using var download = httpFactory.CreateClient();
        using var response = await download.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 25 * 1024 * 1024)
            throw new ArgumentException("File audio tối đa 25MB.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var text = await TranscribeAsync(stream, Path.GetFileName(uri.LocalPath), cancellationToken);
        // OpenAI does not return Azure's confidence score. Zero means unavailable.
        return new SpeechToTextResult(text, 0, JsonSerializer.Serialize(new { text }));
    }

    public async Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await audio.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + count > 25 * 1024 * 1024)
                throw new ArgumentException("File audio tối đa 25MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".wav" or ".mp3" or ".mp4" or ".m4a" or ".webm" or ".mpeg" or ".mpga"))
            throw new ArgumentException("Định dạng audio không được hỗ trợ. Dùng WAV, MP3, MP4, M4A hoặc WEBM.");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(client.Settings.TranscriptionModel), "model");
        form.Add(new StringContent("ja"), "language");
        var file = new ByteArrayContent(buffer.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "audio" + extension);
        using var request = client.CreateRequest("audio/transcriptions", form);
        var raw = await client.SendAsync(request, cancellationToken);
        using var document = JsonDocument.Parse(raw);
        return document.RootElement.GetProperty("text").GetString() ?? string.Empty;
    }
}
