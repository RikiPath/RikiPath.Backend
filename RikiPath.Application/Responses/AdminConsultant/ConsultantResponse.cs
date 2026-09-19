namespace RikiPath.Application.Responses.AdminConsultant
{
    public class ConsultantResponse
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsEmailVerified { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
