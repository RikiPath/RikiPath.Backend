using System.Collections.Generic;

namespace Domain.Entities
{
    /// <summary>A single practice-test question. AudioUrl for listening, ImageUrl for reading/visual questions.</summary>
    public class PracticeQuestion : Base
    {
        public int Id { get; set; }
        public string QuestionText { get; set; }
        public string? AudioUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? Explanation { get; set; }
        public int SortOrder { get; set; }

        public int PracticeTestSectionId { get; set; }
        public PracticeTestSection PracticeTestSection { get; set; }

        public List<PracticeQuestionOption>? Options { get; set; }
        public List<PracticeTestAnswer>? Answers { get; set; }
    }
}
