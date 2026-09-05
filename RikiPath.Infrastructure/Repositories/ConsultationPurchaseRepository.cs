using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class ConsultationPurchaseRepository : GenericRepository<ConsultationPurchase>, IConsultationPurchaseRepository
    {
        public ConsultationPurchaseRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<ConsultationPurchase>> GetByUserAsync(int userId)
            => await _context.ConsultationPurchases
                .Include(x => x.ConsultationPackage)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.PurchasedAt)
                .ToListAsync();

        public async Task<ConsultationPurchase?> GetByTransactionIdAsync(string paymentTransactionId)
            => await _context.ConsultationPurchases
                .FirstOrDefaultAsync(x => x.PaymentTransactionId == paymentTransactionId);
    }
}
