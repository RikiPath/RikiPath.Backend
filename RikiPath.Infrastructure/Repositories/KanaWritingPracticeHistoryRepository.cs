using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
namespace RikiPath.Infrastructure.Repositories
{
    public class KanaWritingPracticeHistoryRepository(AppDbContext context) : GenericRepository<KanaWritingPracticeHistory>(context), IKanaWritingPracticeHistoryRepository { }
}
