namespace RikiPath.Application.Responses.LearningPaths
{
    public class LearningPathResponse
    {
        public int UserId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public string Feasibility { get; set; } = string.Empty;

        /// <summary>true = lộ trình bám ngày thi; false = năng lực chưa đủ, đã chuyển sang lộ trình dài hạn.</summary>
        public bool IsExamDateRealistic { get; set; }

        /// <summary>Số ngày ôn tối thiểu hệ thống ước tính theo năng lực hiện tại.</summary>
        public int RecommendedStudyDays { get; set; }

        /// <summary>Ngày thi gợi ý. Chỉ có giá trị khi IsExamDateRealistic = false.</summary>
        public DateTime? SuggestedExamDate { get; set; }

        public List<string> FocusTopics { get; set; } = new();
        public List<DailyScheduleResponse> DailySchedules { get; set; } = new();
    }
}