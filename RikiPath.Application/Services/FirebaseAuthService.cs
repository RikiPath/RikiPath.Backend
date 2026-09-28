using FirebaseAdmin.Auth;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses.Auth;

namespace RikiPath.Application.Services
{
    public class FirebaseAuthService(ILogger<FirebaseAuthService> logger) : IFirebaseAuthService
    {
        public async Task<FirebaseVerifiedUserResponse> VerifyPhoneIdTokenAsync(string idToken, CancellationToken ct = default)
        {
            try
            {
                // Verify chữ ký + hạn dùng của idToken với Firebase (gọi Google, có cache JWKS nội bộ).
                FirebaseToken decodedToken = await FirebaseAuth.DefaultInstance
                    .VerifyIdTokenAsync(idToken, ct);

                string uid = decodedToken.Uid;

                // Lấy lại UserRecord để có PhoneNumber đã verify — KHÔNG lấy SĐT từ claim
                // do client có thể chèn claim tuỳ ý trong token giả mạo trước khi verify,
                // nên phải đi qua GetUserAsync để chắc chắn dữ liệu đến từ Firebase server.
                UserRecord userRecord = await FirebaseAuth.DefaultInstance.GetUserAsync(uid, ct);

                if (string.IsNullOrWhiteSpace(userRecord.PhoneNumber))
                {
                    throw new InvalidOperationException(
                        "Tài khoản Firebase này không có số điện thoại đã xác thực.");
                }

                return new FirebaseVerifiedUserResponse(userRecord.Uid, userRecord.PhoneNumber);
            }
            catch (FirebaseAuthException ex)
            {
                logger.LogWarning(ex, "Firebase idToken không hợp lệ hoặc đã hết hạn");
                throw;
            }
        }
    }
}