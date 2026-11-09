namespace RikiPath.Application.Requests.PracticeExercises
{
    public class SubmitPracticeAttemptRequest
    {
        public List<SubmitPracticeAnswerRequest> Answers { get; set; } = new();
    }

    public class SubmitPracticeAnswerRequest
    {
        public int QuestionId { get; set; }
        public int? SelectedOptionId { get; set; }
        public string? TextAnswer { get; set; }
    }
}
