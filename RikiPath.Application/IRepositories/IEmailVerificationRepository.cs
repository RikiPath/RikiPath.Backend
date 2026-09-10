using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface IEmailVerificationRepository : IGenericRepository<EmailVerification>
    {
        Task<EmailVerification?> GetValidCodeAsync(int userId, string verificationCode);
    }
}
