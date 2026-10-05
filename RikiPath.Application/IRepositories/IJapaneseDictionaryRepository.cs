using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories;

public interface IJapaneseDictionaryRepository : IGenericRepository<JapaneseDictionaryEntry>
{
    Task<List<JapaneseDictionaryEntry>> SearchAsync(
        int? userId,
        string readingKana,
        CancellationToken cancellationToken = default);
}
