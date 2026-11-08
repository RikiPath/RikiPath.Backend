namespace RikiPath.Application.Responses.PracticeExercises
{
    public class StartPracticeAttemptResponse
    {
        public int AttemptId { get; init; }
        public int PracticeExerciseId { get; init; }
        public string Title { get; init; } = string.Empty;
        public DateTime StartedAt { get; init; }
        /// <summary>true nếu tiếp tục lượt làm bài chưa hoàn thành trước đó.</summary>
        public bool Resumed { get; init; }
        public List<PracticeQuestionResponse> Questions { get; init; } = new();
        public List<PracticeSavedAnswerResponse> SavedAnswers { get; init; } = new();
    }

    /// <summary>Câu hỏi gửi cho learner — cố ý KHÔNG chứa IsCorrect / Explanation.</summary>
    public class PracticeQuestionResponse
    {
        public int Id { get; init; }
        public string QuestionText { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public List<PracticeOptionResponse> Options { get; init; } = new();
    }

    public class PracticeOptionResponse
    {
        public int Id { get; init; }
        public string OptionText { get; init; } = string.Empty;
        public int SortOrder { get; init; }
    }

    public class PracticeSavedAnswerResponse
    {
        public int QuestionId { get; init; }
        public int? SelectedOptionId { get; init; }
        public string? TextAnswer { get; init; }
    }
}
