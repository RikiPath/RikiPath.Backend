using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

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
