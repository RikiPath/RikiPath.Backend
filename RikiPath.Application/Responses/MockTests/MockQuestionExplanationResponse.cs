namespace RikiPath.Application.Responses.MockTests;

public sealed record MockQuestionExplanationResponse(int QuestionId, int Score, int MaxScore,
    string Explanation, IReadOnlyList<MockOptionExplanation> Options);

public sealed record MockOptionExplanation(int OptionId, bool Selected, bool IsCorrect, string Reason);
