using Domain.Enums;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface ICoursePurchaseRepository : IGenericRepository<CoursePurchase>
    {
        Task<List<CoursePurchase>> SearchAsync(int? userId, int? courseId,
            PaymentStatus? status, int pageIndex, int pageSize);

        Task<int> CountSearchAsync(int? userId, int? courseId, PaymentStatus? status);

        Task<CoursePurchase?> GetByUserAndCourseAsync(int userId, int courseId);

        Task<CoursePurchase?> GetByOrderCodeAsync(long orderCode);

        Task<bool> HasPurchasedCourseAsync(int userId, int courseId);

        Task<List<CoursePurchase>> GetPurchasedCoursesByUserAsync(int userId);
    }
}
