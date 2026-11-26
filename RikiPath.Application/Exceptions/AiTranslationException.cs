using System.Net;

namespace RikiPath.Application.Exceptions;

public sealed class AiTranslationException(
    string errorCode,
    HttpStatusCode statusCode,
    string message,
    bool retryable = true,
    Exception? inner = null)
    : AiServiceException(message, inner)
{
    public string ErrorCode { get; } = errorCode;
    public HttpStatusCode StatusCode { get; } = statusCode;
    public bool Retryable { get; } = retryable;
}
