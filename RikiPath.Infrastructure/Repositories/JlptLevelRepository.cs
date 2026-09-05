using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class JlptLevelRepository : GenericRepository<JlptLevel>, IJlptLevelRepository
    {
        public JlptLevelRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<JlptLevel?> GetByNameAsync(string name)
            => await _context.JlptLevels.FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower());

        public async Task<List<JlptLevel>> GetAllOrderedAsync()
            => await _context.JlptLevels.OrderBy(x => x.SortOrder).ToListAsync();
    }
}
