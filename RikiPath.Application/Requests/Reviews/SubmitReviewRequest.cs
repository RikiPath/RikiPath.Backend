using System.ComponentModel.DataAnnotations;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Requests.Reviews
{
    public class SubmitReviewRequest
    {
        [Required]
        public int ReviewCardId { get; set; }

        [Range(0, 5, ErrorMessage = "Quality phải trong khoảng 0-5.")]
        public int Quality { get; set; } = 4;

        public ReviewRating? Rating { get; set; }

        [MaxLength(20)]
        public string StudyMode { get; set; } = "flip";

        [MaxLength(500)]
        public string? UserAnswer { get; set; }

        public int? ResponseTimeMs { get; set; }

        [MaxLength(120)]
        public string? IdempotencyKey { get; set; }

        public Guid? SessionKey { get; set; }
    }
}
