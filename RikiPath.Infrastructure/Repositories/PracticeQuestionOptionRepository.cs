using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
namespace RikiPath.Infrastructure.Repositories;
public class PracticeQuestionOptionRepository(AppDbContext context) : GenericRepository<PracticeQuestionOption>(context), IPracticeQuestionOptionRepository { }