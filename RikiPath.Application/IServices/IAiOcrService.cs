using Microsoft.AspNetCore.Http;

namespace RikiPath.Application.IServices
{
    public interface IAiOcrService
    {
        Task<string> ScanJapaneseHandwritingAsync(IFormFile file);
    }
}
