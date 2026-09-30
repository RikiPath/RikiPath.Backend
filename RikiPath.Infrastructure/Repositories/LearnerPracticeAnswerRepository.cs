using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
namespace RikiPath.Infrastructure.Repositories;
public class LearnerPracticeAnswerRepository(AppDbContext context) : GenericRepository<LearnerPracticeAnswer>(context), ILearnerPracticeAnswerRepository { }