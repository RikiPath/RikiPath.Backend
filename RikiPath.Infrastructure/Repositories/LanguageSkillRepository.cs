using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class LanguageSkillRepository : GenericRepository<LanguageSkill>, ILanguageSkillRepository
    {
        public LanguageSkillRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<LanguageSkill?> GetByNameAsync(string name)
            => await _context.LanguageSkills.FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower());
    }
}
