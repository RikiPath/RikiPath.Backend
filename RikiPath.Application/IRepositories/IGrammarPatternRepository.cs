using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IGrammarPatternRepository : IGenericRepository<GrammarPattern>
    {
        Task<(List<GrammarPattern> Items, int TotalCount)> SearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword, int pageIndex, int pageSize);
        Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword);
        Task<List<GrammarPattern>> GetPendingReviewAsync();
    }
}
