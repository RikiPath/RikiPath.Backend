using Domain.Entities;
using Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IPracticeSubmissionRepository : IGenericRepository<PracticeSubmission>
    {
        Task<List<PracticeSubmission>> GetByUserAsync(int userId, SubmissionType? type);
        Task<PracticeSubmission?> GetWithGradingResultAsync(int id);
    }
}
