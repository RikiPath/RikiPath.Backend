namespace RikiPath.Application.Responses.CertificateLevel
{
    public class CertificateLevelResponse
    {
        public int Id { get; set; }
        public int CertificateTypeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
    }
}
