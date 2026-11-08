namespace RikiPath.Application.Requests.PracticeExercises;

public class SubmitPracticeExerciseAttemptRequest
{
    public List<PracticeExerciseAnswerRequest> Answers { get; set; } = new();
}

public class PracticeExerciseAnswerRequest
{
    public int QuestionId { get; set; }
    public int? SelectedOptionId { get; set; }
}
