namespace RikiPath.Application.Requests.LearningPath
{
    public class GenerateLearningPathRequest
    {
        /// <summary>Ngày dự định thi; nếu bỏ trống sẽ dùng ngày đã lưu trong hồ sơ.</summary>
        public DateTime? TargetExamDate { get; set; }

        /// <summary>Fallback khi chưa có ngày thi; có ngày thi thì AI tự quyết định thời hạn khả thi.</summary>
        public string? Horizon { get; set; }
    }
}
