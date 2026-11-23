namespace RikiPath.Application.EmailTemplates;

public enum SakuraEmailTone
{
    Sakura,
    Info,
    Success,
    Warning,
    Danger,
}

public sealed record EmailDetailItem(string Label, string Value);

public sealed record RenderedEmailContent(
    string Subject,
    string HtmlBody,
    string PlainTextBody);

public sealed record AccountVerificationOtpEmailData(
    string? RecipientName,
    string EmailAddress,
    string OtpCode,
    int LifetimeMinutes,
    bool IsResend = false,
    string? VerificationUrl = null);

public sealed record EmailChangeOtpEmailData(
    string? RecipientName,
    string NewEmailAddress,
    string OtpCode,
    int LifetimeMinutes,
    bool InitiatedByAdmin = false,
    string? VerificationUrl = null);

public sealed record PasswordResetRequestEmailData(
    string? RecipientName,
    string ResetUrl,
    int LifetimeMinutes);

public sealed record PasswordSecurityNoticeEmailData(
    string? RecipientName,
    string EmailAddress,
    DateTime OccurredAtUtc,
    string ActionTitle,
    string ActionDescription,
    string? LoginRelativeOrAbsoluteUrl = "/login");

public sealed record ScheduleNotificationEmailData(
    string? RecipientName,
    string Subject,
    string BadgeText,
    string Headline,
    string Summary,
    IReadOnlyList<EmailDetailItem>? Details = null,
    SakuraEmailTone Tone = SakuraEmailTone.Sakura,
    string? CalloutTitle = null,
    string? CalloutMessage = null,
    string? ActionText = null,
    string? ActionUrl = null);

public sealed record BookingEnrollmentEmailData(
    string? RecipientName,
    string Subject,
    string BadgeText,
    string Headline,
    string Summary,
    string CourseOrCohortName,
    IReadOnlyList<EmailDetailItem>? Details = null,
    SakuraEmailTone Tone = SakuraEmailTone.Success,
    string? CalloutTitle = null,
    string? CalloutMessage = null,
    string? ActionText = null,
    string? ActionUrl = null);

public sealed record StudyNotificationEmailData(
    string? RecipientName,
    string Subject,
    string BadgeText,
    string Headline,
    string Summary,
    string? HighlightLabel = null,
    string? HighlightValue = null,
    IReadOnlyList<EmailDetailItem>? Details = null,
    SakuraEmailTone Tone = SakuraEmailTone.Sakura,
    string? CalloutTitle = null,
    string? CalloutMessage = null,
    string? ActionText = null,
    string? ActionUrl = null);
