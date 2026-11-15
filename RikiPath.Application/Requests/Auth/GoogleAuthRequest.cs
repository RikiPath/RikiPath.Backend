using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth;

public sealed class GoogleAuthRequest
{
    [StringLength(8192)]
    public string Credential { get; set; } = string.Empty;

    [StringLength(8192)]
    public string? IdToken { get; set; }

    public string? CurrentPassword { get; set; }
}
