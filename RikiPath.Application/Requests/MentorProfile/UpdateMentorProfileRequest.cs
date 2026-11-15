using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.MentorProfile
{
    public class UpdateMentorProfileRequest
    {
        [MaxLength(2000, ErrorMessage = "Giới thiệu bản thân không được vượt quá 2000 ký tự.")]
        public string? Bio { get; set; }

        [MaxLength(500, ErrorMessage = "Lĩnh vực chuyên môn không được vượt quá 500 ký tự.")]
        public string? Expertise { get; set; }

        [MaxLength(20, ErrorMessage = "Trình độ JLPT không được vượt quá 20 ký tự.")]
        public string? JlptLevel { get; set; }

        [Range(0, 50, ErrorMessage = "Số năm kinh nghiệm phải nằm trong khoảng từ 0 đến 50.")]
        public int? YearsOfExperience { get; set; }

        [MaxLength(200, ErrorMessage = "Nơi công tác không được vượt quá 200 ký tự.")]
        public string? Workplace { get; set; }

        [MaxLength(1000, ErrorMessage = "Đường dẫn tài liệu không được vượt quá 1000 ký tự.")]
        public string? VerificationDocumentUrl { get; set; }

        [MaxLength(1000, ErrorMessage = "Đường dẫn video không được vượt quá 1000 ký tự.")]
        public string? IntroductionVideoUrl { get; set; }
    }
}
