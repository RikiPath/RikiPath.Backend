

namespace RikiPath.Application.Responses.Content
{
    public class BulkImportResultResponse
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }

        public List<string> Errors { get; set; } = new List<string>();
    }
}
