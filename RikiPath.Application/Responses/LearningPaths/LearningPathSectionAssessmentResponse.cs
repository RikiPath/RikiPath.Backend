namespace RikiPath.Application.Responses.LearningPaths;

public class LearningPathSectionAssessmentResponse
{
    public int? SectionId { get; set; }
    public string SectionTitle { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public string SkillCode { get; set; } = string.Empty;
    public string? EstimatedLevelCode { get; set; }
    public string? EstimatedLevelBand { get; set; }
    public string? RecommendedStudyLevelCode { get; set; }
    public string? HighestTestedLevelCode { get; set; }
    public string TargetLevelCode { get; set; } = string.Empty;
    public bool HasSufficientEvidence { get; set; }
    public List<LearningPathSkillLevelEvidence> LevelBreakdowns { get; set; } = new();
    public bool HasData { get; set; }
    public double? ScorePercent { get; set; }
    public double? SuggestedStudySharePercent { get; set; }
    public int CorrectCount { get; set; }
    public int TotalCount { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public class LearningPathSkillLevelEvidence
{
    public string LevelCode { get; set; } = string.Empty;
    public int CorrectCount { get; set; }
    public int TotalCount { get; set; }
    public double ScorePercent { get; set; }
    public bool MeetsPlanningThreshold { get; set; }
}

public class LearningPathStudyTimeScenario
{
    public int MinutesPerDay { get; set; }
    public int EstimatedStudyDays { get; set; }
    public bool? CanMeetRequestedExamDate { get; set; }
    public bool WithinRequestedRange { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class LearningPathPhaseResponse
{
    public string Title { get; set; } = string.Empty;
    public string Focus { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int StartDay { get; set; }
    public int EndDay { get; set; }
}
