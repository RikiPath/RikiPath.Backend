using Domain.Entities;
using Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IPracticeTestRepository : IGenericRepository<PracticeTest>
    {
        Task<List<PracticeTest>> SearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword, int pageIndex, int pageSize);
        Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword);
        Task<List<PracticeTest>> GetPendingReviewAsync();
        Task<PracticeTest?> GetWithSectionsAndQuestionsAsync(int practiceTestId);
        Task<List<PracticeTest>> GetByLevelAsync(int jlptLevelId);
    }
}
