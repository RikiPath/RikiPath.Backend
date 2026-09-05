using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RikiPath.Application.Responses.Reviews
{
    public enum ReviewContentType
    {
        Vocabulary,
        Kanji,
        Grammar
    }

    public class ReviewQueueItemResponse
    {
        public int ReviewItemId { get; set; }
        public ReviewContentType ContentType { get; set; }
        public string Term { get; set; } = string.Empty;      // Kanji/Word/Grammar title
        public string? Reading { get; set; }                  // Hiragana (vocab/kanji)
        public string Meaning { get; set; } = string.Empty;
        public string? Note { get; set; }                     // ghi chú cá nhân (nếu từ notebook)
        public int Repetitions { get; set; }
        public DateTime NextReviewDate { get; set; }
    }
}
