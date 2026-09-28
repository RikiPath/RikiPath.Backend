using RikiPath.Domain.Entities;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface INoteRepository : IGenericRepository<Note>
    {
        Task<Note?> GetByRequestIdAsync(int consultationRequestId);
    }
}
