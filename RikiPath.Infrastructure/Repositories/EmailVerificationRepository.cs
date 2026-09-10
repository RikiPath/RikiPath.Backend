using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;

namespace RikiPath.Infrastructure.Repositories
{
    public class EmailVerificationRepository : GenericRepository<EmailVerification>, IEmailVerificationRepository
    {
        public EmailVerificationRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<EmailVerification?> GetValidCodeAsync(int userId, string verificationCode)
            => await _context.Set<EmailVerification>()
                .FirstOrDefaultAsync(x => x.UserId == userId
                    && x.VerificationCode == verificationCode
                    && !x.IsUsed);
    }
}
