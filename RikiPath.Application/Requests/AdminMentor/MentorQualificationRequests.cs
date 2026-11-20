using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.AdminMentor;

public sealed class GrantMentorQualificationRequest
{
    [Range(1, int.MaxValue)]
    public int CertificateLevelId { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public sealed class RevokeMentorQualificationRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do thu hồi quyền giảng dạy.")]
    [MinLength(3)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
