using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IGrammarPointRepository : IGenericRepository<GrammarPoint>
    {
        Task<(List<GrammarPoint> Items, int TotalCount)> SearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword, int pageIndex, int pageSize);
        Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword);
        Task<List<GrammarPoint>> GetPendingReviewAsync();
    }
}
