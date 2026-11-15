namespace RikiPath.Application.Responses.MentorDiscovery;

public sealed class PublicMentorDailySlotsResponse
{
    public DateOnly Date { get; set; }
    public int AvailableSlotsCount => Slots.Count;
    public List<PublicMentorSlotResponse> Slots { get; set; } = new();
}
