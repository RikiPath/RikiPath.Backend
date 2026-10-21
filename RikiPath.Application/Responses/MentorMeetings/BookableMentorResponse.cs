namespace RikiPath.Application.Responses.MentorMeetings;

public sealed class BookableMentorResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}
