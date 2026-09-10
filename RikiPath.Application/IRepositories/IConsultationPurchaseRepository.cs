using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IConsultationPurchaseRepository : IGenericRepository<ConsultationPurchase>
    {
        Task<List<ConsultationPurchase>> GetByUserAsync(int userId);
        Task<ConsultationPurchase?> GetByTransactionIdAsync(string paymentTransactionId);
    }
}
