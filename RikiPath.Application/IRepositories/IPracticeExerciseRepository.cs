using RikiPath.Domain.Entities;
namespace RikiPath.Application.IRepositories;
public interface IPracticeExerciseRepository : IGenericRepository<PracticeExercise>
{
    Task<PracticeExercise?> GetWithQuestionsAndOptionsAsync(int id, CancellationToken cancellationToken = default);
}
