namespace RikiPath.Domain.Entities
{
    public class Certification : Base
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }

        public List<CertificationLevel>? Levels { get; set; }
    }
}
