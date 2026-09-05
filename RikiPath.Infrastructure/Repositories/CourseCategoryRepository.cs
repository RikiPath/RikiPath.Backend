using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class CourseCategoryRepository : GenericRepository<CourseCategory>, ICourseCategoryRepository
    {
        public CourseCategoryRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<CourseCategory?> GetByNameAsync(string name)
            => await _context.CourseCategories.FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower());
    }
}
