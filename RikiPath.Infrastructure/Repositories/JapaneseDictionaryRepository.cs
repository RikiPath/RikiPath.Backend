using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories;

public class JapaneseDictionaryRepository(AppDbContext context)
    : GenericRepository<JapaneseDictionaryEntry>(context), IJapaneseDictionaryRepository
{
    public async Task<List<JapaneseDictionaryEntry>> SearchAsync(
        int? userId,
        string readingKana,
        CancellationToken cancellationToken = default)
        => await _context.JapaneseDictionaryEntries
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.ReadingKana == readingKana
                && (x.UserId == null || x.UserId == userId))
            .OrderByDescending(x => x.UserId == userId)
            .ThenBy(x => x.Surface)
            .Take(20)
            .ToListAsync(cancellationToken);
}
