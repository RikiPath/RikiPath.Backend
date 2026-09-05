using Domain.Entities;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ICourseCategoryRepository : IGenericRepository<CourseCategory>
    {
        Task<CourseCategory?> GetByNameAsync(string name);
    }
}
