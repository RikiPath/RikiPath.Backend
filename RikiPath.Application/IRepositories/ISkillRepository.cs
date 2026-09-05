using Domain.Entities;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ISkillRepository : IGenericRepository<Skill>
    {
        Task<Skill?> GetByNameAsync(string name);
    }
}
