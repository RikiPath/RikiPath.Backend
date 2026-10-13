using RikiPath.Domain.Enums;

namespace RikiPath.Application.Requests.Content
{
    public class ConfirmImportRowRequest
    {
        public ImportEntityType EntityType { get; set; }
        public ImportRowAction Action { get; set; }
        public int? TargetEntityId { get; set; }
        public string RowKey { get; set; } = string.Empty;
        public int SourceRowNumber { get; set; }

        public string PayloadJson { get; set; } = string.Empty;
        public Dictionary<string, string>? ChangedFields { get; set; }
    }

}
