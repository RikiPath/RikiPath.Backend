using RikiPath.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth
{
    public class RegisterRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6), MaxLength(128)]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(128), Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? FullName { get; set; }
    }
}
