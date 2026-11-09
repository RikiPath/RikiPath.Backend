namespace RikiPath.Application.Requests.LearningPath
{
    public class GenerateLearningPathRequest
    {
        /// <summary>Ngày dự định thi; nếu bỏ trống sẽ dùng ngày đã lưu trong hồ sơ.</summary>
        public DateTime? TargetExamDate { get; set; }
    }
}
