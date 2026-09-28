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
    }
}
