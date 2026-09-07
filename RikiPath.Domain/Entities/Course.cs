using Domain.Enums;
using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    /// <summary>
    /// A JLPT-prep course — the top-level content container that groups multiple
    /// <see cref="Lesson"/> records under one JLPT level and one course category.
    /// Carries the content-authoring workflow (Draft/PendingReview/Published/Rejected).
    /// </summary>
    public class Course : Base, IReviewableContent
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }

        public ContentStatus Status { get; set; } = ContentStatus.Draft;
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedDate { get; set; }

        public int JlptLevelId { get; set; }
        public JlptLevel JlptLevel { get; set; }
        public int CourseCategoryId { get; set; }
        public CourseCategory CourseCategory { get; set; }

        public int ContentAuthorId { get; set; }
        public UserAccount ContentAuthor { get; set; }
        public int? ReviewedById { get; set; }
        public UserAccount? ReviewedBy { get; set; }

        public List<Lesson>? Lessons { get; set; }
    }
}
