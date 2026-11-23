using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Analytics
{
    /// <summary>
    /// Claims (acknowledges) today's streak celebration right before the client displays it.
    /// The server recomputes the event itself; <see cref="EventId"/> only guards against a stale client
    /// (e.g. a tab left open across midnight) and is never trusted as the source of the streak value.
    /// </summary>
    public class ClaimStreakCelebrationRequest
    {
        [Required(ErrorMessage = "Thiếu mã sự kiện chúc mừng.")]
        [MaxLength(64)]
        public string EventId { get; set; } = string.Empty;
    }
}
