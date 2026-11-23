namespace RikiPath.Domain.Enums;

/// <summary>SM-2 memory maturity stage of a learner's SRS card.</summary>
public enum SrsLearningState
{
    New = 0,
    Learning = 1,
    Review = 2,
    Relearning = 3,
    Mastered = 4
}
