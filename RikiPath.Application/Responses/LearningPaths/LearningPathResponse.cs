namespace RikiPath.Application.Responses.LearningPaths
{
    public class LearningPathResponse
    {
        public int UserId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> FocusTopics { get; set; } = new();
        public List<SuggestedLessonResponse> SuggestedLessons { get; set; } = new();
    }
}
