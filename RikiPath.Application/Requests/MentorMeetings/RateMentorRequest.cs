using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.MentorMeetings;

public sealed class RateMentorRequest
{
    [Range(1, 5)]
    public int Rating { get; set; }
}
