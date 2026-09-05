using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth
{
    public class VerifyEmailRequest
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public string VerificationCode { get; set; } = string.Empty;
    }
}
