namespace RikiPath.Application.Requests.Notebook
{
    public class CreateVocabularyListRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateVocabularyListRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class BookmarkVocabularyRequest
    {
        public int VocabularyListId { get; set; }
        public int VocabularyEntryId { get; set; }
    }

    public class BookmarkKanjiRequest
    {
        public int VocabularyListId { get; set; }
        public int KanjiEntryId { get; set; }
    }

    public class AddManualEntryRequest
    {
        public int VocabularyListId { get; set; }
        public string Word { get; set; } = string.Empty;
        public string? Reading { get; set; }
        public string Meaning { get; set; } = string.Empty;
        public string? Note { get; set; }
    }
}
