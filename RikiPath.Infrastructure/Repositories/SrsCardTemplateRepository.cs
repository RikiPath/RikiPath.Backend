using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories;

public class SrsCardTemplateRepository(AppDbContext context)
    : GenericRepository<SrsCardTemplate>(context), ISrsCardTemplateRepository
{
    public async Task<List<SrsCardTemplate>> QueryTemplatesAsync(
        ContentStatus? status,
        string? jlptLevel,
        string? cardType,
        int? submittedByUserId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SrsCardTemplates
            .Include(x => x.SubmittedByUser)
            .Include(x => x.ReviewedByAdmin)
            .Where(x => !x.IsDeleted);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(jlptLevel))
        {
            var normalizedLevel = jlptLevel.Trim().ToUpperInvariant();
            query = query.Where(x => x.JlptLevel == normalizedLevel);
        }

        if (!string.IsNullOrWhiteSpace(cardType))
        {
            var normalizedTypeLower = cardType.Trim().ToLowerInvariant();
            query = query.Where(x => x.CardType.ToLower() == normalizedTypeLower);
        }

        if (submittedByUserId.HasValue)
        {
            query = query.Where(x => x.SubmittedByUserId == submittedByUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.FrontText.ToLower().Contains(term)
                || x.BackText.ToLower().Contains(term)
                || (x.Reading != null && x.Reading.ToLower().Contains(term))
                || (x.Meaning != null && x.Meaning.ToLower().Contains(term)));
        }

        return await query
            .OrderByDescending(x => x.ModifiedDate ?? x.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<SrsCardTemplate?> GetDetailedByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _context.SrsCardTemplates
            .Include(x => x.SubmittedByUser)
            .Include(x => x.ReviewedByAdmin)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
}
