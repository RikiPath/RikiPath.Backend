namespace RikiPath.Application.Requests.LearningPath;

public class PreviewLearningPathRequest
{
    public int StartingJlptLevelId { get; set; }
    public int? TargetJlptLevelId { get; set; }
}
