namespace RikiPath.Application.Requests.AdminConsultant
{
    public class UpdateConsultationPackageRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
    }
}
