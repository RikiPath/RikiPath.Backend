using RikiPath.Application.Requests.ContentAuthoring;

namespace RikiPath.Application.Responses.ContentAuthoring
{
    /// <summary>Kế hoạch AI đề xuất. Nếu ClarifyingQuestion khác null thì Tasks rỗng: cần author trả lời rồi gửi lại yêu cầu.</summary>
    public class ContentPlanResponse
    {
        public string Summary { get; set; } = string.Empty;
        public string? ClarifyingQuestion { get; set; }
        public List<ContentPlanTask> Tasks { get; set; } = new();
    }

    public enum ContentTaskStatus
    {
        Succeeded = 1,
        Failed = 2,
        Skipped = 3
    }

    /// <summary>Kết quả của MỘT công việc. Chỉ đúng một thuộc tính nội dung được điền, tùy theo Type.</summary>
    public class ContentTaskResult
    {
        public string TaskId { get; set; } = string.Empty;
        public ContentTaskType Type { get; set; }
        public ContentTaskStatus Status { get; set; }
        public string? Error { get; set; }

        /// <summary>Cấp độ của công việc — FE gửi lại khi Confirm để map nội dung vào cấp độ.</summary>
        public int? CertificateLevelId { get; set; }

        /// <summary>Kanji/Vocabulary/Grammar bổ sung cho bài học ĐÃ CÓ: FE gửi lại làm LessonId của nhóm khi Confirm.</summary>
        public int? ExistingLessonId { get; set; }

        public List<KanjiSuggestionItem>? Kanji { get; set; }
        public List<VocabularySuggestionItem>? Vocabulary { get; set; }
        public List<GrammarSuggestionItem>? Grammar { get; set; }
        public SuggestLessonResponse? Lesson { get; set; }
        public PlanPracticeExerciseDraft? PracticeExercise { get; set; }
        public SuggestMockTestResponse? MockTest { get; set; }
    }

    /// <summary>Bản nháp bài luyện tập; gắn với bài học đã có (ExistingLessonId) hoặc bài học mới cùng kế hoạch (NewLessonTitle).</summary>
    public class PlanPracticeExerciseDraft
    {
        public int? ExistingLessonId { get; set; }
        public string? NewLessonTitle { get; set; }
        public int LanguageSkillId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<ChoiceQuestionDraft> Questions { get; set; } = new();
    }

    public class ExecuteContentPlanResponse
    {
        public List<ContentTaskResult> Results { get; set; } = new();
        public int FailedCount => Results.Count(r => r.Status == ContentTaskStatus.Failed);
        public int SkippedCount => Results.Count(r => r.Status == ContentTaskStatus.Skipped);
    }
}