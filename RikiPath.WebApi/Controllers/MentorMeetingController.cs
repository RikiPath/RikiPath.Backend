using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

/// <summary>HTTP endpoints for Mentor Meeting plans, availability, purchases, and bookings.</summary>
[ApiController]
[Route("api/mentor-meetings")]
public sealed class MentorMeetingController(IMentorMeetingService service) : ControllerBase
{
    [HttpGet("mentors")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMentors(CancellationToken cancellationToken)
         => ToActionResult(await service.GetMentorsAsync(cancellationToken));

    /// <summary>Lấy danh sách các gói tư vấn 1-1 khả dụng.</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
        => ToActionResult(await service.GetPlansAsync(cancellationToken));

    /// <summary>Tra cứu các khung giờ trống của Mentor theo khoảng thời gian.</summary>
    [HttpGet("available-slots")]
    [HttpGet("availability")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> GetAvailableSlots(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? mentorId,
        CancellationToken cancellationToken)
        => ToActionResult(await service.GetAvailableSlotsAsync(from, to, mentorId, cancellationToken));

    /// <summary>Creates a package checkout without reserving a mentor slot.</summary>
    [HttpPost("purchase")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> Purchase(
        [FromBody] PurchaseMentorPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FrontendOrigin))
            request.FrontendOrigin = RequestOrigin();
        return ToActionResult(await service.PurchaseAsync(request, cancellationToken));
    }

    private string? RequestOrigin()
    {
        var origin = Request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origin)) return origin;
        return Uri.TryCreate(Request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
            ? referer.GetLeftPart(UriPartial.Authority)
            : null;
    }

    /// <summary>Đặt buổi hẹn 1-1 sử dụng gói tư vấn đã đăng ký (Subscription).</summary>
    [HttpPost("subscriptions/{subscriptionId:int}/book")]
    [HttpPost("subscriptions/{subscriptionId:int}/bookings")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> BookIncludedSession(
        int subscriptionId,
        [FromBody] BookIncludedMeetingRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await service.BookIncludedSessionAsync(subscriptionId, request, cancellationToken));

    /// <summary>Lấy danh sách các buổi hẹn tư vấn cá nhân đã đặt.</summary>
    [HttpGet("my-bookings")]
    [HttpGet("bookings")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> GetMyBookings(CancellationToken cancellationToken)
        => ToActionResult(await service.GetMyBookingsAsync(cancellationToken));

    /// <summary>Xem trạng thái gói tư vấn đã mua theo Subscription ID.</summary>
    [HttpGet("subscriptions/{subscriptionId:int}/status")]
    [HttpGet("purchases/{subscriptionId:int}")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> GetPurchaseStatus(int subscriptionId, CancellationToken cancellationToken)
        => ToActionResult(await service.GetPurchaseStatusAsync(subscriptionId, cancellationToken));

    [HttpGet("purchases")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> GetMyPurchases(CancellationToken ct)
        => ToActionResult(await service.GetMyPurchasesAsync(ct));

    [HttpPost("bookings")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> BookFromBalance([FromBody] BookIncludedMeetingRequest request, CancellationToken ct)
        => ToActionResult(await service.BookFromBalanceAsync(request, ct));


    [HttpPost("purchases/{subscriptionId:int}/sync")]
    [HttpPost("subscriptions/{subscriptionId:int}/sync")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> SyncPurchase(int subscriptionId, CancellationToken cancellationToken)
        => ToActionResult(await service.SyncPurchaseAsync(subscriptionId, cancellationToken));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
