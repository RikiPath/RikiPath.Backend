using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.LearningMoments
{
    /// <summary>
    /// Claims one pending learning moment right before the client shows it. The server re-checks that the
    /// key is currently eligible for this learner; the key itself is never trusted as proof of completion.
    /// </summary>
    public class ClaimLearningMomentRequest
    {
        [Required(ErrorMessage = "Thiếu mã khoảnh khắc học tập.")]
        [MaxLength(120)]
        public string OccurrenceKey { get; set; } = string.Empty;
    }
}
