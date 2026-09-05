using System.Collections.Generic;

namespace Domain.Entities
{
    /// <summary>Course category master data — Admin-configurable.</summary>
    public class CourseCategory : Base
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }

        public List<Course>? Courses { get; set; }
    }
}
