using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth
{
    public class UpdateEmailRequest
    {
        [Required, EmailAddress, MaxLength(255)]
        public string NewEmail { get; set; } = string.Empty;

        [MaxLength(128)]
        public string? CurrentPassword { get; set; }
    }
}
