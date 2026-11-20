using RikiPath.Application.IServices;
using RikiPath.Domain;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace RikiPath.Application.Services
{
    public class EmailService(AppSettings appSettings, ILogger<EmailService>? logger = null) : IEmailService
    {
        public async Task<EmailSendResult> SendValidationEmailAsync(
            string toEmail, string htmlContent, CancellationToken cancellationToken)
            => await SendEmailAsync(toEmail, "Xác thực tài khoản RikiPath", htmlContent, cancellationToken);

        public async Task<EmailSendResult> SendEmailAsync(
            string toEmail, string subject, string htmlContent, CancellationToken cancellationToken)
        {
            try
            {
                var smtp = appSettings.SmtpSettings;
                if (smtp is null || string.IsNullOrWhiteSpace(smtp.Host) || smtp.Port is < 1 or > 65535 ||
                    string.IsNullOrWhiteSpace(smtp.FromEmail))
                {
                    logger?.LogWarning("Email delivery unavailable: SMTP configuration is incomplete.");
                    return EmailSendResult.Fail("Không thể gửi email qua nhà cung cấp.");
                }

                using var client = new SmtpClient(smtp.Host, smtp.Port)
                {
                    Credentials = new NetworkCredential(smtp.Username, smtp.Password),
                    EnableSsl = smtp.EnableSsl,
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(smtp.FromEmail, smtp.FromName),
                    Subject = subject,
                    Body = htmlContent,
                    IsBodyHtml = true,
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message, cancellationToken);

                return EmailSendResult.Success();
            }
            catch (Exception ex)
            {
                // Không trả chi tiết lỗi SMTP/provider ra API vì có thể lộ thông tin hạ tầng.
                logger?.LogError("Email delivery failed with {FailureType}.", ex.GetType().Name);
                return EmailSendResult.Fail("Không thể gửi email qua nhà cung cấp.");
            }
        }
    }
}
