using RikiPath.Domain.Enums;
namespace RikiPath.Application.Responses.KanaWriting
{
    public class KanaWritingQueueItem
    {
        public int KanaCharacterId { get; set; }
        public string Character { get; set; } = string.Empty;
        public KanaCharacterType Type { get; set; }
        public string Romaji { get; set; } = string.Empty;
        public int StrokeCount { get; set; }
        public string? StrokeOrderImageUrl { get; set; }
        public string? AudioUrl { get; set; }
        public bool IsNew { get; set; }
    }
}
