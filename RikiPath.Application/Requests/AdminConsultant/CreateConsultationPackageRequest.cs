using Domain.Enums;

namespace RikiPath.Application.Requests.AdminConsultant
{
    public class CreateConsultationPackageRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ConsultationType Type { get; set; }
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
    }
}
