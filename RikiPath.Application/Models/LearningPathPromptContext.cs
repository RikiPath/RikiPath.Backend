namespace RikiPath.Application.Models
{
    public readonly struct LearningPathPromptContext(
    string targetJlptLevel,
    IReadOnlyList<(string LessonTitle, int ProgressPercent)> lessonProgress,
    IReadOnlyList<string> notebookWords,
    double? latestTestScorePercent,
    IReadOnlyDictionary<string, double>? latestTestSkillBreakdown)
    {
        public string TargetJlptLevel { get; } = targetJlptLevel;
        public IReadOnlyList<(string LessonTitle, int ProgressPercent)> LessonProgress { get; } = lessonProgress;
        public IReadOnlyList<string> NotebookWords { get; } = notebookWords;
        public double? LatestTestScorePercent { get; } = latestTestScorePercent;
        public IReadOnlyDictionary<string, double>? LatestTestSkillBreakdown { get; } = latestTestSkillBreakdown;

        public string ToPromptText()
        {
            var lessonSummary = LessonProgress.Count == 0
                ? "Chưa học bài nào."
                : string.Join("; ", LessonProgress.Select(l => $"{l.LessonTitle} ({l.ProgressPercent}%)"));

            var wordSummary = NotebookWords.Count == 0
                ? "Sổ tay trống."
                : string.Join(", ", NotebookWords.Take(50)); // giới hạn tránh prompt quá dài

            var skillSummary = LatestTestSkillBreakdown is { Count: > 0 }
                ? string.Join("; ", LatestTestSkillBreakdown.Select(kv => $"{kv.Key}: {kv.Value:F0}%"))
                : "Chưa có dữ liệu bài thi thử.";

            return $$"""
            Bạn là cố vấn học tiếng Nhật cho một học viên đang luyện thi JLPT {TargetJlptLevel}.
            Tiến độ bài học: {lessonSummary}
            Một số từ trong sổ tay cá nhân: {wordSummary}
            Điểm bài thi thử gần nhất: {(LatestTestScorePercent is { } s ? $"{s:F0}%" : "chưa có")}; phân tích theo kỹ năng: {skillSummary}

            Hãy trả về DUY NHẤT một JSON object theo đúng schema sau, không thêm giải thích ngoài JSON:
            {
              "summary": "1-2 câu tóm tắt tình trạng học tập",
              "focusTopics": ["chủ đề 1", "chủ đề 2"],
              "suggestedLessons": [{ "title": "...", "reason": "..." }]
            }
            """;
        }
    }
}
