using RikiPath.Application.DTOs.Content;

namespace RikiPath.Application.Requests.Content
{
    public class BulkImportRequest
    {
        public ContentEntityType EntityType { get; set; }
        public Stream FileStream { get; set; } = null!;
        public int CertificationLevelId { get; set; }
        public int? TargetPracticeTestSectionId { get; set; }
    }
}
