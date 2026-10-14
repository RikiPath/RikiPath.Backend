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
        Task<int> CountSearchAsync(int? jlptLevelId, string? keyword);
    }
}
