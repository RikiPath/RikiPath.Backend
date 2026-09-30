using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
namespace RikiPath.Infrastructure.Repositories;
public class PracticeExerciseAttemptRepository(AppDbContext context) : GenericRepository<PracticeExerciseAttempt>(context), IPracticeExerciseAttemptRepository { }