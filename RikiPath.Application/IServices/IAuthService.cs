using RikiPath.Application.Requests.Auth;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Auth;

namespace RikiPath.Application.IServices
{
    public interface IAuthService
    {
        Task<ApiResponse<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> UpdateEmailAsync(int userId, UpdateEmailRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken);
    }
}
