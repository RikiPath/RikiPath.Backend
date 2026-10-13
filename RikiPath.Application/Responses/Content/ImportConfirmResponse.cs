namespace RikiPath.Application.Responses.Content
{
    public class ImportConfirmResponse
    {
        public Guid BatchId { get; set; }
        public int DraftsCreatedCount { get; set; }

        /// <summary>Số dòng server phòng thủ bỏ qua (Action không phải New/Modified, hoặc thiếu PayloadJson) - lẽ ra FE đã lọc trước khi gửi.</summary>
        public int SkippedCount { get; set; }
    }
}
