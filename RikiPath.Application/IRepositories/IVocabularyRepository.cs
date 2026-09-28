using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IVocabularyRepository : IGenericRepository<Vocabulary>
    {
        Task<(List<Vocabulary> items, int total)> SearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword, int pageIndex, int pageSize);
        Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword);
        Task<List<Vocabulary>> GetPendingReviewAsync();
    }
}
