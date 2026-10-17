using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IGrammarPatternRepository : IGenericRepository<GrammarPattern>
    {
        Task<(List<GrammarPattern> Items, int TotalCount)> SearchAsync(
             int? jlptLevelId,
             string? keyword,
             int pageIndex,
             int pageSize);
        Task<(List<GrammarPattern> Items, int TotalCount)> SearchPublicAsync(
             int? jlptLevelId,
             string? keyword,
             int pageIndex,
             int pageSize,
             CancellationToken cancellationToken = default);
        Task<GrammarPattern?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<int> CountSearchAsync(int? jlptLevelId, string? keyword);
    }
}
