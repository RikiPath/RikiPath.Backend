using RikiPath.Application.IServices;
using RikiPath.Domain;
using System.Net;
using System.Net.Mail;

namespace RikiPath.Application.Services
{
    public class EmailService(AppSettings appSettings) : IEmailService
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
            catch (Exception)
            {
                // Không trả chi tiết lỗi SMTP/provider ra API vì có thể lộ thông tin hạ tầng.
                return EmailSendResult.Fail("Không thể gửi email qua nhà cung cấp.");
            }
        }
    }
}
