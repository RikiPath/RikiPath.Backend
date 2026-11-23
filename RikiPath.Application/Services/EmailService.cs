using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Logging;
using RikiPath.Application.EmailTemplates;
using RikiPath.Application.IServices;
using RikiPath.Domain;

namespace RikiPath.Application.Services
{
    public class EmailService(
        AppSettings appSettings,
        ILogger<EmailService>? logger = null,
        IEmailTemplateService? emailTemplates = null) : IEmailService
    {
        private IEmailTemplateService Templates => emailTemplates ?? new EmailTemplateService(appSettings);

        public async Task<EmailSendResult> SendValidationEmailAsync(
            string toEmail, string htmlContent, CancellationToken cancellationToken)
            => await SendEmailAsync(toEmail, "Xác thực tài khoản RikiPath", htmlContent, cancellationToken);

        public async Task<EmailSendResult> SendEmailAsync(
            string toEmail, string subject, string htmlContent, CancellationToken cancellationToken)
            => await SendEmailAsync(toEmail, subject, htmlContent, plainTextContent: null, cancellationToken);

        public Task<EmailSendResult> SendEmailAsync(
            string toEmail, RenderedEmailContent content, CancellationToken cancellationToken)
            => SendEmailAsync(toEmail, content.Subject, content.HtmlBody, content.PlainTextBody, cancellationToken);

        public async Task<EmailSendResult> SendEmailAsync(
            string toEmail,
            string subject,
            string htmlContent,
            string? plainTextContent,
            CancellationToken cancellationToken)
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

                if (string.IsNullOrWhiteSpace(toEmail) || !MailAddress.TryCreate(toEmail.Trim(), out var recipient))
                {
                    logger?.LogWarning("Email delivery skipped: recipient address is invalid.");
                    return EmailSendResult.Fail("Địa chỉ email người nhận không hợp lệ.");
                }

                var resolvedSubject = string.IsNullOrWhiteSpace(subject) ? "Thông báo từ RikiPath" : subject.Trim();
                string finalHtml;
                string finalPlain;

                if (htmlContent.TrimStart().StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase))
                {
                    finalHtml = htmlContent;
                    finalPlain = string.IsNullOrWhiteSpace(plainTextContent)
                        ? EmailTemplateService.HtmlToPlainText(htmlContent)
                        : plainTextContent;
                }
                else
                {
                    var wrapped = Templates.WrapLegacyHtml(resolvedSubject, htmlContent);
                    finalHtml = wrapped.HtmlBody;
                    finalPlain = string.IsNullOrWhiteSpace(plainTextContent)
                        ? wrapped.PlainTextBody
                        : plainTextContent;
                }

                using var client = new SmtpClient(smtp.Host, smtp.Port)
                {
                    Credentials = new NetworkCredential(smtp.Username, smtp.Password),
                    EnableSsl = smtp.EnableSsl,
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(smtp.FromEmail, smtp.FromName, Encoding.UTF8),
                    Subject = resolvedSubject,
                    SubjectEncoding = Encoding.UTF8,
                    BodyEncoding = Encoding.UTF8,
                    HeadersEncoding = Encoding.UTF8,
                };
                message.To.Add(recipient);

                // multipart/alternative: plain text first, HTML second (RFC 2046 compliant for Gmail & Outlook).
                var plainView = AlternateView.CreateAlternateViewFromString(
                    finalPlain, Encoding.UTF8, MediaTypeNames.Text.Plain);
                plainView.TransferEncoding = TransferEncoding.QuotedPrintable;

                var htmlView = AlternateView.CreateAlternateViewFromString(
                    finalHtml, Encoding.UTF8, MediaTypeNames.Text.Html);
                htmlView.TransferEncoding = TransferEncoding.QuotedPrintable;

                if (finalHtml.Contains($"cid:{EmailTemplateService.BrandLogoContentId}", StringComparison.Ordinal))
                {
                    var logoStream = typeof(EmailTemplateService).Assembly.GetManifestResourceStream(
                        "RikiPath.Application.EmailTemplates.brand-logo.png");
                    if (logoStream is null)
                        throw new InvalidOperationException("Embedded email logo is unavailable.");

                    var logo = new LinkedResource(logoStream, MediaTypeNames.Image.Png)
                    {
                        ContentId = EmailTemplateService.BrandLogoContentId,
                        TransferEncoding = TransferEncoding.Base64,
                    };
                    htmlView.LinkedResources.Add(logo);
                }

                message.AlternateViews.Add(plainView);
                message.AlternateViews.Add(htmlView);

                await client.SendMailAsync(message, cancellationToken);

                return EmailSendResult.Success();
            }
            catch (Exception ex)
            {
                // Không trả chi tiết lỗi SMTP/provider ra API và không ghi OTP/nội dung nhạy cảm vào log.
                logger?.LogError("Email delivery failed with {FailureType}.", ex.GetType().Name);
                return EmailSendResult.Fail("Không thể gửi email qua nhà cung cấp.");
            }
        }
    }
}
