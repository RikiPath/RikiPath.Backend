namespace RikiPath.Application.Exceptions
{
    public class AiQuotaExceededException(string message) : AiServiceException(message);
}
