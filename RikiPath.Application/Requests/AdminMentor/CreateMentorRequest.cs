using RikiPath.Domain.Enums;

namespace RikiPath.Application.Requests.AdminMentor
{
    public class CreateMentorRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
