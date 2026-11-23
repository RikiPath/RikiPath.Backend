using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// Level 2 of Personal Notebook V3 (table <c>"LearnerNotes"</c>): learner-owned Notebook File
    /// inside a <see cref="NotebookFolder"/>, grouping <see cref="NotebookPage"/>s and their Saved Items.
    /// </summary>
    public class LearnerNote : Base
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; } = null!;

        public int? FolderId { get; set; }
        public NotebookFolder? Folder { get; set; }

        public int SortOrder { get; set; }
        public bool IsDefault { get; set; }
        public int Revision { get; set; } = 1;

        public List<NotebookPage>? Pages { get; set; }
        public List<LearnerNoteEntry>? LearnerNoteEntries { get; set; }
    }
}
