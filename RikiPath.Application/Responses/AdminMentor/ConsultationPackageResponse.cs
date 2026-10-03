using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.AdminMentor
{
    public class ConsultationPackageResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public MentorType Type { get; set; }
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsActive { get; set; }
    }
}
