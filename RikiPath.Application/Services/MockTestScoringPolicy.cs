using System.Globalization;
using System.Text.Json;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

/// <summary>
/// Chính sách chấm điểm chuẩn hóa (Single Source of Truth) cho Bộ đề luyện tập (thang 10)
/// và Thi thử JLPT N5–N2 (thang 180 mô phỏng theo cơ cấu điểm thành phần & điểm liệt JLPT).
/// </summary>
public static class MockTestScoringPolicy
{
    public const string ScoreTypePractice10 = "PRACTICE_SCALE_10";
    public const string ScoreTypeMock180 = "JLPT_MOCK_180";
    public const string ScoreTypeLegacyPercent = "LEGACY_PERCENT";

    public const string RoundingPractice = "ROUND_HALF_UP_2DEC";
    public const string RoundingMock = "JLPT_SECTION_NEAREST_INT_SUM";

    public const string MockDisclaimer =
        "Điểm thi thử mô phỏng theo quy chuẩn JLPT (không phải Scaled Score IRT chính thức của JLPT).";

    public const string PracticeDisclaimer =
        "Phân loại mức độ luyện tập chỉ phục vụ theo dõi tiến độ nội bộ, không áp dụng ngưỡng đỗ JLPT.";

    public sealed record JlptScoringSectionSpec(
        string Key,
        string Title,
        int MaxScore,
        int PassScore);

    public sealed record JlptMockSpec(
        string LevelCode,
        int OverallMaxScore,
        int OverallPassScore,
        IReadOnlyList<JlptScoringSectionSpec> Sections);

    public sealed record TestSectionScoreInput(
        int SectionId,
        string SectionTitle,
        string? SkillCode,
        string? SkillName,
        int CorrectCount,
        int TotalCount,
        double RawScore,
        double MaxRawScore);

    private static readonly IReadOnlyDictionary<string, JlptMockSpec> MockSpecs =
        new Dictionary<string, JlptMockSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["N5"] = new(
                "N5",
                180,
                80,
                [
                    new("KNOWLEDGE_READING", "Kiến thức ngôn ngữ & Đọc hiểu", 120, 38),
                    new("LISTENING", "Nghe hiểu", 60, 19),
                ]),
            ["N4"] = new(
                "N4",
                180,
                90,
                [
                    new("KNOWLEDGE_READING", "Kiến thức ngôn ngữ & Đọc hiểu", 120, 38),
                    new("LISTENING", "Nghe hiểu", 60, 19),
                ]),
            ["N3"] = new(
                "N3",
                180,
                95,
                [
                    new("LANGUAGE_KNOWLEDGE", "Kiến thức ngôn ngữ", 60, 19),
                    new("READING", "Đọc hiểu", 60, 19),
                    new("LISTENING", "Nghe hiểu", 60, 19),
                ]),
            ["N2"] = new(
                "N2",
                180,
                90,
                [
                    new("LANGUAGE_KNOWLEDGE", "Kiến thức ngôn ngữ", 60, 19),
                    new("READING", "Đọc hiểu", 60, 19),
                    new("LISTENING", "Nghe hiểu", 60, 19),
                ]),
        };

    public static bool IsPracticeExamKind(string? examKind) =>
        string.Equals(examKind, nameof(ExamKind.PracticeSet), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(examKind, nameof(ExamKind.MixedPractice), StringComparison.OrdinalIgnoreCase);

    public static JlptMockSpec? TryGetMockSpec(string? levelCode) =>
        string.IsNullOrWhiteSpace(levelCode) ? null : MockSpecs.GetValueOrDefault(levelCode.Trim());

    /// <summary>
    /// Lấy trọng số thực tế của câu hỏi từ SkillMetadataJson nếu có cấu hình; mặc định = 1.0.
    /// </summary>
    public static double ResolveQuestionWeight(MockQuestion question)
    {
        var w = TryParseWeightFromJson(question.SkillMetadataJson)
            ?? TryParseWeightFromJson(question.QuestionBankItem?.SkillMetadataJson);
        return w is > 0 and < 10_000 ? w.Value : 1d;
    }

    private static double? TryParseWeightFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var key in new[] { "weight", "points", "point", "maxScore", "score" })
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (!prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                    if (prop.Value.ValueKind == JsonValueKind.Number &&
                        prop.Value.TryGetDouble(out var val) &&
                        double.IsFinite(val) && val > 0)
                    {
                        return val;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed metadata JSON and fall back cleanly to equal weight.
        }
        return null;
    }

    public static (
        List<MockTestSectionResult> Entities,
        List<SectionResultItem> Responses,
        double OverallAccuracyPercent,
        double RawScore,
        double MaxRawScore,
        MockAttemptScoringSummary Scoring)
        BuildEvaluatedAttempt(
            int attemptId,
            string? examKind,
            string? jlptLevelCode,
            IReadOnlyList<MockQuestion> questions,
            IReadOnlyDictionary<int, bool> correctByQuestion)
    {
        var grouped = questions
            .Where(q => q.MockTestSection is not null)
            .GroupBy(q => q.MockTestSection)
            .OrderBy(g => g.Key.SortOrder)
            .ThenBy(g => g.Key.Id)
            .Select(g =>
            {
                var total = 0;
                var correct = 0;
                var rawEarned = 0d;
                var rawMax = 0d;
                foreach (var q in g)
                {
                    total++;
                    var w = ResolveQuestionWeight(q);
                    rawMax += w;
                    if (correctByQuestion.TryGetValue(q.Id, out var ok) && ok)
                    {
                        correct++;
                        rawEarned += w;
                    }
                }
                var pct = rawMax <= 0d ? 0d : Math.Round(rawEarned * 100d / rawMax, 2, MidpointRounding.AwayFromZero);
                return new
                {
                    Section = g.Key,
                    Correct = correct,
                    Total = total,
                    RawScore = Math.Round(rawEarned, 2, MidpointRounding.AwayFromZero),
                    MaxRawScore = Math.Round(rawMax, 2, MidpointRounding.AwayFromZero),
                    AccuracyPercent = pct,
                };
            })
            .ToList();

        var entities = grouped.Select(g => new MockTestSectionResult
        {
            MockTestAttemptId = attemptId,
            MockTestSectionId = g.Section.Id,
            CorrectCount = g.Correct,
            TotalCount = g.Total,
            ScorePercent = g.AccuracyPercent,
        }).ToList();

        var responses = grouped.Select(g => new SectionResultItem
        {
            MockTestSectionId = g.Section.Id,
            SectionTitle = g.Section.Title,
            LanguageSkillName = g.Section.LanguageSkill?.Name ?? string.Empty,
            LanguageSkillCode = g.Section.LanguageSkill?.Code,
            CorrectCount = g.Correct,
            TotalCount = g.Total,
            RawScore = g.RawScore,
            MaxRawScore = g.MaxRawScore,
            Score = g.AccuracyPercent,
        }).ToList();

        var totalRaw = Math.Round(grouped.Sum(g => g.RawScore), 2, MidpointRounding.AwayFromZero);
        var totalMaxRaw = Math.Round(grouped.Sum(g => g.MaxRawScore), 2, MidpointRounding.AwayFromZero);
        var overallAccuracy = totalMaxRaw <= 0d
            ? 0d
            : Math.Round(totalRaw * 100d / totalMaxRaw, 2, MidpointRounding.AwayFromZero);

        var inputs = grouped.Select(g => new TestSectionScoreInput(
            g.Section.Id,
            g.Section.Title,
            g.Section.LanguageSkill?.Code,
            g.Section.LanguageSkill?.Name,
            g.Correct,
            g.Total,
            g.RawScore,
            g.MaxRawScore)).ToList();

        var scoring = EvaluateFromSections(
            examKind,
            jlptLevelCode,
            inputs,
            totalCorrect: grouped.Sum(g => g.Correct),
            totalQuestions: grouped.Sum(g => g.Total),
            fallbackAccuracyPercent: overallAccuracy);

        return (entities, responses, overallAccuracy, totalRaw, totalMaxRaw, scoring);
    }

    public static MockAttemptScoringSummary EvaluateFromSectionResults(
        string? examKind,
        string? jlptLevelCode,
        IEnumerable<MockTestSectionResult> sectionResults,
        int totalCorrect,
        int totalQuestions,
        double? fallbackAccuracyPercent = null)
    {
        var inputs = sectionResults
            .Select(r =>
            {
                var total = Math.Max(0, r.TotalCount);
                // Reconstruct earned weight proportionally if legacy ScorePercent reflects weighted grading, else CorrectCount.
                var earned = total > 0 && r.ScorePercent is >= 0 and <= 100
                    ? Math.Round(r.ScorePercent * total / 100d, 4, MidpointRounding.AwayFromZero)
                    : r.CorrectCount;
                return new TestSectionScoreInput(
                    r.MockTestSectionId,
                    r.MockTestSection?.Title ?? string.Empty,
                    r.MockTestSection?.LanguageSkill?.Code,
                    r.MockTestSection?.LanguageSkill?.Name,
                    r.CorrectCount,
                    total,
                    earned,
                    total);
            })
            .ToList();

        return EvaluateFromSections(
            examKind,
            jlptLevelCode,
            inputs,
            totalCorrect,
            totalQuestions,
            fallbackAccuracyPercent);
    }

    public static MockAttemptScoringSummary EvaluateFromSections(
        string? examKind,
        string? jlptLevelCode,
        IReadOnlyList<TestSectionScoreInput> sections,
        int totalCorrect,
        int totalQuestions,
        double? fallbackAccuracyPercent = null)
    {
        var level = jlptLevelCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var hasSections = sections.Count > 0 && sections.Sum(s => s.MaxRawScore) > 0d;

        var rawEarned = hasSections
            ? Math.Round(sections.Sum(s => s.RawScore), 2, MidpointRounding.AwayFromZero)
            : totalCorrect;
        var rawMax = hasSections
            ? Math.Round(sections.Sum(s => s.MaxRawScore), 2, MidpointRounding.AwayFromZero)
            : Math.Max(0, totalQuestions);

        var accuracyPercent = rawMax > 0d
            ? Math.Round(rawEarned * 100d / rawMax, 2, MidpointRounding.AwayFromZero)
            : Math.Round(fallbackAccuracyPercent ?? 0d, 2, MidpointRounding.AwayFromZero);

        if (IsPracticeExamKind(examKind))
        {
            var canComputePractice = rawMax > 0d || fallbackAccuracyPercent.HasValue;
            if (!canComputePractice)
            {
                return new MockAttemptScoringSummary
                {
                    ScoreType = ScoreTypeLegacyPercent,
                    JlptLevelCode = level,
                    RawScore = 0,
                    MaxRawScore = 0,
                    AccuracyPercent = 0,
                    NormalizedScore = 0,
                    NormalizedMaxScore = 10,
                    IsPassStatusApplicable = false,
                    PassStatusCode = "INCOMPLETE_STRUCTURE",
                    PassStatusLabel = "Chưa đủ dữ liệu chấm điểm",
                    IsScoringValid = false,
                    RoundingPolicy = RoundingPractice,
                    Disclaimer = PracticeDisclaimer,
                };
            }

            var score10 = rawMax > 0d
                ? Math.Round((rawEarned / rawMax) * 10d, 2, MidpointRounding.AwayFromZero)
                : Math.Round((Math.Clamp(accuracyPercent, 0d, 100d) / 100d) * 10d, 2, MidpointRounding.AwayFromZero);

            score10 = Math.Clamp(score10, 0d, 10d);
            var (band, bandLabel, bandTone) = ClassifyPracticeBand(score10);

            return new MockAttemptScoringSummary
            {
                ScoreType = ScoreTypePractice10,
                JlptLevelCode = level,
                RawScore = rawEarned,
                MaxRawScore = rawMax,
                AccuracyPercent = accuracyPercent,
                NormalizedScore = score10,
                NormalizedMaxScore = 10,
                PassingScore = null,
                IsPassStatusApplicable = false,
                SimulatedPassed = null,
                PassStatusCode = "PRACTICE_BAND",
                PassStatusLabel = bandLabel,
                PerformanceBand = band,
                PerformanceLabel = bandLabel,
                PerformanceTone = bandTone,
                IsScoringValid = true,
                RoundingPolicy = RoundingPractice,
                Disclaimer = PracticeDisclaimer,
            };
        }

        // FullMockTest -> JLPT N5-N2 simulated 180-point scoring
        var spec = TryGetMockSpec(level);
        if (spec is null || !hasSections)
        {
            return new MockAttemptScoringSummary
            {
                ScoreType = fallbackAccuracyPercent.HasValue ? ScoreTypeLegacyPercent : ScoreTypeMock180,
                JlptLevelCode = level,
                RawScore = rawEarned,
                MaxRawScore = rawMax,
                AccuracyPercent = accuracyPercent,
                NormalizedScore = accuracyPercent,
                NormalizedMaxScore = 100,
                PassingScore = spec?.OverallPassScore,
                IsPassStatusApplicable = false,
                SimulatedPassed = null,
                PassStatusCode = fallbackAccuracyPercent.HasValue ? "LEGACY_UNAVAILABLE" : "INCOMPLETE_STRUCTURE",
                PassStatusLabel = "Chưa đủ dữ liệu đánh giá ngưỡng JLPT",
                PerformanceTone = "warning",
                IsScoringValid = false,
                RoundingPolicy = RoundingMock,
                Disclaimer = MockDisclaimer,
                FailureReasons =
                [
                    spec is null
                        ? "Chưa xác định được cấu trúc chuẩn JLPT N5–N2 của bài thi."
                        : "Lượt thi chưa có đủ dữ liệu điểm từng phần thi để quy đổi theo cấu trúc JLPT 180 điểm.",
                ],
            };
        }

        var buckets = spec.Sections.ToDictionary(
            s => s.Key,
            _ => (Raw: 0d, Max: 0d, Correct: 0, Total: 0));
        var unmappedSections = new List<string>();

        foreach (var sec in sections)
        {
            var targetKey = ResolveJlptScoringSectionKey(level, sec.SkillCode, sec.SkillName, sec.SectionTitle);
            if (targetKey is null || !buckets.TryGetValue(targetKey, out var cur))
            {
                if (!string.IsNullOrWhiteSpace(sec.SectionTitle))
                    unmappedSections.Add(sec.SectionTitle);
                continue;
            }
            buckets[targetKey] = (
                cur.Raw + sec.RawScore,
                cur.Max + sec.MaxRawScore,
                cur.Correct + sec.CorrectCount,
                cur.Total + sec.TotalCount);
        }

        var scoringSections = new List<MockScoringSectionResponse>(spec.Sections.Count);
        var missingRequired = new List<string>();
        foreach (var secSpec in spec.Sections)
        {
            var agg = buckets[secSpec.Key];
            var hasValidData = agg.Max > 0d && agg.Total > 0;
            if (!hasValidData)
                missingRequired.Add(secSpec.Title);

            var secAccuracy = agg.Max > 0d
                ? Math.Round(agg.Raw * 100d / agg.Max, 2, MidpointRounding.AwayFromZero)
                : 0d;
            var secScore = agg.Max > 0d
                ? (double)Math.Clamp((int)Math.Round((agg.Raw / agg.Max) * secSpec.MaxScore, 0, MidpointRounding.AwayFromZero), 0, secSpec.MaxScore)
                : 0d;
            var secPassed = hasValidData && secScore >= secSpec.PassScore;

            scoringSections.Add(new MockScoringSectionResponse
            {
                SectionKey = secSpec.Key,
                Title = secSpec.Title,
                Score = secScore,
                MaxScore = secSpec.MaxScore,
                PassScore = secSpec.PassScore,
                IsPassed = secPassed,
                IsRequired = true,
                HasValidData = hasValidData,
                RawScore = Math.Round(agg.Raw, 2, MidpointRounding.AwayFromZero),
                MaxRawScore = Math.Round(agg.Max, 2, MidpointRounding.AwayFromZero),
                AccuracyPercent = secAccuracy,
                CorrectCount = agg.Correct,
                TotalCount = agg.Total,
            });
        }

        if (missingRequired.Count > 0 || unmappedSections.Count > 0)
        {
            var reasons = new List<string>();
            if (missingRequired.Count > 0)
                reasons.Add($"Đề thi thiếu phần thi bắt buộc theo chuẩn JLPT {spec.LevelCode}: {string.Join(", ", missingRequired)}.");
            if (unmappedSections.Count > 0)
                reasons.Add($"Có phần thi chưa khớp cấu trúc JLPT {spec.LevelCode}: {string.Join(", ", unmappedSections)}.");

            return new MockAttemptScoringSummary
            {
                ScoreType = ScoreTypeMock180,
                JlptLevelCode = spec.LevelCode,
                RawScore = rawEarned,
                MaxRawScore = rawMax,
                AccuracyPercent = accuracyPercent,
                NormalizedScore = scoringSections.Sum(s => s.Score),
                NormalizedMaxScore = spec.OverallMaxScore,
                PassingScore = spec.OverallPassScore,
                IsPassStatusApplicable = false,
                SimulatedPassed = null,
                PassStatusCode = "INCOMPLETE_STRUCTURE",
                PassStatusLabel = "Chưa đủ dữ liệu đánh giá ngưỡng JLPT",
                PerformanceTone = "warning",
                IsScoringValid = false,
                RoundingPolicy = RoundingMock,
                Disclaimer = MockDisclaimer,
                FailureReasons = reasons,
                ScoringSections = scoringSections,
            };
        }

        // Strict policy: overall simulated score equals exact sum of rounded section scores.
        var totalMockScore = scoringSections.Sum(s => s.Score);
        var failedSections = scoringSections.Where(s => !s.IsPassed).ToList();
        var meetsOverall = totalMockScore >= spec.OverallPassScore;
        var passed = meetsOverall && failedSections.Count == 0;

        var failureReasons = new List<string>();
        if (!meetsOverall)
        {
            failureReasons.Add(
                $"Tổng điểm thi thử đạt {FormatScore(totalMockScore)}/{spec.OverallMaxScore} điểm (chưa đạt ngưỡng tham chiếu {spec.OverallPassScore}/{spec.OverallMaxScore} của JLPT {spec.LevelCode}).");
        }
        foreach (var fs in failedSections)
        {
            failureReasons.Add(
                $"Phần {fs.Title} đạt {FormatScore(fs.Score)}/{fs.MaxScore} điểm (dưới điểm tối thiểu {fs.PassScore}/{fs.MaxScore}).");
        }
        if (!passed && meetsOverall && failedSections.Count > 0)
        {
            failureReasons.Add(
                $"Dù tổng điểm từ {spec.OverallPassScore}/{spec.OverallMaxScore} trở lên, kết quả vẫn là Chưa đạt do có phần thi dưới điểm tối thiểu.");
        }

        return new MockAttemptScoringSummary
        {
            ScoreType = ScoreTypeMock180,
            JlptLevelCode = spec.LevelCode,
            RawScore = rawEarned,
            MaxRawScore = rawMax,
            AccuracyPercent = accuracyPercent,
            NormalizedScore = totalMockScore,
            NormalizedMaxScore = spec.OverallMaxScore,
            PassingScore = spec.OverallPassScore,
            IsPassStatusApplicable = true,
            SimulatedPassed = passed,
            PassStatusCode = passed ? "SIMULATED_PASSED" : "SIMULATED_FAILED",
            PassStatusLabel = passed ? "Đạt ngưỡng" : "Chưa đạt",
            PerformanceTone = passed ? "success" : "danger",
            IsScoringValid = true,
            RoundingPolicy = RoundingMock,
            Disclaimer = MockDisclaimer,
            FailureReasons = failureReasons,
            ScoringSections = scoringSections,
        };
    }

    public static (string Band, string Label, string Tone) ClassifyPracticeBand(double scoreOn10)
    {
        if (scoreOn10 < 5d) return ("NEEDS_IMPROVEMENT", "Cần cải thiện", "danger");
        if (scoreOn10 < 7d) return ("AVERAGE", "Trung bình", "warning");
        if (scoreOn10 < 8.5d) return ("GOOD", "Khá", "info");
        return ("EXCELLENT", "Tốt", "success");
    }

    public static string? ResolveJlptScoringSectionKey(
        string? levelCode,
        string? skillCode,
        string? skillName,
        string? sectionTitle)
    {
        var level = levelCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var code = skillCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var name = skillName?.Trim().ToLowerInvariant() ?? string.Empty;
        var title = sectionTitle?.Trim().ToLowerInvariant() ?? string.Empty;

        var isListening = code == "LISTENING" ||
                          name.Contains("listening", StringComparison.Ordinal) ||
                          name.Contains("nghe", StringComparison.Ordinal) ||
                          title.Contains("nghe hiểu", StringComparison.Ordinal) ||
                          title.Contains("listening", StringComparison.Ordinal);
        if (isListening) return "LISTENING";

        if (level is "N5" or "N4")
        {
            // N5 & N4 merge Language Knowledge (Vocabulary, Kanji, Grammar) + Reading into one 120-point scoring section.
            if (code is "VOCABULARY" or "KANJI" or "GRAMMAR" or "READING" ||
                name.Contains("vocab", StringComparison.Ordinal) ||
                name.Contains("kanji", StringComparison.Ordinal) ||
                name.Contains("grammar", StringComparison.Ordinal) ||
                name.Contains("reading", StringComparison.Ordinal) ||
                title.Contains("từ vựng", StringComparison.Ordinal) ||
                title.Contains("ngữ pháp", StringComparison.Ordinal) ||
                title.Contains("đọc hiểu", StringComparison.Ordinal) ||
                title.Contains("kiến thức ngôn ngữ", StringComparison.Ordinal))
            {
                return "KNOWLEDGE_READING";
            }
            return null;
        }

        if (level is "N3" or "N2")
        {
            var hasReading = code == "READING" ||
                             name.Contains("reading", StringComparison.Ordinal) ||
                             title.Contains("đọc hiểu", StringComparison.Ordinal);
            var hasLanguageKnowledge = code is "VOCABULARY" or "KANJI" or "GRAMMAR" ||
                                       name.Contains("vocab", StringComparison.Ordinal) ||
                                       name.Contains("kanji", StringComparison.Ordinal) ||
                                       name.Contains("grammar", StringComparison.Ordinal) ||
                                       title.Contains("từ vựng", StringComparison.Ordinal) ||
                                       title.Contains("ngữ pháp", StringComparison.Ordinal) ||
                                       title.Contains("hán tự", StringComparison.Ordinal) ||
                                       title.Contains("kanji", StringComparison.Ordinal) ||
                                       title.Contains("kiến thức ngôn ngữ", StringComparison.Ordinal);

            if (hasReading && !hasLanguageKnowledge)
                return "READING";
            if (code == "READING" && hasReading)
                return "READING";
            if (hasLanguageKnowledge && !hasReading)
                return "LANGUAGE_KNOWLEDGE";
            if (code is "VOCABULARY" or "KANJI" or "GRAMMAR")
                return "LANGUAGE_KNOWLEDGE";
            return null;
        }

        return null;
    }

    public static string FormatScore(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
}
