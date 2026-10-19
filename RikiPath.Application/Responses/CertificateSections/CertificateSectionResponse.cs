namespace RikiPath.Application.Responses.CertificateSections
{
    public sealed class CertificateSectionResponse
    {
        public int Id { get; init; }
        public int CertificateLevelId { get; init; }
        public string CertificateLevelCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int? DurationMinutes { get; init; }
        public int MaxScore { get; init; }
        public int PassingScore { get; init; }
        public int SortOrder { get; init; }
        public bool IsArchived { get; init; }
        public DateTime? CreatedDate { get; init; }
        public DateTime? ModifiedDate { get; init; }
    }
}
