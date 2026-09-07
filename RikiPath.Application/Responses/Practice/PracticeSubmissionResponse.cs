using Domain.Enums;
using RikiPath.Application.Responses.Grading;

namespace RikiPath.Application.Responses.Practice
{
    public class PracticeSubmissionResponse
    {
        public int Id { get; set; }
        public SubmissionType Type { get; set; }
        public string? TextContent { get; set; }
        public string? ImageUrl { get; set; }

        public int JlptLevelId { get; set; }
        public string JlptLevelName { get; set; } = string.Empty;

        public DateTime SubmittedAt { get; set; }
        public GradingResultResponse? Grading { get; set; }
    }
}
