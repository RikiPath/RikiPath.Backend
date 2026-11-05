using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IRepositories
{
    public interface IMockTestRepository : IGenericRepository<MockTest>
    {
        Task<List<MockTest>> SearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword, int pageIndex, int pageSize);
        Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword);
        Task<MockTest?> GetWithSectionsAndQuestionsAsync(
            int practiceTestId,
            CancellationToken cancellationToken = default);
        Task<List<MockTest>> GetAllWithSectionsAndQuestionsAsync(
            CancellationToken cancellationToken = default);
        Task<List<MockTest>> GetByLevelAsync(int jlptLevelId);
        Task<List<MockTest>> GetLearnerPlacementTestsByLevelAsync(int certificateLevelId, CancellationToken cancellationToken = default);
        Task<MockTest?> GetPlacementTestDetailByLevelAsync(int certificateLevelId, CancellationToken cancellationToken = default);

    }
}