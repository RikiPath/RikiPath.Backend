using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IKanjiRepository : IGenericRepository<Kanji>
    {
        Task<(List<Kanji> Items, int TotalCount)> SearchAsync(
            int? jlptLevelId,
            ContentStatus? status,
            string? keyword,
            int? minStroke,
            int? maxStroke,
            int pageIndex,
            int pageSize);
        Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword);
        Task<List<Kanji>> GetPendingReviewAsync();
    }
}
