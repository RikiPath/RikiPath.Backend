namespace RikiPath.Application.Requests.PracticeTests
{
    public class SubmitAttemptRequest
    {
        public List<AnswerSubmissionItem> Answers { get; set; } = new();
    }
}
