using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Reviews;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class ReviewController(IReviewService reviewService) : ControllerBase
    {
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
        {
            var response = await reviewService.GetSummaryAsync(GetCurrentUserId(), cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("daily-queue")]
        public async Task<IActionResult> GetDailyQueue(
            [FromQuery] string mode = "flip",
            [FromQuery] string? cardType = null,
            [FromQuery] string? jlptLevel = null,
            [FromQuery] string? sourceType = null,
            CancellationToken cancellationToken = default)
        {
            var response = await reviewService.GetDailyReviewQueueAsync(
                GetCurrentUserId(),
                mode,
                cardType,
                jlptLevel,
                sourceType,
                cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("cards")]
        public async Task<IActionResult> GetCardLibrary(
            [FromQuery] SrsCardStatus? status = null,
            [FromQuery] SrsLearningState? learningState = null,
            [FromQuery] string? cardType = null,
            [FromQuery] string? jlptLevel = null,
            [FromQuery] string? sourceType = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default)
        {
            var response = await reviewService.GetCardLibraryAsync(
                GetCurrentUserId(),
                status,
                learningState,
                cardType,
                jlptLevel,
                sourceType,
                search,
                page,
                pageSize,
                cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("cards/{cardId:int}")]
        public async Task<IActionResult> GetCardById(int cardId, CancellationToken cancellationToken)
        {
            var response = await reviewService.GetCardByIdAsync(GetCurrentUserId(), cardId, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("cards/{cardId:int}/history")]
        public async Task<IActionResult> GetCardHistory(int cardId, CancellationToken cancellationToken)
        {
            var response = await reviewService.GetCardHistoryAsync(GetCurrentUserId(), cardId, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("preview-enroll")]
        public async Task<IActionResult> PreviewEnroll(
            [FromBody] EnrollSrsCardRequest request,
            CancellationToken cancellationToken)
        {
            var response = await reviewService.PreviewEnrollAsync(GetCurrentUserId(), request, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        // Feature control: suspending SRS blocks starting/adding new study, never finishing an in-progress session.
        [RequireFeature(FeatureCodes.SrsStudy)]
        [HttpPost("enroll")]
        public async Task<IActionResult> EnrollCard(
            [FromBody] EnrollSrsCardRequest request,
            CancellationToken cancellationToken)
        {
            var response = await reviewService.EnrollCardAsync(GetCurrentUserId(), request, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [RequireFeature(FeatureCodes.SrsStudy)]
        [HttpPost("enroll-batch")]
        public async Task<IActionResult> EnrollBatch(
            [FromBody] BatchEnrollSrsCardsRequest request,
            CancellationToken cancellationToken)
        {
            var response = await reviewService.BatchEnrollCardsAsync(GetCurrentUserId(), request, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("cards/{cardId:int}")]
        public async Task<IActionResult> UpdateCard(
            int cardId,
            [FromBody] UpdateSrsCardRequest request,
            CancellationToken cancellationToken)
        {
            var response = await reviewService.UpdateCardAsync(GetCurrentUserId(), cardId, request, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("cards/{cardId:int}/suspend")]
        public async Task<IActionResult> SuspendCard(int cardId, CancellationToken cancellationToken)
        {
            var response = await reviewService.SuspendCardAsync(GetCurrentUserId(), cardId, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("cards/{cardId:int}/resume")]
        public async Task<IActionResult> ResumeCard(int cardId, CancellationToken cancellationToken)
        {
            var response = await reviewService.ResumeCardAsync(GetCurrentUserId(), cardId, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("cards/{cardId:int}/restore")]
        public async Task<IActionResult> RestoreCard(int cardId, CancellationToken cancellationToken)
        {
            var response = await reviewService.RestoreCardAsync(GetCurrentUserId(), cardId, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("cards/{cardId:int}")]
        public async Task<IActionResult> SoftDeleteCard(int cardId, CancellationToken cancellationToken)
        {
            var response = await reviewService.SoftDeleteCardAsync(GetCurrentUserId(), cardId, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
        {
            var response = await reviewService.GetPreferencesAsync(GetCurrentUserId(), cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferences(
            [FromBody] UpdateUserSrsPreferenceRequest request,
            CancellationToken cancellationToken)
        {
            var response = await reviewService.UpdatePreferencesAsync(GetCurrentUserId(), request, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("submit-result")]
        public async Task<IActionResult> SubmitResult(
            [FromBody] SubmitReviewRequest request,
            CancellationToken cancellationToken)
        {
            var response = await reviewService.SubmitReviewResultAsync(GetCurrentUserId(), request, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [RequireFeature(FeatureCodes.SrsStudy)]
        [HttpPost("sessions/start")]
        public async Task<IActionResult> StartSession(
            [FromBody] StartSrsSessionRequest request,
            CancellationToken cancellationToken)
        {
            var response = await reviewService.StartSessionAsync(GetCurrentUserId(), request ?? new(), cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("sessions/active")]
        public async Task<IActionResult> GetActiveSession(
            [FromQuery] string? sessionKind = null,
            CancellationToken cancellationToken = default)
        {
            var response = await reviewService.GetActiveSessionAsync(GetCurrentUserId(), sessionKind, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("sessions/{sessionKey:guid}/finish")]
        public async Task<IActionResult> FinishSession(Guid sessionKey, CancellationToken cancellationToken)
        {
            var response = await reviewService.FinishSessionAsync(GetCurrentUserId(), sessionKey, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("sessions/{sessionKey:guid}/abandon")]
        public async Task<IActionResult> AbandonSession(Guid sessionKey, CancellationToken cancellationToken)
        {
            var response = await reviewService.AbandonSessionAsync(GetCurrentUserId(), sessionKey, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("calendar")]
        public async Task<IActionResult> GetCalendar(
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            var utcNow = DateTime.UtcNow;
            var response = await reviewService.GetReviewCalendarAsync(
                GetCurrentUserId(),
                year ?? utcNow.Year,
                month ?? utcNow.Month,
                cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        private int GetCurrentUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
    }
}
