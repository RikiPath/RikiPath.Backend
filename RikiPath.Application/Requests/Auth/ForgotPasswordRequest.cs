using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth;

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
