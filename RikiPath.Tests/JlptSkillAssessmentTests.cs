using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.PlacementTests;
using RikiPath.Application.Services;

namespace RikiPath.Tests;

public class JlptSkillAssessmentTests
{
    [Theory]
    [InlineData(2, 15, true, "long_term")]
    [InlineData(0, 14, true, "short_term")]
    [InlineData(1, 60, true, "long_term")]
    [InlineData(0, 14, false, "long_term")]
    public void Horizon_is_driven_by_skill_gap_and_workload_not_the_requested_calendar(int gap, int days, bool complete, string expected)
        => Assert.Equal(expected, LearningPathPlanningPolicy.ClassifyHorizon(gap, days, complete));

    [Fact]
    public void Mastery_uses_each_skill_at_each_tested_level_not_the_overall_label()
    {
        var placement = new PlacementTestResultResponse
        {
            AssessedJlptLevelName = "N5",
            Sections =
            [
                Section("Vocabulary", "N2", 8, 10), Section("Grammar", "N5", 9, 10),
                Section("Grammar", "N2", 2, 10), Section("Reading", "N3", 9, 10),
                Section("Listening", "N4", 9, 10, audio: true), Section("Kanji", "N2", 10, 10),
                Section("Writing", "N2", 10, 10)
            ]
        };
        var result = JlptSkillAssessmentPolicy.Assess(placement, "N2").ToDictionary(x => x.SkillCode);
        Assert.Equal(4, result.Count);
        Assert.Equal("N2", result["vocabulary"].EstimatedLevelCode);
        Assert.Equal("N5", result["grammar"].EstimatedLevelCode);
        Assert.Equal("N3", result["reading"].EstimatedLevelCode);
        Assert.Equal("N4", result["listening"].EstimatedLevelCode);
    }

    [Fact]
    public void Adjacent_levels_show_progress_without_upgrading_the_confirmed_level()
    {
        var placement = new PlacementTestResultResponse
        {
            Sections =
            [
                Section("Vocabulary", "N3", 5, 5), Section("Vocabulary", "N2", 4, 5),
                Section("Grammar", "N3", 4, 5), Section("Grammar", "N2", 3, 5),
                Section("Reading", "N3", 5, 5), Section("Reading", "N2", 2, 5),
                Section("Listening", "N3", 1, 5, audio: true), Section("Listening", "N2", 5, 5, audio: true),
            ],
        };

        var result = JlptSkillAssessmentPolicy.Assess(placement, "N2").ToDictionary(skill => skill.SkillCode);
        Assert.Equal("N2", result["vocabulary"].EstimatedLevelCode);
        Assert.Null(result["vocabulary"].EstimatedLevelBand);
        Assert.Equal("N3", result["grammar"].EstimatedLevelCode);
        Assert.Equal("N3–N2", result["grammar"].EstimatedLevelBand);
        Assert.Equal("high", result["grammar"].Priority);
        Assert.Equal("N3", result["reading"].EstimatedLevelCode);
        Assert.Null(result["reading"].EstimatedLevelBand);
        Assert.Null(result["listening"].EstimatedLevelCode);
        Assert.Equal("needs_assessment", result["listening"].Priority);
    }

    [Fact]
    public void Scores_are_weighted_by_question_count_and_unknown_levels_do_not_inherit_overall_N2()
    {
        var placement = new PlacementTestResultResponse
        {
            AssessedJlptLevelName = "N2",
            Sections = [Section("Grammar", "N4", 1, 1), Section("Grammar", "N4", 0, 9),
                new SectionResultItem { LanguageSkillName = "Vocabulary", TotalCount = 10, CorrectCount = 10 }]
        };
        var result = JlptSkillAssessmentPolicy.Assess(placement, "N2");
        var grammar = result.Single(x => x.SkillCode == "grammar");
        Assert.Equal(10, grammar.ScorePercent);
        Assert.Null(grammar.EstimatedLevelCode);
        Assert.False(result.Single(x => x.SkillCode == "vocabulary").HasSufficientEvidence);
    }

    [Fact]
    public void Transcript_only_and_tiny_samples_do_not_establish_listening_or_level_mastery()
    {
        var placement = new PlacementTestResultResponse
        { Sections = [Section("Listening", "N2", 10, 10), Section("Vocabulary", "N2", 1, 1)] };
        var result = JlptSkillAssessmentPolicy.Assess(placement, "N2");
        Assert.False(result.Single(x => x.SkillCode == "listening").HasData);
        Assert.Null(result.Single(x => x.SkillCode == "vocabulary").EstimatedLevelCode);
    }

    [Theory]
    [InlineData(15, null, null, 15, 15)]
    [InlineData(null, 30, 60, 30, 60)]
    public void Requests_accept_fixed_or_range(int? fixedMinutes, int? min, int? max, int expectedMin, int expectedMax)
        => Assert.Equal((expectedMin, expectedMax), LearningPathPlanningPolicy.ResolveDailyMinutes(fixedMinutes, min, max, 45, null, null));

    [Fact]
    public void Ambiguous_or_partial_ranges_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => LearningPathPlanningPolicy.ResolveDailyMinutes(15, 30, 60, 45, null, null));
        Assert.Throws<ArgumentException>(() => LearningPathPlanningPolicy.ResolveDailyMinutes(null, 30, null, 45, null, null));
    }

    private static SectionResultItem Section(string skill, string level, int correct, int total, bool audio = false)
        => new() { LanguageSkillName = skill, JlptLevelName = level, CorrectCount = correct, TotalCount = total, HasAudioEvidence = audio };
}
