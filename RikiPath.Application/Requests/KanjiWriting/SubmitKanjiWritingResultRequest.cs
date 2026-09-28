namespace RikiPath.Application.Requests.KanjiWriting
{
    public class SubmitKanjiWritingResultRequest
    {
        public int KanjiId { get; set; }

        /// <summary>Số nét vẽ sai trong lượt luyện - lấy thẳng từ HanziWriter.quiz()'s
        /// onComplete callback (summaryData.totalMistakes) ở phía FE, không cần FE tự tính điểm.</summary>
        public int TotalMistakes { get; set; }
    }
}
