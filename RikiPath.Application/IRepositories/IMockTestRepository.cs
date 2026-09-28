using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMockTestRepository : IGenericRepository<MockTest>
    {
        Task<List<MockTest>> SearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword, int pageIndex, int pageSize);
        Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword);
        Task<List<MockTest>> GetPendingReviewAsync();
        Task<MockTest?> GetWithSectionsAndQuestionsAsync(int practiceTestId);
        Task<List<MockTest>> GetByLevelAsync(int jlptLevelId);
    }
}
