using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Responses.CoursePurchases;

namespace RikiPath.Application.IServices
{
    public interface ICoursePurchaseService
    {
        Task<CoursePurchaseCheckoutResponse> CreateCheckoutAsync(int userId, int courseId, CancellationToken cancellationToken);

        Task HandlePayOsWebhookAsync(PayOsWebhookRequest webhook, CancellationToken cancellationToken);

        Task<CoursePurchaseStatusResponse> GetStatusAsync(int userId, int purchaseId, CancellationToken cancellationToken);

        Task<List<PurchasedCourseResponse>> GetMyPurchasedCoursesAsync(int userId, CancellationToken cancellationToken);
    }
}
