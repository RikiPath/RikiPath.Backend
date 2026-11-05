using System.ComponentModel.DataAnnotations;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Requests.MentorProfile
{
    public class ReviewMentorVerificationRequest
    {
        [Required(ErrorMessage = "Quyết định xét duyệt là bắt buộc.")]
        public MentorVerificationDecision Decision { get; set; }

        [MaxLength(1000, ErrorMessage = "Lý do từ chối không được vượt quá 1000 ký tự.")]
        public string? RejectionReason { get; set; }

        [MaxLength(1000, ErrorMessage = "Ghi chú xét duyệt không được vượt quá 1000 ký tự.")]
        public string? ReviewNotes { get; set; }
    }
}
