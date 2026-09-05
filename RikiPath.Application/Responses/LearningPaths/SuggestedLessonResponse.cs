namespace RikiPath.Application.Responses.LearningPaths
{
    public class SuggestedLessonResponse
    {
        public int? LessonId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
