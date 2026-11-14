namespace RikiPath.Application.Requests.LearningPath;

public class PreviewLearningPathRequest : LearningPathOptionsRequest
{
    public int? StartingJlptLevelId { get; set; }
}
