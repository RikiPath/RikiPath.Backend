using System;
using System.Collections.Generic;

namespace RikiPath.Application.Responses.Notebook
{
    public class LearnerNoteResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? FolderId { get; set; }
        public string? FolderName { get; set; }
        public bool IsDefault { get; set; }
        public int SortOrder { get; set; }
        public int Revision { get; set; }
        public int PageCount { get; set; }
        public int EntryCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class NotebookEntryResponse
    {
        public int NoteEntryId { get; set; }
        public int LearnerNoteId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int? NotebookPageId { get; set; }
        public string? NotebookPageTitle { get; set; }
        public int? FolderId { get; set; }
        public string? FolderName { get; set; }

        public int? ReviewCardId { get; set; }
        public int? VocabularyId { get; set; }
        public int? KanjiId { get; set; }
        public int? GrammarPatternId { get; set; }
        public string ContentType { get; set; } = string.Empty; // "vocabulary" | "kanji" | "grammar" | "manual"
        public string ItemKind { get; set; } = "manual"; // "vocabulary" | "kanji" | "grammar" | "highlight" | "note" | "manual"
        public string Term { get; set; } = string.Empty;
        public string? Reading { get; set; }
        public string Meaning { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? HighlightColor { get; set; }
        public string? SourceType { get; set; }
        public int? SourceId { get; set; }
        public string? SourcePath { get; set; }
        public string? SourceTitle { get; set; }
        public string? ContextSnippet { get; set; }
        public string? ExampleSentence { get; set; }
        public string? ExampleMeaning { get; set; }
        public List<string> Tags { get; set; } = [];
        public bool IsFavorite { get; set; }
        public int Revision { get; set; }
        public DateTime AddedAt { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool IsAlreadyExisting { get; set; }
    }

    public class GrammarBookmarkResponse
    {
        public int ReviewCardId { get; set; }
        public int GrammarPatternId { get; set; }
        public string GrammarTitle { get; set; } = string.Empty;
    }

    public class NotebookPageSummaryResponse
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int? FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public int ChecklistTotalCount { get; set; }
        public int ChecklistCompletedCount { get; set; }
        public bool IsFavorite { get; set; }
        public DateTime? FavoritedAtUtc { get; set; }
        public bool IsDefault { get; set; }
        public int SavedItemCount { get; set; }
        public int SortOrder { get; set; }
        public int Revision { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? LastAutosavedAtUtc { get; set; }
    }

    public class NotebookPageDetailResponse
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int? FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ContentJson { get; set; } = "{\"type\":\"doc\",\"version\":1,\"blocks\":[]}";
        public string ContentPlainText { get; set; } = string.Empty;
        public int ContentFormatVersion { get; set; }
        public int ChecklistTotalCount { get; set; }
        public int ChecklistCompletedCount { get; set; }
        public bool IsFavorite { get; set; }
        public DateTime? FavoritedAtUtc { get; set; }
        public bool IsDefault { get; set; }
        public string? SourceType { get; set; }
        public int? SourceId { get; set; }
        public string? SourcePath { get; set; }
        public string? SourceTitle { get; set; }
        public int SortOrder { get; set; }
        public int Revision { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? LastAutosavedAtUtc { get; set; }
        public List<NotebookEntryResponse> SavedItems { get; set; } = [];
    }

    public class NotebookFileTreeResponse
    {
        public int Id { get; set; }
        public int? FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsDefault { get; set; }
        public int SortOrder { get; set; }
        public int Revision { get; set; }
        public int PageCount { get; set; }
        public int SavedItemCount { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public List<NotebookPageSummaryResponse> Pages { get; set; } = [];
    }

    public class NotebookFolderTreeResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public int SortOrder { get; set; }
        public int Revision { get; set; }
        public int FileCount { get; set; }
        public int PageCount { get; set; }
        public int SavedItemCount { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public List<NotebookFileTreeResponse> Files { get; set; } = [];
    }

    public class NotebookStatsResponse
    {
        public int TotalFolders { get; set; }
        public int TotalFiles { get; set; }
        public int TotalPages { get; set; }
        public int TotalSavedItems { get; set; }
        public int FavoritePagesCount { get; set; }
    }

    public class NotebookTreeResponse
    {
        public List<NotebookFolderTreeResponse> Folders { get; set; } = [];
        public NotebookStatsResponse Stats { get; set; } = new();
    }

    public class NotebookSavedItemsPageResponse
    {
        public List<NotebookEntryResponse> Items { get; set; } = [];
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    public class NotebookSearchResponse
    {
        public string Query { get; set; } = string.Empty;
        public List<NotebookFolderTreeResponse> Folders { get; set; } = [];
        public List<NotebookFileTreeResponse> Files { get; set; } = [];
        public List<NotebookPageSummaryResponse> Pages { get; set; } = [];
        public List<NotebookEntryResponse> SavedItems { get; set; } = [];
        public int TotalSavedItems { get; set; }
    }
}
