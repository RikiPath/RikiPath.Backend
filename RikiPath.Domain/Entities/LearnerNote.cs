using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>A custom, learner-owned vocabulary list inside the personal notebook (e.g. by topic/difficulty).</summary>
    public class LearnerNote : Base
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }

        public List<LearnerNoteEntry>? LearnerNoteEntries { get; set; }
    }
}
