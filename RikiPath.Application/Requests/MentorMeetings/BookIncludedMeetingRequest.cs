namespace RikiPath.Application.Requests.MentorMeetings;

public sealed class BookIncludedMeetingRequest
{
    private string? _question;

    public int MentorAvailabilityId { get; set; }
    public string? JlptLevel { get; set; }
    public string? Question
    {
        get => _question ?? Notes;
        set => _question = value;
    }
    public string? Notes { get; set; }
    public string? OperationKey { get; set; }
}
