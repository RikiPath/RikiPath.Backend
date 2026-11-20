using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Auth;

public class SetPasswordRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    [MaxLength(128, ErrorMessage = "Mật khẩu tối đa 128 ký tự.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
