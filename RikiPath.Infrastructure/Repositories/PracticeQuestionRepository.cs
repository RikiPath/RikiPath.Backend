using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
namespace RikiPath.Infrastructure.Repositories;
public class PracticeQuestionRepository(AppDbContext context) : GenericRepository<PracticeQuestion>(context), IPracticeQuestionRepository { }