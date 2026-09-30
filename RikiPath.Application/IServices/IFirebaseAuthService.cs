using RikiPath.Application.Responses.Auth;

namespace RikiPath.Application.IServices
{
    public interface IFirebaseAuthService
    {
        Task<FirebaseVerifiedUserResponse> VerifyPhoneIdTokenAsync(string idToken, CancellationToken ct = default);
    }
}
