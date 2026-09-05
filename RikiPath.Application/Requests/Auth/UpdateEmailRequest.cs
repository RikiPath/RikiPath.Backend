using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth
{
    public class UpdateEmailRequest
    {
        [Required, EmailAddress]
        public string NewEmail { get; set; } = string.Empty;
    }
}
