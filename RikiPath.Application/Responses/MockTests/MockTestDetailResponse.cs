using System.Collections.Generic;

namespace RikiPath.Application.Responses.MockTests
{
    /// <summary>
    /// Toàn bộ nội dung một bài test để học viên làm bài, câu hỏi được
    /// nhóm theo từng Section. KHÔNG chứa đáp án đúng (IsCorrect/AnswerKey/Explanation)
    /// để tránh lộ đáp án qua response khi học viên đang làm bài.
    /// </summary>
    public class MockTestDetailResponse
    {
        public int MockTestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string JlptLevelName { get; set; } = string.Empty;
        public int TimeLimitMinutes { get; set; }
        public List<MockTestSectionResponse> Sections { get; set; } = new();
    }

    public class MockTestSectionResponse
    {
        public int SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string LanguageSkillName { get; set; } = string.Empty;
        public List<MockQuestionResponse> Questions { get; set; } = new();
    }

    public class MockQuestionResponse
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string? InteractionDataJson { get; set; }
        public string? AudioUrl { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public List<MockQuestionOptionResponse> Options { get; set; } = new();
    }

    public class MockQuestionOptionResponse
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        // Cố ý KHÔNG có IsCorrect — tránh lộ đáp án.
    }
}