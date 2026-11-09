using RikiPath.Application.Requests.ContentAuthoring;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.ContentAuthoring;

namespace RikiPath.Application.IServices
{
    /// <summary>
    /// Điều phối Plan → Execute → Confirm đặt trên <see cref="IContentAuthoringService"/>.
    /// Plan: AI chuyển yêu cầu tự do thành danh sách công việc. Execute: sinh nội dung từng việc (chưa lưu DB).
    /// Confirm: ghi cả gói vào ContentApprovalRequest (Draft) cùng một BatchId.
    /// </summary>
    public interface IContentPlanningService
    {
        Task<ApiResponse<ContentPlanResponse>> PlanContentAsync(
            PlanContentRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<ExecuteContentPlanResponse>> ExecutePlanAsync(
            ExecuteContentPlanRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmPlanAsync(
            ConfirmContentPlanRequest request, CancellationToken cancellationToken);
    }
}