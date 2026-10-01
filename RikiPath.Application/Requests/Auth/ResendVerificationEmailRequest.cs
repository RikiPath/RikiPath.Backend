using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth
{
    public class ResendVerificationEmailRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
