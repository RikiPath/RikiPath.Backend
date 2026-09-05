using System;

namespace Domain.Entities
{
    /// <summary>
    /// A writing or kanji-practice submission from a learner. TextContent is used for
    /// Writing submissions, ImageUrl for handwritten/typed Kanji submissions. JlptLevelId
    /// is required so the AI grading pipeline knows which rubric to grade against.
    /// </summary>
    public class PracticeSubmission : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }

        public SubmissionType Type { get; set; }
        public string? TextContent { get; set; }
        public string? ImageUrl { get; set; }

        public int JlptLevelId { get; set; }
        public JlptLevel JlptLevel { get; set; }

        public DateTime SubmittedAt { get; set; }

        public GradingResult? GradingResult { get; set; }
    }
}
