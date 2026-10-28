using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class MockTestAnswerRepository : GenericRepository<MockTestAnswer>, IMockTestAnswerRepository
    {
        public MockTestAnswerRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<MockTestAnswer>> GetByAttemptIdAsync(
            int practiceTestAttemptId,
            CancellationToken cancellationToken = default)
            => await _context.MockTestAnswers
                .Include(x => x.MockQuestion)
                    .ThenInclude(q => q.Options)
                .Include(x => x.MockQuestion)
                    .ThenInclude(q => q.MockTestSection)
                        .ThenInclude(s => s.MockTest)
                            .ThenInclude(t => t.CertificateLevel)
                .Include(x => x.MockQuestion)
                    .ThenInclude(q => q.MockTestSection)
                        .ThenInclude(s => s.LanguageSkill)
                .Include(x => x.SelectedOption)
                .Where(x => x.MockTestAttemptId == practiceTestAttemptId)
                .OrderBy(x => x.MockQuestion.MockTestSection.SortOrder)
                .ThenBy(x => x.MockQuestion.SortOrder)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
    }
}
