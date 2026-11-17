using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Authorization;

/// <summary>
/// Chặn các thao tác tính lương, thu nhập và chi trả thù lao cho Cố vấn khi phạm vi sản phẩm đã loại bỏ Mentor Payroll/Payout.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class MentorFinanceDisabledAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetService<IConfiguration>();
        var enabled = configuration?.GetValue<bool>("MentorFinance:PayrollAndPayoutEnabled") ?? false;
        if (enabled)
            return;

        var payload = ApiResponse<object>.Fail(
            "Nghiệp vụ tính lương, thu nhập và chi trả thù lao cho Cố vấn đã ngừng hỗ trợ. Vui lòng sử dụng mục Chấm công & KPI.",
            HttpStatusCode.Forbidden,
            errorCode: "MENTOR_FINANCE_OUT_OF_SCOPE");

        context.Result = new ObjectResult(payload)
        {
            StatusCode = (int)HttpStatusCode.Forbidden
        };
    }
}
