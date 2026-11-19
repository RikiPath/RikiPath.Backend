using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Feedback;

public sealed record SaveLearnerFeedbackRequest(
    [Range(1, 5)] int Rating,
    [StringLength(1000)] string? Comment);
