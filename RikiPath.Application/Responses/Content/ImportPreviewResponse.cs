namespace RikiPath.Application.Responses.Content
{
    /// <summary>Kết quả Step 1 (Dry Run) - KHÔNG có gì được ghi xuống DB khi trả response này.</summary>
    public class ImportPreviewResponse
    {
        /// <summary>Id tương quan phía client cho lần preview này (không phải Id trong DB vì chưa lưu gì).</summary>
        public Guid PreviewId { get; set; }

        public string FileName { get; set; } = string.Empty;

        /// <summary>Mỗi sheet tìm thấy trong file trả về 1 phần tử. Sheet không có trong file (single upload, hoặc bulk thiếu sheet) thì không xuất hiện ở đây.</summary>
        public List<SheetImportResponse> Sheets { get; set; } = new();
    }
}
