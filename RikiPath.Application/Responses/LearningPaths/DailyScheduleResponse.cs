namespace RikiPath.Application.Responses.LearningPaths
{
    public class DailyScheduleResponse
    {
        public int Day { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Focus { get; set; } = string.Empty;

        /// <summary>Id bài học trong hệ thống (để frontend mở đúng bài).</summary>
        public List<int> LessonIds { get; set; } = new();

        /// <summary>Tiêu đề các bài học tương ứng với LessonIds (cùng thứ tự).</summary>
        public List<string> Lessons { get; set; } = new();
    }
}