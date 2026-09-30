using RikiPath.Application.DTOs.Content;

namespace RikiPath.Application.Requests.Content
{
    public class BulkImportRequest
    {
        public Stream FileStream { get; set; } = null!;
    }
}
