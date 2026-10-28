namespace RikiPath.Domain.Enums;

/// <summary>Explicit learner choice; no test attempt is created for a skip.</summary>
public enum PlacementDecision
{
    NotTaken = 0,
    Skipped = 1,
    Completed = 2,
}
