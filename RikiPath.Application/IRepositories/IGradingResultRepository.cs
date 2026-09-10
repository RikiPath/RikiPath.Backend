using RikiPath.Domain.Entities;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IGradingResultRepository : IGenericRepository<GradingResult>
    {
        Task<GradingResult?> GetBySubmissionIdAsync(int practiceSubmissionId);
    }
}
