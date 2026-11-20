namespace RikiPath.Application.Responses.MentorMeetings;

public sealed record MentorRecommendationResponse(
    int Id, string Name, string? AvatarUrl, double? AverageRating,
    [property: System.Text.Json.Serialization.JsonIgnore] int ReviewCount,
    bool IsRecommended, IReadOnlyList<MentorAvailabilityResponse> Slots);
