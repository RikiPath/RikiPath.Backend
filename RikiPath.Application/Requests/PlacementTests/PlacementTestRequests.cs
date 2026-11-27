using RikiPath.Application.Requests.MockTests;

namespace RikiPath.Application.Requests.PlacementTests
{
    public class StartPlacementTestRequest
    {
        public int MockTestId { get; set; }
        public bool RestartExisting { get; set; }
        public bool RetakeTargetAssessment { get; set; }

        /// <summary>Optional JLPT level ID (N5-N2) selected specifically for this assessment without mutating TargetCertificateLevelId.</summary>
        public int? AssessmentLevelId { get; set; }

        /// <summary>COMPREHENSIVE (multi-level estimation) or TARGETED (specific level). Optional.</summary>
        public string? AssessmentMode { get; set; }
    }

    public class SubmitPlacementTestRequest
    {
        public List<AnswerSubmissionItem> Answers { get; set; } = new();
    }
}
