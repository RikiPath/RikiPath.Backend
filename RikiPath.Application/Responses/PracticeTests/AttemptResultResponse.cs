namespace RikiPath.Application.Responses.MockTests
{
    public class AttemptResultResponse
    {
        public int AttemptId { get; set; }
        public int MockTestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string JlptLevelName { get; set; } = string.Empty;
        public string ExamKind { get; set; } = "FullMockTest";
        public bool EnforceStrictTimer { get; set; } = true;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public double TotalScore { get; set; }
        public double RawScore { get; set; }
        public double MaxRawScore { get; set; }
        public double AccuracyPercent { get; set; }
        public MockAttemptScoringSummary? Scoring { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int BlankCount { get; set; }
        public int TotalQuestions { get; set; }
        public int DurationSeconds { get; set; }
        public List<int> WrongQuestionIds { get; set; } = [];
        public List<SectionResultItem> Sections { get; set; } = new();
    }

    public sealed class MockAttemptScoringSummary
    {
        public string ScoreType { get; set; } = "LEGACY_PERCENT";
        public string JlptLevelCode { get; set; } = string.Empty;
        public double RawScore { get; set; }
        public double MaxRawScore { get; set; }
        public double AccuracyPercent { get; set; }
        public double NormalizedScore { get; set; }
        public int NormalizedMaxScore { get; set; } = 100;
        public int? PassingScore { get; set; }
        public bool IsPassStatusApplicable { get; set; }
        public bool? SimulatedPassed { get; set; }
        public string PassStatusCode { get; set; } = string.Empty;
        public string? PassStatusLabel { get; set; }
        public string? PerformanceBand { get; set; }
        public string? PerformanceLabel { get; set; }
        public string? PerformanceTone { get; set; }
        public bool IsScoringValid { get; set; } = true;
        public string RoundingPolicy { get; set; } = string.Empty;
        public string? Disclaimer { get; set; }
        public List<string> FailureReasons { get; set; } = [];
        public List<MockScoringSectionResponse> ScoringSections { get; set; } = [];
    }

    public sealed class MockScoringSectionResponse
    {
        public string SectionKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public double Score { get; set; }
        public int MaxScore { get; set; }
        public int PassScore { get; set; }
        public bool IsPassed { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool HasValidData { get; set; } = true;
        public double RawScore { get; set; }
        public double MaxRawScore { get; set; }
        public double AccuracyPercent { get; set; }
        public int CorrectCount { get; set; }
        public int TotalCount { get; set; }
    }

    public sealed record MockTestHistoryItem(
        int AttemptId,
        int MockTestId,
        string Title,
        string JlptLevelName,
        DateTime SubmittedAt,
        double TotalScore,
        string ExamKind = "FullMockTest",
        string AttemptStatus = "completed",
        int CorrectCount = 0,
        int WrongCount = 0,
        int BlankCount = 0,
        int TotalQuestions = 0,
        int DurationSeconds = 0,
        DateTime? StartedAt = null,
        bool CanReview = true,
        MockAttemptScoringSummary? Scoring = null,
        string AttemptKind = "MockTest",
        string? AssessedJlptLevelName = null,
        string? RecommendedTargetJlptLevelName = null);

    public sealed record ExamHistoryStatsResponse(
        int TotalAttempts,
        int PracticeCount,
        int MockCount,
        int PlacementCount,
        int CompletedCount,
        double? AveragePracticeScore10,
        double? AverageMockScore180,
        double? BestMockScore180,
        int? CompletionRatePercent);

    public sealed record PagedMockTestHistoryResponse(
        IReadOnlyList<MockTestHistoryItem> Items,
        int Total,
        int Page,
        int PageSize,
        bool HasMore,
        ExamHistoryStatsResponse Stats);
}
