//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using RikiPath.Application.Requests.Feedback;
//using RikiPath.Infrastructure.Services;

//namespace RikiPath.WebApi.Controllers;

//[ApiController]
//[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
//public sealed class LearnerFeedbackController(LearnerFeedbackService feedback) : ControllerBase
//{
//    [HttpGet("api/learner/feedback")]
//    [Authorize(Roles = "Learner")]
//    public async Task<IActionResult> ListMine([FromQuery] string? type, CancellationToken ct, [FromQuery] int page = 1)
//    {
//        var result = await feedback.ListAsync(false, type, page, ct);
//        return StatusCode(result.StatusCode, result);
//    }

//    [HttpGet("api/admin/learner-feedback")]
//    [Authorize(Roles = "Admin")]
//    public async Task<IActionResult> ListAdmin([FromQuery] string? type, CancellationToken ct, [FromQuery] int page = 1)
//    {
//        var result = await feedback.ListAsync(true, type, page, ct);
//        return StatusCode(result.StatusCode, result);
//    }

//    [HttpGet("api/learner/feedback/{type}/{id:int}")]
//    [Authorize(Roles = "Learner")]
//    public async Task<IActionResult> GetMine(string type, int id, CancellationToken ct)
//    {
//        var result = await feedback.GetMineAsync(type, id, ct);
//        return StatusCode(result.StatusCode, result);
//    }

//    [HttpPut("api/learner/feedback/{type}/{id:int}")]
//    [Authorize(Roles = "Learner")]
//    public async Task<IActionResult> SaveMine(string type, int id, SaveLearnerFeedbackRequest request, CancellationToken ct)
//    {
//        var result = await feedback.SaveMineAsync(type, id, request, ct);
//        return StatusCode(result.StatusCode, result);
//    }
//}
