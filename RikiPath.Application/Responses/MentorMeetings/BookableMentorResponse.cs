namespace RikiPath.Application.Responses.MentorMeetings;

public sealed class BookableMentorResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public List<string> TeachingJlptLevels { get; init; } = [];
    public string? Headline { get; init; }
    public string? Bio { get; init; }
    public string? TeachingMethod { get; init; }
}
