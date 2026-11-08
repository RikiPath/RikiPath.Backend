using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.MentorProfile
{
    public class SubmitMentorVerificationRequest
    {
        [MaxLength(1000, ErrorMessage = "Đường dẫn tài liệu không được vượt quá 1000 ký tự.")]
        public string? VerificationDocumentUrl { get; set; }

        [MaxLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự.")]
        public string? Notes { get; set; }
    }
}
