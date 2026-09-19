using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class CertificationLevelSkillRepository(AppDbContext context) : GenericRepository<CertificationLevelSkill>(context), ICertificationLevelSkillRepository
    {
    }
}
