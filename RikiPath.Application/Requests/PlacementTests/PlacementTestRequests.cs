using RikiPath.Application.Requests.MockTests;

namespace RikiPath.Application.Requests.PlacementTests
{
    public class StartPlacementTestRequest
    {
        public int? MaxQuestionsPerLevel { get; set; }
    }

    public class SubmitPlacementTestRequest
    {
        public List<AnswerSubmissionItem> Answers { get; set; } = new();
    }
}
