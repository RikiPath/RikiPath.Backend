namespace RikiPath.Application.Requests.ContentAuthoring
{
    /// <summary>Các loại công việc AI có thể được giao trong một kế hoạch. Thứ tự giá trị = thứ tự thực thi.</summary>
    public enum ContentTaskType
    {
        Kanji = 1,
        Vocabulary = 2,
        Grammar = 3,
        Lesson = 4,
        PracticeExercise = 5,
        MockTest = 6
    }

    // =====================================================================================
    // BƯỚC 1 — PLAN
    // =====================================================================================

    /// <summary>Author viết một yêu cầu tự do; AI chỉ chuyển thành danh sách công việc, chưa sinh nội dung.</summary>
    public class PlanContentRequest
    {
        public string Prompt { get; set; } = string.Empty;
    }

    /// <summary>Một công việc trong kế hoạch. Author có thể sửa/xóa/thêm trước khi chạy.</summary>
    public class ContentPlanTask
    {
        /// <summary>Định danh trong kế hoạch (t1, t2...). Dùng để khớp kết quả và tham chiếu giữa các công việc.</summary>
        public string TaskId { get; set; } = string.Empty;

        public ContentTaskType Type { get; set; }

        /// <summary>Yêu cầu chi tiết, tự đủ ý (chủ đề, cấp độ, lưu ý riêng của author) cho công việc này.</summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>Kanji/Vocabulary/Grammar: số mục. PracticeExercise: số câu hỏi. Lesson/MockTest: bỏ qua.</summary>
        public int Count { get; set; }

        public int? CertificateLevelId { get; set; }
        public int? LanguageSkillId { get; set; }

        /// <summary>
        /// PracticeExercise: gắn vào bài học ĐÃ CÓ trong hệ thống.
        /// Kanji/Vocabulary/Grammar: bổ sung nội dung cho bài học ĐÃ CÓ (cấp độ lấy theo bài học đó).
        /// </summary>
        public int? ExistingLessonId { get; set; }

        /// <summary>PracticeExercise: gắn vào bài học (Lesson task) sẽ được tạo trong chính kế hoạch này.</summary>
        public string? LessonTaskId { get; set; }

        /// <summary>MockTest: các phần thi cần sinh.</summary>
        public List<MockTestSectionPlan>? MockSections { get; set; }
    }

    // =====================================================================================
    // BƯỚC 2 — EXECUTE
    // =====================================================================================

    /// <summary>Kế hoạch (có thể đã được author chỉnh sửa) gửi lại để chạy.</summary>
    public class ExecuteContentPlanRequest
    {
        public List<ContentPlanTask> Tasks { get; set; } = new();
    }

    // =====================================================================================
    // BƯỚC 3 — CONFIRM (một lần cho cả gói, cùng một BatchId)
    // =====================================================================================

    public class ConfirmContentPlanRequest
    {
        public List<ConfirmPlanKanjiGroup> KanjiGroups { get; set; } = new();
        public List<ConfirmPlanVocabularyGroup> VocabularyGroups { get; set; } = new();
        public List<ConfirmPlanGrammarGroup> GrammarGroups { get; set; } = new();
        public List<ConfirmPlanLesson> Lessons { get; set; } = new();
        public List<ConfirmPlanPracticeExercise> PracticeExercises { get; set; } = new();
        public List<ConfirmMockTestRequest> MockTests { get; set; } = new();
    }

    public class ConfirmPlanKanjiGroup
    {
        /// <summary>Cấp độ để map nội dung (ContentLevelMapping) khi admin duyệt. Null = không map.</summary>
        public int? CertificateLevelId { get; set; }

        /// <summary>Bài học ĐÃ CÓ mà nhóm này được bổ sung vào (lấy từ ExistingLessonId của công việc). Null = nội dung mới của gói.</summary>
        public int? LessonId { get; set; }
        public List<ConfirmKanjiItem> Items { get; set; } = new();
    }

    public class ConfirmPlanVocabularyGroup
    {
        public int? CertificateLevelId { get; set; }
        public int? LessonId { get; set; }
        public List<ConfirmVocabularyItem> Items { get; set; } = new();
    }

    public class ConfirmPlanGrammarGroup
    {
        public int? CertificateLevelId { get; set; }
        public int? LessonId { get; set; }
        public List<ConfirmGrammarItem> Items { get; set; } = new();
    }

    public class PlanVocabularyRef
    {
        public string Word { get; set; } = string.Empty;
        public string Reading { get; set; } = string.Empty;
    }

    /// <summary>
    /// Bài học trong gói. Liên kết tới nội dung ĐÃ CÓ bằng Id; liên kết tới nội dung MỚI (cùng gói, chưa có Id)
    /// bằng khóa tự nhiên: ký tự Kanji / Word+Reading / Title ngữ pháp.
    /// </summary>
    public class ConfirmPlanLesson
    {
        public int CertificateLevelId { get; set; }
        public int LanguageSkillId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }

        public List<int> KanjiIds { get; set; } = new();
        public List<int> VocabularyIds { get; set; } = new();
        public List<int> GrammarPatternIds { get; set; } = new();

        public List<string> NewKanji { get; set; } = new();
        public List<PlanVocabularyRef> NewVocabulary { get; set; } = new();
        public List<string> NewGrammar { get; set; } = new();
    }

    /// <summary>Bài luyện tập: gắn bài học đã có (ExistingLessonId) HOẶC bài học mới cùng gói (NewLessonTitle) — chọn đúng một.</summary>
    public class ConfirmPlanPracticeExercise
    {
        public int? ExistingLessonId { get; set; }
        public string? NewLessonTitle { get; set; }
        public int LanguageSkillId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<ChoiceQuestionDraft> Questions { get; set; } = new();
    }
}