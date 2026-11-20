namespace RikiPath.Application.Responses.Auth
{
    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PendingEmail { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsEmailVerified { get; set; }
        public bool HasPassword { get; set; }
        public bool GoogleLinked { get; set; }
    }
}
