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

        /// <summary>Ngày này có thể bắt đầu/tiếp tục theo thứ tự lộ trình hay chưa.</summary>
        public bool CanStart { get; set; }

        /// <summary>Ngày đã được người học bắt đầu.</summary>
        public bool IsStarted { get; set; }

        /// <summary>Ngày đã được người học xác nhận hoàn thành.</summary>
        public bool IsCompleted { get; set; }

        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
