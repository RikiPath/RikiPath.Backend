using Microsoft.AspNetCore.Http;

namespace RikiPath.Application.IServices
{
    public interface ISpeechToTextService
    {
        Task<string> ConvertAudioToTextAsync(IFormFile audioFile);
    }
}
