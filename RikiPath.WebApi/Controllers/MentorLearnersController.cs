using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Mentor")]
[Route("api/mentor/learners")]
public sealed class MentorLearnersController(IMentorLearnerSkillProfileService skillProfiles) : ControllerBase
{
    [HttpGet("{learnerId:int}/skill-profile")]
    public async Task<IActionResult> GetSkillProfile(
        int learnerId,
        [FromQuery] int? cohortId,
        CancellationToken cancellationToken)
    {
        var result = await skillProfiles.GetSkillProfileAsync(learnerId, cohortId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
