using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IRepositories
{
    public interface IVocabularyRepository : IGenericRepository<Vocabulary>
    {
        Task<(List<Vocabulary> items, int total)> SearchAsync(
            int? jlptLevelId,
            string? keyword,
            int pageIndex,
            int pageSize);
        Task<(List<Vocabulary> items, int total)> SearchPublicAsync(
            int? jlptLevelId,
            string? keyword,
            int pageIndex,
            int pageSize,
            CancellationToken cancellationToken = default);
        Task<Vocabulary?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<int> CountSearchAsync(int? jlptLevelId, string? keyword);
    }
}
