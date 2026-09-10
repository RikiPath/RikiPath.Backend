using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Payments;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoursePurchaseController(ICoursePurchaseService coursePurchaseService) : ControllerBase
    {
        /// <summary>
        /// Create a checkout for purchasing a course. Returns a checkout URL/QR code and purchase id.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Checkout khi learner chọn mua khóa học.
        /// - Luồng xử lý: Tạo CoursePurchase (status = Pending), gọi payment gateway để tạo payment link.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Nếu đã mua khóa học sẽ trả lỗi.
        /// </remarks>
        /// <param name="courseId">ID of the course to purchase.</param>
        /// <param name="cancellationToken">Cancellation token (optional).</param>
        /// <response code="200">Success: returns checkout info (checkout URL, qr code, purchase id).</response>
        /// <response code="400">Bad request (invalid course or already purchased).</response>
        /// <response code="401">Unauthorized: missing token.</response>
        /// <response code="500">Server error.</response>
        [HttpPost("checkout/{courseId:int}")]
        [Authorize(Roles = "Learner")]
        public async Task<IActionResult> CreateCheckout([FromRoute] int courseId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await coursePurchaseService.CreateCheckoutAsync(userId, courseId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// PayOS webhook endpoint for course purchase payment updates.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: N/A (webhook).
        /// - Luồng xử lý: Verify signature and update CoursePurchase.PaymentStatus accordingly.
        /// - Lưu ý: This endpoint should be callable by the payment provider; allow anonymous.
        /// </remarks>
        /// <param name="webhook">Payload from PayOS.</param>
        /// <param name="cancellationToken">Cancellation token (optional).</param>
        /// <response code="200">Webhook processed.</response>
        /// <response code="400">Invalid payload or signature.</response>
        /// <response code="500">Server error.</response>
        [HttpPost("payos/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PayOsWebhook([FromBody] PayOsWebhookRequest webhook, CancellationToken cancellationToken)
        {
            await coursePurchaseService.HandlePayOsWebhookAsync(webhook, cancellationToken);
            return Ok();
        }

        /// <summary>
        /// Get status of a specific course purchase for the authenticated learner.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Purchase detail / polling from FE.
        /// - Luồng xử lý: Return payment status and purchase metadata.
        /// </remarks>
        /// <param name="purchaseId">Purchase id to check.</param>
        /// <param name="cancellationToken">Cancellation token (optional).</param>
        /// <response code="200">Success: returns purchase status.</response>
        /// <response code="401">Unauthorized.</response>
        /// <response code="403">Forbidden if user cannot access the purchase.</response>
        /// <response code="404">Not found.</response>
        [HttpGet("purchases/{purchaseId:int}/status")]
        [Authorize(Roles = "Learner")]
        public async Task<IActionResult> GetStatus([FromRoute] int purchaseId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await coursePurchaseService.GetStatusAsync(userId, purchaseId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Get list of courses purchased by the authenticated learner.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: My Purchases / Library.
        /// </remarks>
        /// <param name="cancellationToken">Cancellation token (optional).</param>
        /// <response code="200">Success: returns list of purchased courses.</response>
        [HttpGet("my-purchases")]
        [Authorize(Roles = "Learner")]
        public async Task<IActionResult> GetMyPurchases(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await coursePurchaseService.GetMyPurchasedCoursesAsync(userId, cancellationToken);
            return Ok(result);
        }
    }
}
