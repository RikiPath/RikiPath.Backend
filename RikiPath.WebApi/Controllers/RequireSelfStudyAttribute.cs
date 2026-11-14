using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RikiPath.Application.Responses;
using RikiPath.Application.Services;
using System.Net;

namespace RikiPath.WebApi.Controllers;

public sealed class RequireSelfStudyAttribute() : TypeFilterAttribute(typeof(RequireSelfStudyFilter));

public sealed class RequireSelfStudyFilter(SubscriptionAccessService subscriptions) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if ((await subscriptions.GetAccessAsync(context.HttpContext.RequestAborted)).Active) return;
        var response = ApiResponse<object>.Fail("Bạn cần gói tự học đang hiệu lực.", HttpStatusCode.Forbidden,
            errorCode: "SELF_STUDY_PACKAGE_REQUIRED");
        context.Result = new ObjectResult(response) { StatusCode = response.StatusCode };
    }
}
