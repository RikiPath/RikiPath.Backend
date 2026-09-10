using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class ConsultationRequestRepository : GenericRepository<ConsultationRequest>, IConsultationRequestRepository
    {
        public ConsultationRequestRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<ConsultationRequest>> GetQueueForConsultantAsync(int consultantId)
            => await _context.ConsultationRequests
                .Include(x => x.ConsultationPurchase).ThenInclude(p => p.ConsultationPackage)
                .Include(x => x.ConsultationPurchase).ThenInclude(p => p.UserAccount)
                .Include(x => x.ConsultantAvailability)
                .Where(x => x.ConsultantId == consultantId)
                .OrderBy(x => x.ScheduledAt ?? x.CreatedDate)
                .ToListAsync();

        public async Task<List<ConsultationRequest>> GetPendingAssignmentAsync()
            => await _context.ConsultationRequests
                .Include(x => x.ConsultationPurchase).ThenInclude(p => p.ConsultationPackage)
                .Where(x => x.Status == ConsultationStatus.PendingAssignment)
                .OrderBy(x => x.CreatedDate)
                .ToListAsync();
    }
}
