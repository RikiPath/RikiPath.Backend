using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IReviewItemRepository : IGenericRepository<ReviewItem>
    {
        Task<List<ReviewItem>> GetDueForReviewAsync(int userId, DateTime asOf);
        Task<ReviewItem?> GetByNoteEntryIdAsync(int vocabularyNoteEntryId);
    }
}
