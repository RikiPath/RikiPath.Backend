namespace RikiPath.Application.Models
{
    public readonly struct LearningPathPromptContext(
    string targetJlptLevel,
    string assessedJlptLevel,
    IReadOnlyList<(string LessonTitle, int ProgressPercent)> lessonProgress,
    IReadOnlyList<string> notebookWords,
    double? latestTestScorePercent,
    IReadOnlyDictionary<string, double>? latestTestLanguageSkillBreakdown)
    {
        public string TargetJlptLevel { get; } = targetJlptLevel;
        public string AssessedJlptLevel { get; } = assessedJlptLevel;
        public IReadOnlyList<(string LessonTitle, int ProgressPercent)> LessonProgress { get; } = lessonProgress;
        public IReadOnlyList<string> NotebookWords { get; } = notebookWords;
        public double? LatestTestScorePercent { get; } = latestTestScorePercent;
        public IReadOnlyDictionary<string, double>? LatestTestLanguageSkillBreakdown { get; } = latestTestLanguageSkillBreakdown;

        public string ToPromptText()
        {
            var lessonSummary = LessonProgress.Count == 0
                ? "Chưa học bài nào."
                : string.Join("; ", LessonProgress.Select(l => $"{l.LessonTitle} ({l.ProgressPercent}%)"));

            var wordSummary = NotebookWords.Count == 0
                ? "Sổ tay trống."
                : string.Join(", ", NotebookWords.Take(50)); // giới hạn tránh prompt quá dài

            var skillSummary = LatestTestLanguageSkillBreakdown is { Count: > 0 }
                ? string.Join("; ", LatestTestLanguageSkillBreakdown.Select(kv => $"{kv.Key}: {kv.Value:F0}%"))
                : "Chưa có dữ liệu bài thi thử.";

            return $$"""
            Trình độ mục tiêu của người học: JLPT {TargetJlptLevel}.
            Trình độ hiện tại theo Placement gần nhất: JLPT {AssessedJlptLevel}.
            Tiến độ bài học: {lessonSummary}
            Một số từ trong sổ tay cá nhân: {wordSummary}
            Điểm bài thi thử gần nhất: {(LatestTestScorePercent is { } s ? $"{s:F0}%" : "chưa có")}; phân tích theo kỹ năng: {skillSummary}
            """;
        }
    }
}
