using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class UserAccountRepository(AppDbContext context) : GenericRepository<UserAccount>(context), IUserAccountRepository
    {
        public async Task<UserAccount?> GetByEmailAsync(string email)
        {
            var normalizedEmail = email.Trim().ToLower();
            return await _context.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail);
        }

        public async Task<List<UserAccount>> GetByRoleAsync(Role role, int pageIndex, int pageSize)
            => await _context.Users
                .Where(x => x.Role == role)
                .OrderBy(x => x.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        public async Task<int> CountByRoleAsync(Role role)
            => await _context.Users.CountAsync(x => x.Role == role);
    }
}
