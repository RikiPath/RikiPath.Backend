namespace RikiPath.Application.Requests.Notebook
{
    public class CreateLearnerNoteRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateLearnerNoteRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class BookmarkVocabularyRequest
    {
        public int LearnerNoteId { get; set; }
        public int VocabularyId { get; set; }
    }

    public class BookmarkKanjiRequest
    {
        public int LearnerNoteId { get; set; }
        public int KanjiId { get; set; }
    }

    public class AddManualEntryRequest
    {
        public int LearnerNoteId { get; set; }
        public string Word { get; set; } = string.Empty;
        public string? Reading { get; set; }
        public string Meaning { get; set; } = string.Empty;
        public string? Note { get; set; }
        /// <summary>
        /// Khi true (mặc định, tương thích Mobile), tự động thêm vào hàng chờ SRS (ReviewCard).
        /// Khi false (ví dụ ghi chú bài học hoặc highlight đoạn văn), lưu entry vào Sổ tay nhưng không tự tạo ReviewCard.
        /// </summary>
        public bool EnrollSrs { get; set; } = true;
    }

    public class UpdateNotebookEntryRequest
    {
        /// <summary>Tùy chọn di chuyển mục sang trang ghi chú / danh sách khác thuộc cùng Learner.</summary>
        public int? LearnerNoteId { get; set; }
        public string? Word { get; set; }
        public string? Reading { get; set; }
        public string? Meaning { get; set; }
        public string? Note { get; set; }
    }
}
