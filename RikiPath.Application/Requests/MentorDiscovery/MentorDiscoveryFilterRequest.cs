namespace RikiPath.Application.Requests.MentorDiscovery;

public sealed class MentorDiscoveryFilterRequest
{
    public string? SearchTerm { get; set; }
    public string? Expertise { get; set; }
    public string? JlptLevel { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
}
