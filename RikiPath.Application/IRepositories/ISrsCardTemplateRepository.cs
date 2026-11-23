using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IRepositories;

public interface ISrsCardTemplateRepository : IGenericRepository<SrsCardTemplate>
{
    Task<List<SrsCardTemplate>> QueryTemplatesAsync(
        ContentStatus? status,
        string? jlptLevel,
        string? cardType,
        int? submittedByUserId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<SrsCardTemplate?> GetDetailedByIdAsync(int id, CancellationToken cancellationToken = default);
}
