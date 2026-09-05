using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IConsultationRequestRepository : IGenericRepository<ConsultationRequest>
    {
        Task<List<ConsultationRequest>> GetQueueForConsultantAsync(int consultantId);
        Task<List<ConsultationRequest>> GetPendingAssignmentAsync();
    }
}
