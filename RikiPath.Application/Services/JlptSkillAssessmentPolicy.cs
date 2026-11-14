using System.Text.RegularExpressions;
using RikiPath.Application.Responses.LearningPaths;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.PlacementTests;

namespace RikiPath.Application.Services;

public enum JlptStudySkill { Vocabulary, Grammar, Reading, Listening }

public static class JlptSkillAssessmentPolicy
{
    internal static int EvidenceRank(string? code) =>
        string.Equals(code?.Trim(), "N1", StringComparison.OrdinalIgnoreCase) ? 5 : JlptCurriculum.Rank(code);

    public static JlptStudySkill? Classify(string? name)
    {
        var value = (name ?? string.Empty).Trim().ToLowerInvariant();
        // Combined/unknown sections cannot be split into independent evidence reliably.
        var matches = new List<JlptStudySkill>();
        if (value.Contains("vocab") || value.Contains("từ vựng") || value.Contains("tu vung") || value.Contains("語彙")) matches.Add(JlptStudySkill.Vocabulary);
        if (value.Contains("grammar") || value.Contains("ngữ pháp") || value.Contains("ngu phap") || value.Contains("文法")) matches.Add(JlptStudySkill.Grammar);
        if (value.Contains("reading") || value.Contains("đọc") || value.Contains("doc hieu") || value.Contains("読解")) matches.Add(JlptStudySkill.Reading);
        if (value.Contains("listening") || value.Contains("nghe") || value.Contains("聴解")) matches.Add(JlptStudySkill.Listening);
        return matches.Distinct().Count() == 1 ? matches[0] : null;
    }

    public static string Label(JlptStudySkill skill) => skill switch
    {
        JlptStudySkill.Vocabulary => "Từ vựng",
        JlptStudySkill.Grammar => "Ngữ pháp",
        JlptStudySkill.Reading => "Đọc hiểu",
        _ => "Nghe hiểu"
    };

    public static List<LearningPathSectionAssessmentResponse> Assess(PlacementTestResultResponse placement, string targetCode,
        IReadOnlyList<SectionResultItem>? skillEvidence = null)
    {
        const int minimumSample = PlacementAssessmentModePolicy.MinimumQuestionsPerSkillAndLevel;
        var comparisonCode = PlacementAssessmentModePolicy.ComparisonLevel(targetCode);
        return Enum.GetValues<JlptStudySkill>().Select(skill =>
        {
            var sections = (skillEvidence ?? placement.Sections).Where(section =>
                Classify(string.IsNullOrWhiteSpace(section.LanguageSkillName) ? section.SectionTitle : section.LanguageSkillName) == skill &&
                (skill != JlptStudySkill.Listening || section.HasAudioEvidence == true) &&
                section.TotalCount > 0 && section.CorrectCount >= 0 && section.CorrectCount <= section.TotalCount).ToList();
            var levels = sections.Select(section =>
            {
                var code = section.JlptLevelName;
                if (EvidenceRank(code) == 0)
                    code = Regex.Match(section.SectionTitle.ToUpperInvariant(), @"\bN[1-5]\b").Value;
                // A legacy single-level snapshot is unambiguous; a mixed test's overall level is not.
                if (EvidenceRank(code) == 0 && placement.LevelBreakdowns.Count == 1)
                    code = placement.LevelBreakdowns[0].JlptLevelName;
                return new { Section = section, Code = (code ?? "").Trim().ToUpperInvariant() };
            }).Where(x => EvidenceRank(x.Code) > 0)
                .GroupBy(x => x.Code)
                .Select(group => new LearningPathSkillLevelEvidence
                {
                    LevelCode = group.Key,
                    CorrectCount = group.Sum(x => x.Section.CorrectCount),
                    TotalCount = group.Sum(x => x.Section.TotalCount),
                    ScorePercent = Math.Round(group.Sum(x => x.Section.CorrectCount) * 100d / group.Sum(x => x.Section.TotalCount), 2),
                    MeetsPlanningThreshold = group.Sum(x => x.Section.TotalCount) >= minimumSample &&
                        group.Sum(x => x.Section.CorrectCount) * 100d / group.Sum(x => x.Section.TotalCount) >= 80
                }).OrderBy(x => EvidenceRank(x.LevelCode)).ToList();
            var supported = levels.Where(x => x.MeetsPlanningThreshold).LastOrDefault();
            var highest = levels.LastOrDefault();
            var targetEvidence = levels.FirstOrDefault(level => level.LevelCode == targetCode && level.TotalCount >= minimumSample);
            var lowerEvidence = levels.FirstOrDefault(level => level.LevelCode == comparisonCode && level.TotalCount >= minimumSample);
            var inconsistent = targetEvidence is { MeetsPlanningThreshold: true } && lowerEvidence is { ScorePercent: < 50 };
            string? recommendedStudyLevel = null;
            if (targetEvidence is not null && (comparisonCode is null || lowerEvidence is not null))
            {
                recommendedStudyLevel = targetEvidence.MeetsPlanningThreshold && !inconsistent
                    ? targetCode
                    : comparisonCode ?? targetCode;
            }
            var band = lowerEvidence is { MeetsPlanningThreshold: true } && supported == lowerEvidence &&
                targetEvidence is { MeetsPlanningThreshold: false, ScorePercent: >= 50 }
                ? $"{lowerEvidence.LevelCode}–{targetEvidence.LevelCode}"
                : null;
            var total = sections.Sum(x => x.TotalCount);
            var correct = sections.Sum(x => x.CorrectCount);
            var sufficient = !inconsistent && levels.Any(x => x.TotalCount >= minimumSample);
            var estimated = inconsistent ? null : supported?.LevelCode;
            var result = new LearningPathSectionAssessmentResponse
            {
                SkillCode = skill.ToString().ToLowerInvariant(), Skill = Label(skill), SectionTitle = Label(skill),
                TargetLevelCode = targetCode, HasData = total > 0, HasSufficientEvidence = sufficient,
                EstimatedLevelCode = estimated, EstimatedLevelBand = band, HighestTestedLevelCode = highest?.LevelCode,
                RecommendedStudyLevelCode = recommendedStudyLevel,
                CorrectCount = correct, TotalCount = total,
                ScorePercent = total > 0 ? Math.Round(correct * 100d / total, 2) : null,
                LevelBreakdowns = levels,
                Priority = !sufficient ? "needs_assessment" : estimated is null || EvidenceRank(estimated) < EvidenceRank(targetCode) ? "high" : "maintenance"
            };
            result.Comment = (inconsistent, sufficient, band, estimated) switch
            {
                (true, _, _, _) => $"{result.Skill}: kết quả ở {comparisonCode} và {targetCode} chưa nhất quán. Cần thêm dữ liệu để xác nhận cấp độ.",
                (_, false, _, _) => $"{result.Skill}: chưa đủ câu hỏi hợp lệ để ước lượng cấp độ.",
                (_, _, not null, _) => $"{result.Skill}: đạt ngưỡng ở {comparisonCode} và đang tiến gần {targetCode}.",
                (_, _, _, null) => $"{result.Skill}: chưa đạt ngưỡng ở các cấp đã kiểm tra. Nên ôn lại kiến thức nền của kỹ năng này.",
                _ => $"{result.Skill}: kết quả phù hợp khoảng {estimated}, với mục tiêu ôn {targetCode}.",
            };
            result.Recommendation = skill switch
            {
                JlptStudySkill.Vocabulary => "Ôn từ vựng theo cấp độ, cách đọc Kanji trong từ và sử dụng từ trong ngữ cảnh.",
                JlptStudySkill.Grammar => "Củng cố cấu trúc còn yếu, luyện chọn mẫu câu, sắp xếp câu và ngữ pháp trong đoạn văn.",
                JlptStudySkill.Reading => "Luyện đọc từ đoạn ngắn đến dài, tìm ý chính, suy luận và tra cứu thông tin theo thời gian.",
                _ => "Luyện nghe nhiệm vụ, nắm ý chính và phản hồi theo mức phù hợp, đối chiếu bản lời thoại sau khi nghe."
            };
            return result;
        }).ToList();
    }
}
