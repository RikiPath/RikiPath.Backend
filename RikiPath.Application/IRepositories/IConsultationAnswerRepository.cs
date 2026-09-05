using Domain.Entities;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IConsultationAnswerRepository : IGenericRepository<ConsultationAnswer>
    {
        Task<ConsultationAnswer?> GetByRequestIdAsync(int consultationRequestId);
    }
}
