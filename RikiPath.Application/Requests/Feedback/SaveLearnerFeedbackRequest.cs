using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Feedback;

public sealed record SaveLearnerFeedbackRequest(
    [property: Range(1, 5)] int Rating,
    [property: StringLength(1000)] string? Comment);

