namespace RikiPath.Application.Responses.Auth
{
    public class RegisterResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; } = string.Empty;
    }
}
