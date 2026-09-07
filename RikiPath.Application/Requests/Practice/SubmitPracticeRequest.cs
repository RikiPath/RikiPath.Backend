using Domain.Enums;

namespace RikiPath.Application.Requests.Practice
{
    public class SubmitPracticeRequest
    {
        public SubmissionType Type { get; set; }
        public string? TextContent { get; set; }
        public string? ImageUrl { get; set; }

        public int JlptLevelId { get; set; }
    }
}
