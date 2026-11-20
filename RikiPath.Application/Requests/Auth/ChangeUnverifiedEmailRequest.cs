using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth;

public sealed class ChangeUnverifiedEmailRequest
{
    [Required, EmailAddress]
    public string CurrentEmail { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string NewEmail { get; set; } = string.Empty;
}
