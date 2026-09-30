using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
namespace RikiPath.Infrastructure.Repositories
{
    public class KanaWritingPracticeCardRepository(AppDbContext context) : GenericRepository<KanaWritingPracticeCard>(context), IKanaWritingPracticeCardRepository { }
}
