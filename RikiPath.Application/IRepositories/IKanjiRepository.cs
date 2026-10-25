using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IKanjiRepository : IGenericRepository<Kanji>
    {
        Task<List<Kanji>> GetDictionaryEntriesAsync(CancellationToken cancellationToken = default);
        Task<(List<Kanji> Items, int TotalCount)> SearchAsync(
            int? jlptLevelId,
            string? keyword,
            int? minStroke,
            int? maxStroke,
            int pageIndex,
            int pageSize);
        Task<(List<Kanji> Items, int TotalCount)> SearchPublicAsync(
            int? jlptLevelId,
            string? keyword,
            int? minStroke,
            int? maxStroke,
            int pageIndex,
            int pageSize,
            CancellationToken cancellationToken = default);
        Task<Kanji?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<int> CountSearchAsync(int? jlptLevelId, string? keyword);
    }
}
