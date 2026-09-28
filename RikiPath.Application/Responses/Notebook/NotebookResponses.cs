namespace RikiPath.Application.Responses.Notebook
{
    public class LearnerNoteResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int EntryCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class NotebookEntryResponse
    {
        public int NoteEntryId { get; set; }
        public int? ReviewCardId { get; set; }
        public string ContentType { get; set; } = string.Empty; // "vocabulary" | "kanji" | "manual"
        public string Term { get; set; } = string.Empty;
        public string? Reading { get; set; }
        public string Meaning { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTime AddedAt { get; set; }
    }

    public class GrammarBookmarkResponse
    {
        public int ReviewCardId { get; set; }
        public int GrammarPatternId { get; set; }
        public string GrammarTitle { get; set; } = string.Empty;
    }
}
