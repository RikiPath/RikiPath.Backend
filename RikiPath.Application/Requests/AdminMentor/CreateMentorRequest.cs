using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.AdminMentor
{
    public class CreateMentorRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        [MaxLength(200)]
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
