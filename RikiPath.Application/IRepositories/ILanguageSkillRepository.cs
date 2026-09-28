using RikiPath.Domain.Entities;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILanguageSkillRepository : IGenericRepository<LanguageSkill>
    {
        Task<LanguageSkill?> GetByNameAsync(string name);
    }
}
