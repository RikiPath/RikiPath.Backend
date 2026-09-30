using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace RikiPath.Infrastructure.Repositories;
public class PracticeExerciseRepository(AppDbContext context) : GenericRepository<PracticeExercise>(context), IPracticeExerciseRepository
{
    public Task<PracticeExercise?> GetWithQuestionsAndOptionsAsync(int id, CancellationToken cancellationToken = default)
        => context.PracticeExercises.Include(x => x.Questions).ThenInclude(x => x!.Options)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
}
