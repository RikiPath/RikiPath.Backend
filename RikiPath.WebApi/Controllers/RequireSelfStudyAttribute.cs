using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RikiPath.Application.Responses;
using RikiPath.Application.Services;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

public sealed class RequireSelfStudyAttribute() : TypeFilterAttribute(typeof(RequireSelfStudyFilter));

public sealed class RequireSelfStudyFilter(SubscriptionAccessService subscriptions) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var decision = await subscriptions.EvaluateFeatureAsync(FeatureCodes.LessonsContent, context.HttpContext.RequestAborted);
        if (decision.Allowed) return;
        var response = ApiResponse<object>.Fail(
            decision.Message ?? "Bạn cần gói tự học đang hiệu lực.",
            decision.StatusCode,
            errorCode: decision.ErrorCode ?? "SELF_STUDY_PACKAGE_REQUIRED");
        context.Result = new ObjectResult(response) { StatusCode = response.StatusCode };
    }
}

public sealed class RequireFeatureAttribute : TypeFilterAttribute
{
    public RequireFeatureAttribute(string featureCode) : base(typeof(RequireFeatureFilter))
    {
        Arguments = [featureCode];
    }
}

public sealed class RequireFeatureFilter(string featureCode, SubscriptionAccessService subscriptions) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var decision = await subscriptions.EvaluateFeatureAsync(featureCode, context.HttpContext.RequestAborted);
        if (decision.Allowed) return;
        var response = ApiResponse<object>.Fail(
            decision.Message ?? "Bạn chưa có quyền truy cập tính năng này.",
            decision.StatusCode,
            errorCode: decision.ErrorCode ?? "FEATURE_ACCESS_DENIED");
        context.Result = new ObjectResult(response) { StatusCode = response.StatusCode };
    }
}
