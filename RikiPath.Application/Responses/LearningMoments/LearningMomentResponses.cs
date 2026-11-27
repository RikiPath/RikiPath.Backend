namespace RikiPath.Application.Responses.LearningMoments
{
    /// <summary>
    /// One celebration-worthy moment derived by the server from verified learning data (never from a
    /// client-declared completion). The client renders copy/visuals from <see cref="Type"/> + <see cref="Data"/>.
    /// </summary>
    public class LearningMomentResponse
    {
        /// <summary>Stable occurrence key, unique per learner + moment (e.g. <c>moment:first-lesson:42</c>).</summary>
        public string OccurrenceKey { get; set; } = string.Empty;

        /// <summary>ONBOARDING_COMPLETED, PLACEMENT_COMPLETED, LEARNING_PATH_CREATED, FIRST_LESSON_COMPLETED, DAILY_SRS_COMPLETED, MOCK_TEST_COMPLETED.</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary><c>major</c> (prominent dialog) or <c>milestone</c> (compact dialog).</summary>
        public string Level { get; set; } = "milestone";

        /// <summary>Lower value = shown first.</summary>
        public int Priority { get; set; }

        public DateTime? AchievedAtUtc { get; set; }

        /// <summary>Verified route for the primary action, chosen from the learner's real state.</summary>
        public string? CtaHref { get; set; }

        /// <summary>Small, display-only facts (level codes, lesson title, counts). Never fabricated.</summary>
        public Dictionary<string, string> Data { get; set; } = [];

        /// <summary>
        /// Purchase moments only: the features the purchased plan grants, from the plan's real feature bindings.
        /// <see cref="LearningMomentBenefitResponse.IsNew"/> is false when another active order already granted it.
        /// </summary>
        public List<LearningMomentBenefitResponse> Benefits { get; set; } = [];
    }

    public class LearningMomentBenefitResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = "general";
        public bool IsNew { get; set; } = true;
    }

    public class PendingLearningMomentsResponse
    {
        public List<LearningMomentResponse> Items { get; set; } = [];
        public DateTime ServerUtcNow { get; set; }
    }

    public class ClaimLearningMomentResponse
    {
        /// <summary>True only for the single request that won the claim; other tabs/devices get false.</summary>
        public bool Claimed { get; set; }
        public LearningMomentResponse? Moment { get; set; }
    }

    /// <summary>An achievement already earned, for the learner's achievement history.</summary>
    public class LearnerAchievementResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int? Value { get; set; }
        public DateTime? AchievedAtUtc { get; set; }
    }

    public class LearnerAchievementsResponse
    {
        public List<LearnerAchievementResponse> Achieved { get; set; } = [];

        /// <summary>The nearest real next goals (e.g. next streak badge), for gentle motivation.</summary>
        public List<LearnerAchievementResponse> Next { get; set; } = [];
    }
}
