namespace RikiPath.Application.Responses.Sidebar;

public static class SidebarCounterKind
{
    public const string ActionRequired = "ActionRequired";
    public const string DueItems = "DueItems";
    public const string PendingUserAction = "PendingUserAction";
    public const string UnreadActivity = "UnreadActivity";
}

public sealed class SidebarCounterItemDto
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Kind { get; set; } = SidebarCounterKind.ActionRequired;
    public Dictionary<string, object?>? Metadata { get; set; }
}

public sealed class SidebarCountersResponse
{
    public string Role { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public long ServerVersionMs { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public List<SidebarCounterItemDto> Counters { get; set; } = [];
}
