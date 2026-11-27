namespace RikiPath.Application.Requests.MockTests
{
    public class SubmitAttemptRequest
    {
        public List<AnswerSubmissionItem> Answers { get; set; } = new();
        /// <summary>Monotonic sequence token from client to ignore stale out-of-order autosaves.</summary>
        public long? ClientSequence { get; set; }
        /// <summary>Elapsed active seconds reported on practice save/pause (validated server-side).</summary>
        public int? ActiveElapsedSeconds { get; set; }
    }

    public sealed class StartMockTestAttemptRequest
    {
        /// <summary>Only allowed for PracticeSet/MixedPractice when explicitly starting a new attempt and abandoning the active one.</summary>
        public bool ForceNew { get; set; }
    }
}
