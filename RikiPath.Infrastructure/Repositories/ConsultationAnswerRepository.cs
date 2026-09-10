using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RikiPath.Infrastructure.Repositories
{
    public class ConsultationAnswerRepository : GenericRepository<ConsultationAnswer>, IConsultationAnswerRepository
    {
        public ConsultationAnswerRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<ConsultationAnswer?> GetByRequestIdAsync(int consultationRequestId)
            => await _context.ConsultationAnswers
                .FirstOrDefaultAsync(x => x.ConsultationRequestId == consultationRequestId);
    }
}
