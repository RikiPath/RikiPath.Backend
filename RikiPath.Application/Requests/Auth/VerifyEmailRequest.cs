using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth
{
    public class VerifyEmailRequest
    {
        public int? UserId { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Required, RegularExpression(@"^\d{6}$")]
        public string VerificationCode { get; set; } = string.Empty;
    }
}
