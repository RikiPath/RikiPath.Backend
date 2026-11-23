using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth
{
    public class VerifyEmailLinkRequest
    {
        [Required, MaxLength(4096)]
        public string Token { get; set; } = string.Empty;
    }
}
