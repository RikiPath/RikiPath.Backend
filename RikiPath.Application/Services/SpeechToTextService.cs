using Microsoft.AspNetCore.Http;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.SpeechToText;
using System.Net;

namespace RikiPath.Application.Services;

public class SpeechToTextService(ISpeechRecognitionClient speechClient) : ISpeechToTextService
{
    public async Task<string> ConvertAudioToTextAsync(IFormFile audioFile, CancellationToken cancellationToken = default)
    {
        if (audioFile.Length == 0 || audioFile.Length > 25 * 1024 * 1024)
            throw new ArgumentException("File audio phải có dữ liệu và không vượt quá 25MB.");
        await using var stream = audioFile.OpenReadStream();
        return await speechClient.TranscribeAsync(stream, audioFile.FileName, cancellationToken);
    }

    public Task<ApiResponse<SpeechTokenResponse>> GetTokenAsync(CancellationToken cancellationToken)
        => Task.FromResult(ApiResponse<SpeechTokenResponse>.Fail(
            "Đã chuyển sang OpenAI. Gửi file audio tới POST /api/speech/transcribe thay cho Azure Speech SDK.",
            HttpStatusCode.Gone));
}
