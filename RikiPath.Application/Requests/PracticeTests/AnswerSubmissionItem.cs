namespace RikiPath.Application.Requests.MockTests
{
    public class AnswerSubmissionItem
    {
        public int QuestionId { get; set; }

        /// <summary>Null nếu thí sinh bỏ trống câu này.</summary>
        public int? SelectedOptionId { get; set; }
    }
}
