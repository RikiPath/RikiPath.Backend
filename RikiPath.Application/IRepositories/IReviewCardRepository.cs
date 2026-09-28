using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IReviewCardRepository : IGenericRepository<ReviewCard>
    {
        Task<List<ReviewCard>> GetDueForReviewAsync(int userId, DateTime asOf);
        Task<ReviewCard?> GetByNoteEntryIdAsync(int vocabularyNoteEntryId);
    }
}
