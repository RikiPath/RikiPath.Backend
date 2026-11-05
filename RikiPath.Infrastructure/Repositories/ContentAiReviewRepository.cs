using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class ContentAiReviewRepository(AppDbContext context) : GenericRepository<ContentAiReview>(context), IContentAiReviewRepository
    {
    }
}
