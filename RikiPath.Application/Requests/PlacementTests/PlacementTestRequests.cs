using RikiPath.Application.Requests.MockTests;

namespace RikiPath.Application.Requests.PlacementTests
{
    public class StartPlacementTestRequest
    {
        public int MockTestId { get; set; }
    }

    public class SubmitPlacementTestRequest
    {
        public List<AnswerSubmissionItem> Answers { get; set; } = new();
    }
}
