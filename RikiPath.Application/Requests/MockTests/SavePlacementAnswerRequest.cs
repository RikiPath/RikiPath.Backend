namespace RikiPath.Application.Requests.MockTests
{
    public class SavePlacementAnswerRequest
    {
        public int QuestionId { get; set; }

        /// <summary>
        /// Id option được chọn. Null nghĩa là bỏ chọn (học viên đổi ý, chưa chọn lại).
        /// </summary>
        public int? SelectedOptionId { get; set; }
    }
}
