using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
namespace RikiPath.Infrastructure.Repositories;
public class HomeworkAssignmentRepository(AppDbContext context) : GenericRepository<HomeworkAssignment>(context), IHomeworkAssignmentRepository { }