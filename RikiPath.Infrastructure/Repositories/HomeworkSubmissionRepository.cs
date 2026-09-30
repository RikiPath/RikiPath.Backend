using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
namespace RikiPath.Infrastructure.Repositories;
public class HomeworkSubmissionRepository(AppDbContext context) : GenericRepository<HomeworkSubmission>(context), IHomeworkSubmissionRepository { }