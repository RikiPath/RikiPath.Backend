namespace RikiPath.Application.Responses.Content
{
    public class MockTestImportPayloadResponse
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int TimeLimitMinutes { get; set; }
        public bool IsPlacementAssessment { get; set; }
        public string CertificateLevelCode { get; set; } = string.Empty;
        public int CertificateLevelId { get; set; }
        public List<MockTestSectionPayloadResponse> Sections { get; set; } = [];
    }
}
