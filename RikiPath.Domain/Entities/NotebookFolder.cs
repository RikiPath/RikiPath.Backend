using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// Level 1 of Personal Notebook V3: top-level learner folder grouping Notebook Files
    /// (for example "JLPT N5", "Giao tiếp", or default "Sổ tay chung").
    /// </summary>
    public class NotebookFolder : Base
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public string NormalizedName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsDefault { get; set; }
        public int Revision { get; set; } = 1;

        public List<LearnerNote>? Files { get; set; }
    }
}
