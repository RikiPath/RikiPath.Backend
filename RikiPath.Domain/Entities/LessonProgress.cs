using System;

namespace RikiPath.Domain.Entities
{
    /// <summary>Per-learner, per-lesson watch progress.</summary>
    public class LessonProgress : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }

        public int ProgressPercent { get; set; }
        public bool IsCompleted { get; set; }
        public int LastPositionSeconds { get; set; }
        public DateTime? LastWatchedAt { get; set; }
    }
}
