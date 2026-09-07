using Domain.Entities;
using Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IKanjiEntryRepository : IGenericRepository<KanjiEntry>
    {
        Task<(List<KanjiEntry> Items, int TotalCount)> SearchAsync(
            int? jlptLevelId,
            ContentStatus? status,
            string? keyword,
            int? minStroke,
            int? maxStroke,
            int pageIndex,
            int pageSize);
        Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword);
        Task<List<KanjiEntry>> GetPendingReviewAsync();
    }
}
