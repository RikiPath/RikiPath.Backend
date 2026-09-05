using RikiPath.Application.IRepositories;
using RikiPath.Infrastructure;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class UserAccountRepository : GenericRepository<UserAccount>, IUserAccountRepository
    {
        public UserAccountRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<UserAccount?> GetByEmailAsync(string email)
            => await _context.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower());

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
