using RikiPath.Application.DTOs.ContentAuthoring;
using RikiPath.Application.Responses;

namespace RikiPath.Application.IServices;

public enum QuestionAiAction
{
    GenerateQuestions = 1,
    SuggestOptions = 2,
    SuggestDistractors = 3,
    SuggestExplanation = 4,
    RewriteQuestion = 5,
    ImproveClarity = 6,
    DetectAmbiguity = 7,
    SuggestLevelAndType = 8,
    CheckLanguageQuality = 9,
    SuggestReadingQuestionsFromPassage = 10,
    SuggestListeningQuestionsFromTranscript = 11,
}

public sealed class QuestionAiAssistRequest
{
    public QuestionAiAction Action { get; set; } = QuestionAiAction.GenerateQuestions;
    public string CertificateLevelCode { get; set; } = "N5";
    public string SkillName { get; set; } = "Vocabulary";
    public string? TemplateKind { get; set; }
    public string? QuestionType { get; set; }
    public string? Topic { get; set; }
    public string? Difficulty { get; set; }
    public int DesiredCount { get; set; } = 3;
    public string? AuthorPrompt { get; set; }
    public CanonicalQuestionDto? CurrentQuestion { get; set; }
    public string? PassageText { get; set; }
    public string? PassageTitle { get; set; }
    public string? ListeningTranscript { get; set; }
}

public sealed class AiQuestionSuggestionItem
{
    public CanonicalQuestionDto Question { get; set; } = new();
    public CanonicalValidationResult Validation { get; set; } = new();
    public List<DuplicateMatchDto> DuplicateMatches { get; set; } = [];
    public string? Rationale { get; set; }
}

public sealed class AiQualityIssueDto
{
    public string Field { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Message { get; set; } = string.Empty;
    public string? SuggestedFix { get; set; }
}

public sealed class QuestionAiAssistResponse
{
    public QuestionAiAction Action { get; set; }
    public CanonicalQuestionDto? Before { get; set; }
    public List<AiQuestionSuggestionItem> Suggestions { get; set; } = [];
    public List<AiQualityIssueDto> AnalysisIssues { get; set; } = [];
    public string? SuggestedLevelCode { get; set; }
    public string? SuggestedTemplateKind { get; set; }
    public string? SuggestedQuestionType { get; set; }
    public string? Summary { get; set; }
    public AiProvenanceDto Provenance { get; set; } = new();
}

public interface IQuestionAiAssistantService
{
    Task<ApiResponse<QuestionAiAssistResponse>> AssistAsync(
        QuestionAiAssistRequest request, int actorUserId, CancellationToken ct);
}
