namespace RikiPath.Application.Requests.CertificateSections
{
    public sealed class UpdateCertificateSectionRequest
    {
        public int CertificateLevelId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? DurationMinutes { get; set; }
        public int MaxScore { get; set; }
        public int PassingScore { get; set; }
        public int SortOrder { get; set; }
    }
}
