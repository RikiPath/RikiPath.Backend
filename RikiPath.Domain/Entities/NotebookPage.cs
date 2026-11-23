using System;
using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// Level 3 of Personal Notebook V3: individual rich-text note page inside a Notebook File
    /// (<see cref="LearnerNote"/>), owning rich block JSON, searchable plain text, favorite flag,
    /// revision token, and child Saved Items (<see cref="LearnerNoteEntry"/>).
    /// </summary>
    public class NotebookPage : Base
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; } = null!;

        public int LearnerNoteId { get; set; }
        public LearnerNote LearnerNote { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string ContentJson { get; set; } = "{\"type\":\"doc\",\"version\":1,\"blocks\":[]}";
        public string ContentPlainText { get; set; } = string.Empty;
        public int ContentFormatVersion { get; set; } = 1;

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
        public DateTime? LastAutosavedAtUtc { get; set; }
        public int Revision { get; set; } = 1;

        public List<LearnerNoteEntry>? SavedItems { get; set; }
    }
}
