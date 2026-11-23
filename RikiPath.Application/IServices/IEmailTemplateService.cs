using RikiPath.Application.EmailTemplates;

namespace RikiPath.Application.IServices;

public interface IEmailTemplateService
{
    RenderedEmailContent BuildAccountVerificationOtp(AccountVerificationOtpEmailData data);
    RenderedEmailContent BuildEmailChangeOtp(EmailChangeOtpEmailData data);
    RenderedEmailContent BuildPasswordResetRequest(PasswordResetRequestEmailData data);
    RenderedEmailContent BuildPasswordSecurityNotice(PasswordSecurityNoticeEmailData data);
    RenderedEmailContent BuildScheduleNotification(ScheduleNotificationEmailData data);
    RenderedEmailContent BuildBookingEnrollment(BookingEnrollmentEmailData data);
    RenderedEmailContent BuildStudyNotification(StudyNotificationEmailData data);
    RenderedEmailContent WrapLegacyHtml(string subject, string htmlFragment);
}
