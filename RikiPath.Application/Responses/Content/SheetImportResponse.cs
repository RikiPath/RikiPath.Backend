using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.Content
{
    public class SheetImportResponse
    {
        public ImportEntityType EntityType { get; set; }
        public string SheetName { get; set; } = string.Empty;

        public int TotalRows { get; set; }
        public int NewCount { get; set; }
        public int ModifiedCount { get; set; }
        public int UnchangedCount { get; set; }
        public int ErrorCount { get; set; }

        /// <summary>Trả về TẤT CẢ các dòng (kể cả Unchanged/Error) - theo đúng yêu cầu, không được lọc bớt.</summary>
        public List<ImportRowDiffResponse> Rows { get; set; } = new();
    }
}

