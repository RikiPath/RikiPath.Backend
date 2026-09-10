using RikiPath.Application.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class ConsultantAvailabilityRepository : GenericRepository<ConsultantAvailability>, IConsultantAvailabilityRepository
    {
        public ConsultantAvailabilityRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<ConsultantAvailability>> GetByConsultantAsync(int consultantId)
            => await _context.ConsultantAvailabilities
                .Where(x => x.ConsultantId == consultantId)
                .OrderBy(x => x.StartTime)
                .ToListAsync();

        public async Task<List<ConsultantAvailability>> GetAvailableSlotsAsync(int? consultantId, DateTime from, DateTime to)
        {
            IQueryable<ConsultantAvailability> query = _context.ConsultantAvailabilities
                .Where(x => !x.IsBooked && x.StartTime >= from && x.EndTime <= to);

            if (consultantId.HasValue)
                query = query.Where(x => x.ConsultantId == consultantId.Value);

            return await query.Include(x => x.Consultant).OrderBy(x => x.StartTime).ToListAsync();
        }
    }
}
