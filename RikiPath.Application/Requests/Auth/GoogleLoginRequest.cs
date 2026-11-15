using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth
{
    public class GoogleLoginRequest
    {
        [Required(ErrorMessage = "idToken là bắt buộc")]
        public string IdToken { get; set; } = string.Empty;
    }
}
