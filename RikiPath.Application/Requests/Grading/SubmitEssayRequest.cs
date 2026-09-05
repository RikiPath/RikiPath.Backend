namespace RikiPath.Application.Requests.Grading
{
    public enum SubmissionType
    {
        Essay = 1,
        SentenceConstruction = 2,
        KanjiWriting = 3,
    }

    public class SubmitEssayRequest
    {
        public SubmissionType Type { get; set; }

        /// <summary>Optional — gắn bài nộp với 1 lesson cụ thể nếu có.</summary>
        public int? LessonId { get; set; }

        /// <summary>Đề bài / yêu cầu (VD: "Viết đoạn văn 200 chữ về sở thích của bạn").</summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>Bài làm của học viên (text; với KanjiWriting có thể là kết quả OCR).</summary>
        public string Content { get; set; } = string.Empty;
    }
}
