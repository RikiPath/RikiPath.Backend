using RikiPath.Application.Requests.Auth;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Auth;

namespace RikiPath.Application.IServices
{
    public interface IAuthService
    {
        Task<ApiResponse<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<RegisterResponse>> RegisterWithFirebasePhoneAsync(FirebasePhoneAuthRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> ResendVerificationEmailAsync(ResendVerificationEmailRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> VerifyEmailByLinkAsync(VerifyEmailLinkRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<RegisterResponse>> ChangeUnverifiedEmailAsync(ChangeUnverifiedEmailRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
        ApiResponse<GoogleAuthConfigResponse> GetGoogleAuthConfig();
        Task<ApiResponse<LoginResponse>> ContinueWithGoogleAsync(GoogleAuthRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<LoginResponse>> LoginWithFirebasePhoneAsync(FirebasePhoneAuthRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<LoginResponse>> GetMeAsync(CancellationToken cancellationToken);
        Task<ApiResponse> UpdateEmailAsync(int userId, UpdateEmailRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> ResendPendingEmailAsync(CancellationToken cancellationToken);
        Task<ApiResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> SetPasswordAsync(SetPasswordRequest request, CancellationToken cancellationToken);
    }
}
