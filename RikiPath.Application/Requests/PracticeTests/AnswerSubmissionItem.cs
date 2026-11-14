namespace RikiPath.Application.Requests.MockTests
{
    public class AnswerSubmissionItem
    {
        public int QuestionId { get; set; }

        /// <summary>Null nếu bỏ trống; riêng bài Placement, 0 là lựa chọn chưa học hoặc không biết.</summary>
        public int? SelectedOptionId { get; set; }
        /// <summary>Structured response for non-choice questions; shape depends on question type.</summary>
        public System.Text.Json.JsonElement? Response { get; set; }
    }
}
