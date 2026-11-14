using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth;

public sealed class GoogleAuthRequest
{
    [Required, StringLength(8192)]
    public string Credential { get; set; } = string.Empty;

    public string? CurrentPassword { get; set; }
}
