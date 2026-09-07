using Domain.Entities;
using Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IVocabularyEntryRepository : IGenericRepository<VocabularyEntry>
    {
        Task<(List<VocabularyEntry> items, int total)> SearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword, int pageIndex, int pageSize);
        Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword);
        Task<List<VocabularyEntry>> GetPendingReviewAsync();
    }
}
