using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IJlptLevelRepository : IGenericRepository<JlptLevel>
    {
        Task<JlptLevel?> GetByNameAsync(string name);
        Task<List<JlptLevel>> GetAllOrderedAsync();
    }
}
