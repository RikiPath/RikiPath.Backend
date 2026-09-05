namespace RikiPath.Application.IServices
{
    public class EmailSendResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }

        public static EmailSendResult Success()
        {
            return new EmailSendResult
            {
                IsSuccess = true
            };
        }

        public static EmailSendResult Fail(string errorMessage)
        {
            return new EmailSendResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }

    public interface IEmailService
    {
        Task<EmailSendResult> SendValidationEmailAsync(string toEmail, string htmlContent, CancellationToken cancellationToken);
    }
}
