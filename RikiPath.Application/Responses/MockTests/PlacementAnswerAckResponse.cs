namespace RikiPath.Application.Responses.MockTests
{
    public class PlacementAnswerAckResponse
    {
        public int AttemptId { get; set; }
        public int QuestionId { get; set; }
        public int? SelectedOptionId { get; set; }
        public DateTime SavedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
