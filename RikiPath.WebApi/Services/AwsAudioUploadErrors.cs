using System.Net;
using Amazon.Runtime;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Services;

public static class AwsAudioUploadErrors
{
    public static ApiResponse<T> Create<T>(AmazonServiceException error, ILogger? logger = null)
    {
        // Log provider identifiers only; AWS messages can contain account details or request data.
        logger?.LogWarning("AWS audio upload failed. ErrorCode={ErrorCode} Status={Status} RequestId={RequestId}",
            error.ErrorCode, (int)error.StatusCode, error.RequestId);

        var problem = error.ErrorCode switch
        {
            "InvalidAccessKeyId" or "InvalidClientTokenId" or "SignatureDoesNotMatch" or
                "ExpiredToken" or "InvalidToken" =>
                ("AUDIO_STORAGE_CREDENTIALS_INVALID", "Cấu hình xác thực kho audio chưa hợp lệ. Vui lòng liên hệ quản trị viên."),
            "AccessDenied" or "AccessDeniedException" or "AllAccessDisabled" =>
                ("AUDIO_STORAGE_ACCESS_DENIED", "Hệ thống chưa được cấp quyền tải audio lên kho lưu trữ. Vui lòng liên hệ quản trị viên."),
            "NoSuchBucket" =>
                ("AUDIO_STORAGE_BUCKET_NOT_FOUND", "Chưa tìm thấy kho audio đã cấu hình. Vui lòng liên hệ quản trị viên."),
            "PermanentRedirect" or "AuthorizationHeaderMalformed" or "IncorrectEndpoint" =>
                ("AUDIO_STORAGE_REGION_MISMATCH", "Cấu hình vùng của kho audio chưa đúng. Vui lòng liên hệ quản trị viên."),
            _ when error.StatusCode == HttpStatusCode.Forbidden =>
                ("AUDIO_STORAGE_ACCESS_DENIED", "Hệ thống chưa được cấp quyền tải audio lên kho lưu trữ. Vui lòng liên hệ quản trị viên."),
            _ => (string.Empty, string.Empty)
        };

        if (problem.Item1.Length != 0)
            return ApiResponse<T>.Fail(problem.Item2, HttpStatusCode.ServiceUnavailable, problem.Item1, retryable: false);

        var retryable = (int)error.StatusCode >= 500 || (int)error.StatusCode is 0 or 429 ||
            error.ErrorCode is "SlowDown" or "RequestTimeout";
        return ApiResponse<T>.Fail("Chưa tải được audio lên kho lưu trữ. Vui lòng thử lại.",
            HttpStatusCode.BadGateway, "AUDIO_STORAGE_UNAVAILABLE", retryable);
    }
}
