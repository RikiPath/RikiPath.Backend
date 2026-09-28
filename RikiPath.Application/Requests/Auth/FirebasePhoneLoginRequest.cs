using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth
{
    public class FirebasePhoneLoginRequest
    {
        [Required(ErrorMessage = "idToken là bắt buộc")]
        public string IdToken { get; set; } = default!;
    }
}
