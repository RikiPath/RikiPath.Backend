namespace RikiPath.Application.Responses.KanjiWriting
{
    public class KanjiWritingQueueItem
    {
        public int KanjiId { get; set; }
        public string Character { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string? OnYomi { get; set; }
        public string? KunYomi { get; set; }

        /// <summary>true nếu đây là chữ mới (chưa từng luyện viết lần nào) - FE nên bật
        /// animateCharacter() cho FE xem trước cách viết đúng, thay vì vào quiz ngay.</summary>
        public bool IsNew { get; set; }
    }
}
