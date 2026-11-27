using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using RikiPath.Application.EmailTemplates;
using RikiPath.Application.IServices;
using RikiPath.Domain;

namespace RikiPath.Application.Services;

public sealed partial class EmailTemplateService(AppSettings? appSettings = null) : IEmailTemplateService
{
    private const string FontFamily = "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Noto Sans', 'Noto Sans JP', 'Hiragino Sans', 'Yu Gothic', Arial, sans-serif";
    private const string MonoFontFamily = "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace";
    internal const string BrandLogoContentId = "rikipath-brand-logo";

    private static readonly Lazy<TimeZoneInfo> VietnamZone = new(ResolveVietnamTimeZone);

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTagRegex();

    [GeneratedRegex(@"</(p|div|h1|h2|h3|h4|li|tr)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndTagRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiNewlineRegex();

    public RenderedEmailContent BuildAccountVerificationOtp(AccountVerificationOtpEmailData data)
    {
        var name = NormalizeName(data.RecipientName);
        var safeName = Encode(name);
        var safeOtp = Encode(data.OtpCode);
        var minutes = Math.Clamp(data.LifetimeMinutes, 1, 1440);
        var rawVerifyUrl = ResolveRawSafeUrl(data.VerificationUrl);
        var safeVerifyUrl = rawVerifyUrl is null ? null : Encode(rawVerifyUrl);

        var subject = data.IsResend
            ? "Mã xác thực mới cho tài khoản RikiPath"
            : "Xác thực tài khoản RikiPath";
        var preheader = $"Mã xác thực RikiPath của bạn là {data.OtpCode} (hiệu lực {minutes} phút).";
        const string headline = "Xác thực email của bạn";

        var bodyHtml = new StringBuilder();
        bodyHtml.Append(RenderParagraph($"Xin chào <strong>{safeName}</strong>, sử dụng mã dưới đây để hoàn tất xác thực tài khoản RikiPath."));
        bodyHtml.Append(RenderMinimalOtp(safeOtp, minutes));

        if (!string.IsNullOrEmpty(safeVerifyUrl))
            bodyHtml.Append(RenderPrimaryButton("Xác thực email", safeVerifyUrl, compactMargin: true));

        bodyHtml.Append(RenderMutedNote("Nếu bạn không yêu cầu mã này, hãy bỏ qua email. Đừng chia sẻ mã với bất kỳ ai."));

        var plain = new List<string>
        {
            $"Xin chào {name}, sử dụng mã dưới đây để hoàn tất xác thực tài khoản RikiPath.",
            string.Empty,
            $"MÃ XÁC THỰC: {data.OtpCode}",
            $"Mã có hiệu lực trong {minutes} phút.",
        };
        if (!string.IsNullOrEmpty(rawVerifyUrl))
        {
            plain.Add(string.Empty);
            plain.Add($"Xác thực email nhanh tại: {rawVerifyUrl}");
        }
        plain.Add(string.Empty);
        plain.Add("Nếu bạn không yêu cầu mã này, hãy bỏ qua email. Đừng chia sẻ mã với bất kỳ ai.");

        return RenderSakuraDocument(
            subject,
            preheader,
            string.Empty,
            SakuraEmailTone.Sakura,
            headline,
            bodyHtml.ToString(),
            plain,
            minimal: true);
    }

    public RenderedEmailContent BuildEmailChangeOtp(EmailChangeOtpEmailData data)
    {
        var name = NormalizeName(data.RecipientName);
        var safeName = Encode(name);
        var safeOtp = Encode(data.OtpCode);
        var minutes = Math.Clamp(data.LifetimeMinutes, 1, 1440);
        var rawVerifyUrl = ResolveRawSafeUrl(data.VerificationUrl);
        var safeVerifyUrl = rawVerifyUrl is null ? null : Encode(rawVerifyUrl);

        const string subject = "Xác nhận thay đổi email RikiPath";
        var preheader = $"Mã xác nhận email mới RikiPath: {data.OtpCode} (hiệu lực {minutes} phút).";
        const string headline = "Xác nhận email mới của bạn";
        var lead = data.InitiatedByAdmin
            ? $"Xin chào <strong>{safeName}</strong>, quản trị viên vừa cập nhật email tài khoản RikiPath của bạn. Sử dụng mã dưới đây để xác nhận hộp thư này."
            : $"Xin chào <strong>{safeName}</strong>, sử dụng mã dưới đây để xác nhận địa chỉ email mới cho tài khoản RikiPath.";

        var bodyHtml = new StringBuilder();
        bodyHtml.Append(RenderParagraph(lead));
        bodyHtml.Append(RenderMinimalOtp(safeOtp, minutes));

        if (!string.IsNullOrEmpty(safeVerifyUrl))
            bodyHtml.Append(RenderPrimaryButton("Xác thực email", safeVerifyUrl, compactMargin: true));

        bodyHtml.Append(RenderMutedNote("Nếu bạn không yêu cầu thay đổi email này, hãy bỏ qua email và kiểm tra bảo mật tài khoản. Đừng chia sẻ mã với bất kỳ ai."));

        var plain = new List<string>
        {
            data.InitiatedByAdmin
                ? $"Xin chào {name}, quản trị viên vừa cập nhật email tài khoản RikiPath của bạn. Sử dụng mã dưới đây để xác nhận hộp thư này."
                : $"Xin chào {name}, sử dụng mã dưới đây để xác nhận địa chỉ email mới cho tài khoản RikiPath.",
            string.Empty,
            $"MÃ XÁC THỰC: {data.OtpCode}",
            $"Mã có hiệu lực trong {minutes} phút.",
        };
        if (!string.IsNullOrEmpty(rawVerifyUrl))
        {
            plain.Add(string.Empty);
            plain.Add($"Xác thực email nhanh tại: {rawVerifyUrl}");
        }
        plain.Add(string.Empty);
        plain.Add("Nếu bạn không yêu cầu thay đổi email này, hãy bỏ qua email và kiểm tra bảo mật tài khoản. Đừng chia sẻ mã với bất kỳ ai.");

        return RenderSakuraDocument(
            subject,
            preheader,
            string.Empty,
            SakuraEmailTone.Sakura,
            headline,
            bodyHtml.ToString(),
            plain,
            minimal: true);
    }

    public RenderedEmailContent BuildPasswordResetRequest(PasswordResetRequestEmailData data)
    {
        var name = NormalizeName(data.RecipientName);
        var safeName = Encode(name);
        var safeUrl = ResolveSafeUrl(data.ResetUrl) ?? string.Empty;
        var minutes = Math.Clamp(data.LifetimeMinutes, 1, 1440);

        const string subject = "Đặt lại mật khẩu RikiPath";
        var preheader = $"Liên kết đặt lại mật khẩu RikiPath của bạn có hiệu lực trong {minutes} phút.";

        var bodyHtml = new StringBuilder();
        bodyHtml.Append(RenderParagraph($"Xin chào <strong>{safeName}</strong>,"));
        bodyHtml.Append(RenderParagraph(
            $"Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản RikiPath của bạn. Nhấn vào nút bên dưới để tạo mật khẩu mới. Liên kết này chỉ sử dụng được một lần và có hiệu lực trong <strong>{minutes} phút</strong>."));

        if (!string.IsNullOrEmpty(safeUrl))
        {
            bodyHtml.Append(RenderPrimaryButton("Đặt lại mật khẩu", safeUrl));
            bodyHtml.Append(RenderCopyableLinkBox("Hoặc sao chép liên kết sau vào trình duyệt:", safeUrl));
        }

        bodyHtml.Append(RenderCallout(
            SakuraEmailTone.Warning,
            "Bạn không gửi yêu cầu này?",
            "Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này. Mật khẩu hiện tại của bạn vẫn an toàn và không bị thay đổi."));

        var plain = new List<string>
        {
            $"Xin chào {name},",
            string.Empty,
            $"Chúng tôi nhận được yêu cầu đặt lại mật khẩu tài khoản RikiPath (hiệu lực {minutes} phút).",
            string.Empty,
            $"Mở liên kết sau để đặt lại mật khẩu: {data.ResetUrl}",
            string.Empty,
            "Nếu bạn không yêu cầu thao tác này, hãy bỏ qua email.",
        };

        return RenderSakuraDocument(
            subject,
            preheader,
            "Khôi phục mật khẩu",
            SakuraEmailTone.Sakura,
            "Đặt lại mật khẩu RikiPath",
            bodyHtml.ToString(),
            plain);
    }

    public RenderedEmailContent BuildPasswordSecurityNotice(PasswordSecurityNoticeEmailData data)
    {
        var name = NormalizeName(data.RecipientName);
        var safeName = Encode(name);
        var formattedTime = FormatVietnamDateTime(data.OccurredAtUtc);
        var rawActionUrl = ResolveRawSafeUrl(data.LoginRelativeOrAbsoluteUrl);
        var safeActionUrl = rawActionUrl is null ? null : Encode(rawActionUrl);

        var subject = $"{data.ActionTitle} thành công · RikiPath";
        var preheader = $"Thông báo bảo mật: Tài khoản {data.EmailAddress} vừa hoàn tất thao tác {data.ActionTitle.ToLowerInvariant()}.";

        var bodyHtml = new StringBuilder();
        bodyHtml.Append(RenderParagraph($"Xin chào <strong>{safeName}</strong>,"));
        bodyHtml.Append(RenderParagraph(Encode(data.ActionDescription)));
        bodyHtml.Append(RenderDetailRows([
            new("Tài khoản", data.EmailAddress),
            new("Thao tác", data.ActionTitle),
            new("Thời gian", formattedTime),
            new("Phiên đăng nhập", "Các phiên đăng nhập cũ đã được tự động đăng xuất để bảo vệ tài khoản"),
        ]));

        if (!string.IsNullOrEmpty(safeActionUrl))
            bodyHtml.Append(RenderPrimaryButton("Đăng nhập RikiPath", safeActionUrl));

        bodyHtml.Append(RenderCallout(
            SakuraEmailTone.Danger,
            "Nếu không phải bạn thực hiện",
            "Hãy sử dụng chức năng Quên mật khẩu trên trang đăng nhập RikiPath ngay lập tức và liên hệ đội ngũ hỗ trợ để được bảo vệ tài khoản."));

        var plain = new List<string>
        {
            $"Xin chào {name},",
            string.Empty,
            data.ActionDescription,
            $"- Tài khoản: {data.EmailAddress}",
            $"- Thao tác: {data.ActionTitle}",
            $"- Thời gian: {formattedTime}",
            "- Phiên đăng nhập cũ đã được tự động thu hồi.",
        };
        if (!string.IsNullOrEmpty(rawActionUrl))
        {
            plain.Add(string.Empty);
            plain.Add($"Đăng nhập RikiPath: {rawActionUrl}");
        }
        plain.Add(string.Empty);
        plain.Add("Nếu không phải bạn thực hiện, hãy khôi phục mật khẩu và liên hệ hỗ trợ ngay.");

        return RenderSakuraDocument(
            subject,
            preheader,
            "Cảnh báo bảo mật",
            SakuraEmailTone.Success,
            $"{data.ActionTitle} thành công",
            bodyHtml.ToString(),
            plain);
    }

    public RenderedEmailContent BuildScheduleNotification(ScheduleNotificationEmailData data)
    {
        var name = NormalizeName(data.RecipientName);
        var safeName = Encode(name);
        var rawActionUrl = ResolveRawSafeUrl(data.ActionUrl);
        var safeActionUrl = rawActionUrl is null ? null : Encode(rawActionUrl);

        var bodyHtml = new StringBuilder();
        bodyHtml.Append(RenderParagraph($"Xin chào <strong>{safeName}</strong>,"));
        bodyHtml.Append(RenderParagraph(Encode(data.Summary)));

        if (data.Details is { Count: > 0 })
            bodyHtml.Append(RenderDetailRows(data.Details));

        if (!string.IsNullOrWhiteSpace(data.CalloutMessage))
            bodyHtml.Append(RenderCallout(data.Tone, data.CalloutTitle ?? "Ghi chú lịch học", data.CalloutMessage));

        if (!string.IsNullOrEmpty(safeActionUrl))
            bodyHtml.Append(RenderPrimaryButton(data.ActionText ?? "Xem lịch trên RikiPath", safeActionUrl));

        var plain = new List<string>
        {
            $"Xin chào {name},",
            string.Empty,
            data.Summary,
        };
        AppendPlainDetails(plain, data.Details);
        if (!string.IsNullOrWhiteSpace(data.CalloutMessage))
        {
            plain.Add(string.Empty);
            plain.Add($"{data.CalloutTitle ?? "Ghi chú"}: {data.CalloutMessage}");
        }
        if (!string.IsNullOrEmpty(rawActionUrl))
        {
            plain.Add(string.Empty);
            plain.Add($"{data.ActionText ?? "Xem lịch trên RikiPath"}: {rawActionUrl}");
        }

        return RenderSakuraDocument(
            data.Subject,
            data.Summary,
            data.BadgeText,
            data.Tone,
            data.Headline,
            bodyHtml.ToString(),
            plain);
    }

    public RenderedEmailContent BuildBookingEnrollment(BookingEnrollmentEmailData data)
    {
        var name = NormalizeName(data.RecipientName);
        var safeName = Encode(name);
        var rawActionUrl = ResolveRawSafeUrl(data.ActionUrl);
        var safeActionUrl = rawActionUrl is null ? null : Encode(rawActionUrl);

        var bodyHtml = new StringBuilder();
        bodyHtml.Append(RenderParagraph($"Xin chào <strong>{safeName}</strong>,"));
        bodyHtml.Append(RenderParagraph(Encode(data.Summary)));
        bodyHtml.Append(RenderHighlightBanner("Khóa học / Lịch hẹn", data.CourseOrCohortName, data.Tone));

        if (data.Details is { Count: > 0 })
            bodyHtml.Append(RenderDetailRows(data.Details));

        if (!string.IsNullOrWhiteSpace(data.CalloutMessage))
            bodyHtml.Append(RenderCallout(data.Tone, data.CalloutTitle ?? "Thông tin tham gia", data.CalloutMessage));

        if (!string.IsNullOrEmpty(safeActionUrl))
            bodyHtml.Append(RenderPrimaryButton(data.ActionText ?? "Xem chi tiết trên RikiPath", safeActionUrl));

        var plain = new List<string>
        {
            $"Xin chào {name},",
            string.Empty,
            data.Summary,
            $"Khóa học / Lịch hẹn: {data.CourseOrCohortName}",
        };
        AppendPlainDetails(plain, data.Details);
        if (!string.IsNullOrWhiteSpace(data.CalloutMessage))
        {
            plain.Add(string.Empty);
            plain.Add($"{data.CalloutTitle ?? "Thông tin"}: {data.CalloutMessage}");
        }
        if (!string.IsNullOrEmpty(rawActionUrl))
        {
            plain.Add(string.Empty);
            plain.Add($"{data.ActionText ?? "Xem chi tiết"}: {rawActionUrl}");
        }

        return RenderSakuraDocument(
            data.Subject,
            data.Summary,
            data.BadgeText,
            data.Tone,
            data.Headline,
            bodyHtml.ToString(),
            plain);
    }

    public RenderedEmailContent BuildStudyNotification(StudyNotificationEmailData data)
    {
        var name = NormalizeName(data.RecipientName);
        var safeName = Encode(name);
        var rawActionUrl = ResolveRawSafeUrl(data.ActionUrl);
        var safeActionUrl = rawActionUrl is null ? null : Encode(rawActionUrl);

        var bodyHtml = new StringBuilder();
        bodyHtml.Append(RenderParagraph($"Xin chào <strong>{safeName}</strong>,"));
        bodyHtml.Append(RenderParagraph(Encode(data.Summary)));

        if (!string.IsNullOrWhiteSpace(data.HighlightValue))
            bodyHtml.Append(RenderHighlightBanner(data.HighlightLabel ?? "Cập nhật học tập", data.HighlightValue, data.Tone));

        if (data.Details is { Count: > 0 })
            bodyHtml.Append(RenderDetailRows(data.Details));

        if (!string.IsNullOrWhiteSpace(data.CalloutMessage))
            bodyHtml.Append(RenderCallout(data.Tone, data.CalloutTitle ?? "Nhận xét & Lưu ý", data.CalloutMessage));

        if (!string.IsNullOrEmpty(safeActionUrl))
            bodyHtml.Append(RenderPrimaryButton(data.ActionText ?? "Mở RikiPath", safeActionUrl));

        var plain = new List<string>
        {
            $"Xin chào {name},",
            string.Empty,
            data.Summary,
        };
        if (!string.IsNullOrWhiteSpace(data.HighlightValue))
            plain.Add($"{data.HighlightLabel ?? "Điểm nhấn"}: {data.HighlightValue}");
        AppendPlainDetails(plain, data.Details);
        if (!string.IsNullOrWhiteSpace(data.CalloutMessage))
        {
            plain.Add(string.Empty);
            plain.Add($"{data.CalloutTitle ?? "Ghi chú"}: {data.CalloutMessage}");
        }
        if (!string.IsNullOrEmpty(rawActionUrl))
        {
            plain.Add(string.Empty);
            plain.Add($"{data.ActionText ?? "Mở RikiPath"}: {rawActionUrl}");
        }

        return RenderSakuraDocument(
            data.Subject,
            data.Summary,
            data.BadgeText,
            data.Tone,
            data.Headline,
            bodyHtml.ToString(),
            plain);
    }

    public RenderedEmailContent WrapLegacyHtml(string subject, string htmlFragment)
    {
        var safeSubject = string.IsNullOrWhiteSpace(subject) ? "Thông báo từ RikiPath" : subject.Trim();
        var plainBody = HtmlToPlainText(htmlFragment);

        if (htmlFragment.TrimStart().StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase))
            return new RenderedEmailContent(safeSubject, htmlFragment, plainBody);

        var wrappedContent = $"<div style=\"font-family:{FontFamily};font-size:15px;line-height:1.65;color:#5F5559;\">{htmlFragment}</div>";
        return RenderSakuraDocument(
            safeSubject,
            plainBody,
            "Thông báo hệ thống",
            SakuraEmailTone.Sakura,
            safeSubject,
            wrappedContent,
            [plainBody]);
    }

    public static string FormatVietnamDateTime(DateTime utcOrUnspecified)
    {
        var utc = utcOrUnspecified.Kind == DateTimeKind.Utc
            ? utcOrUnspecified
            : DateTime.SpecifyKind(utcOrUnspecified, DateTimeKind.Utc);
        var vn = TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamZone.Value);
        return $"{vn:HH:mm} · {vn:dd/MM/yyyy} (GMT+7)";
    }

    public static string FormatVietnamSlotRange(DateTime startUtc, DateTime endUtc)
    {
        var sUtc = startUtc.Kind == DateTimeKind.Utc ? startUtc : DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        var eUtc = endUtc.Kind == DateTimeKind.Utc ? endUtc : DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
        var sVn = TimeZoneInfo.ConvertTimeFromUtc(sUtc, VietnamZone.Value);
        var eVn = TimeZoneInfo.ConvertTimeFromUtc(eUtc, VietnamZone.Value);
        return sVn.Date == eVn.Date
            ? $"{sVn:HH:mm} – {eVn:HH:mm} · {sVn:dd/MM/yyyy} (GMT+7)"
            : $"{sVn:HH:mm dd/MM/yyyy} – {eVn:HH:mm dd/MM/yyyy} (GMT+7)";
    }

    public static string HtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = BreakTagRegex().Replace(html, "\n");
        text = BlockEndTagRegex().Replace(text, "\n");
        text = HtmlTagRegex().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        text = MultiSpaceRegex().Replace(text, " ");
        text = MultiNewlineRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    private RenderedEmailContent RenderSakuraDocument(
        string subject,
        string preheader,
        string badgeText,
        SakuraEmailTone badgeTone,
        string headline,
        string innerBodyHtml,
        IEnumerable<string> plainLines,
        bool minimal = false)
    {
        var (badgeBg, badgeBorder, badgeInk) = GetToneColors(badgeTone);
        var supportEmail = ResolveSupportEmail();
        var webUrl = ResolveFrontendBaseUrl();
        var safeLogoUrl = $"cid:{BrandLogoContentId}";
        var safeSubject = Encode(subject);
        var safePreheader = Encode(preheader);
        var safeBadge = Encode(badgeText);
        var safeHeadline = Encode(headline);
        var safeSupportEmail = Encode(supportEmail);
        var year = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture);

        var outerPad = minimal ? "22px 14px" : "28px 16px";
        var headerPad = minimal ? "18px 28px 14px 28px" : "22px 28px 18px 28px";
        var cardPad = minimal ? "24px 28px 22px 28px" : "28px 28px 24px 28px";
        var footerPad = minimal ? "16px 28px 18px 28px" : "20px 28px 24px 28px";

        var html = new StringBuilder(4096);
        html.Append("<!DOCTYPE html>");
        html.Append("<html lang=\"vi\" xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\">");
        html.Append("<head>");
        html.Append("<meta charset=\"UTF-8\" />");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        html.Append("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />");
        html.Append("<meta name=\"x-apple-disable-message-reformatting\" />");
        html.Append("<meta name=\"color-scheme\" content=\"light dark\" />");
        html.Append("<meta name=\"supported-color-schemes\" content=\"light dark\" />");
        html.Append(CultureInfo.InvariantCulture, $"<title>{safeSubject}</title>");
        html.Append("<!--[if gte mso 9]><xml><o:OfficeDocumentSettings><o:AllowPNG/><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml><![endif]-->");
        html.Append("<style>");
        html.Append(@"
            :root { color-scheme: light dark; supported-color-schemes: light dark; }
            body, table, td, a { -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }
            table, td { mso-table-lspace: 0pt; mso-table-rspace: 0pt; }
            img { -ms-interpolation-mode: bicubic; border: 0; height: auto; line-height: 100%; outline: none; text-decoration: none; }
            body { margin: 0 !important; padding: 0 !important; width: 100% !important; background-color: #FAF8F6; }
            @media only screen and (max-width: 620px) {
                .rp-outer-pad { padding: 14px 10px !important; }
                .rp-card-pad { padding: 20px 18px !important; }
                .rp-header-pad { padding: 16px 18px 14px 18px !important; }
                .rp-footer-pad { padding: 16px 18px 18px 18px !important; }
                .rp-h1 { font-size: 20px !important; line-height: 1.3 !important; }
                .rp-otp-code { font-size: 30px !important; letter-spacing: 8px !important; padding-left: 8px !important; }
                .rp-badge-cell { display: block !important; text-align: left !important; padding-top: 10px !important; }
            }
            @media (prefers-color-scheme: dark) {
                body, .rp-body-bg { background-color: #1A1618 !important; }
                .rp-card { background-color: #241F21 !important; border-color: #3D3236 !important; }
                .rp-header-border, .rp-footer-border, .rp-detail-row { border-color: #3D3236 !important; }
                .rp-h1, .rp-text-strong, .rp-detail-val { color: #F5EFF1 !important; }
                .rp-text-body, .rp-detail-lbl { color: #CFC4C8 !important; }
                .rp-text-muted { color: #A6989D !important; }
                .rp-sakura-box { background-color: #2F2127 !important; border-color: #5C3845 !important; }
                .rp-logo-wrap { background-color: #FFF4F7 !important; border-radius: 8px !important; padding: 2px 4px !important; }
                .rp-otp-code, .rp-brand-title { color: #F48FA7 !important; }
                .rp-neutral-box { background-color: #2B2527 !important; border-color: #3D3236 !important; }
                .rp-link-accent { color: #F48FA7 !important; }
            }
            [data-ogsc] .rp-body-bg { background-color: #1A1618 !important; }
            [data-ogsc] .rp-card { background-color: #241F21 !important; border-color: #3D3236 !important; }
            [data-ogsc] .rp-h1, [data-ogsc] .rp-text-strong, [data-ogsc] .rp-detail-val { color: #F5EFF1 !important; }
            [data-ogsc] .rp-text-body, [data-ogsc] .rp-detail-lbl { color: #CFC4C8 !important; }
            [data-ogsc] .rp-text-muted { color: #A6989D !important; }
            [data-ogsc] .rp-sakura-box { background-color: #2F2127 !important; border-color: #5C3845 !important; }
            [data-ogsc] .rp-logo-wrap { background-color: #FFF4F7 !important; border-radius: 8px !important; padding: 2px 4px !important; }
            [data-ogsc] .rp-otp-code, [data-ogsc] .rp-brand-title { color: #F48FA7 !important; }
            [data-ogsc] .rp-neutral-box { background-color: #2B2527 !important; border-color: #3D3236 !important; }
        ");
        html.Append("</style>");
        html.Append("</head>");
        html.Append("<body class=\"rp-body-bg\" style=\"margin:0;padding:0;background-color:#FAF8F6;\">");

        // Preheader
        html.Append(CultureInfo.InvariantCulture, $"<div style=\"display:none;font-size:1px;color:#FAF8F6;line-height:1px;max-height:0px;max-width:0px;opacity:0;overflow:hidden;mso-hide:all;\">{safePreheader}</div>");

        // Outer canvas
        html.Append(CultureInfo.InvariantCulture, $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"rp-body-bg\" style=\"width:100%;background-color:#FAF8F6;font-family:{FontFamily};\">");
        html.Append("<tr>");
        html.Append(CultureInfo.InvariantCulture, $"<td align=\"center\" class=\"rp-outer-pad\" style=\"padding:{outerPad};\">");

        html.Append("<!--[if mso]><table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" align=\"center\"><tr><td><![endif]-->");
        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"rp-card\" style=\"width:100%;max-width:600px;background-color:#FFFFFF;border:1px solid #EAE3E4;border-radius:16px;overflow:hidden;\">");

        // Subtle Sakura top bar
        html.Append("<tr><td style=\"height:3px;background-color:#E05A7A;font-size:0;line-height:0;\">&nbsp;</td></tr>");

        // Brand header
        html.Append("<tr>");
        html.Append(CultureInfo.InvariantCulture, $"<td class=\"rp-header-pad rp-header-border\" style=\"padding:{headerPad};border-bottom:1px solid #EAE3E4;\">");
        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");
        html.Append("<tr>");
        html.Append("<td valign=\"middle\" style=\"text-align:left;\">");
        html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");
        html.Append("<td valign=\"middle\" class=\"rp-logo-wrap\" style=\"vertical-align:middle;line-height:1;\">");
        html.Append(CultureInfo.InvariantCulture, $"<img src=\"{safeLogoUrl}\" width=\"42\" height=\"30\" alt=\"RikiPath\" style=\"display:block;width:42px;height:30px;border:0;outline:none;text-decoration:none;font-family:{FontFamily};font-size:13px;font-weight:700;color:#B53D5C;\" />");
        html.Append("</td>");
        html.Append("<td style=\"padding-left:10px;vertical-align:middle;\">");
        html.Append(CultureInfo.InvariantCulture, $"<div class=\"rp-brand-title\" style=\"font-family:{FontFamily};font-size:19px;font-weight:700;letter-spacing:-0.02em;line-height:1.2;color:#B53D5C;\">RikiPath</div>");
        if (!minimal)
            html.Append(CultureInfo.InvariantCulture, $"<div class=\"rp-text-muted\" style=\"font-family:{FontFamily};font-size:11px;font-weight:500;line-height:1.3;color:#776C70;\">Nền tảng học tiếng Nhật</div>");
        html.Append("</td>");
        html.Append("</tr></table>");
        html.Append("</td>");
        if (!minimal && !string.IsNullOrWhiteSpace(badgeText))
        {
            html.Append("<td valign=\"middle\" align=\"right\" class=\"rp-badge-cell\" style=\"text-align:right;\">");
            html.Append(CultureInfo.InvariantCulture, $"<span style=\"display:inline-block;padding:5px 12px;background-color:{badgeBg};border:1px solid {badgeBorder};border-radius:999px;font-family:{FontFamily};font-size:11px;font-weight:600;line-height:1.2;color:{badgeInk};white-space:nowrap;\">{safeBadge}</span>");
            html.Append("</td>");
        }
        html.Append("</tr>");
        html.Append("</table>");
        html.Append("</td>");
        html.Append("</tr>");

        // Main content body
        html.Append("<tr>");
        html.Append(CultureInfo.InvariantCulture, $"<td class=\"rp-card-pad\" style=\"padding:{cardPad};\">");
        html.Append(CultureInfo.InvariantCulture, $"<h1 class=\"rp-h1\" style=\"margin:0 0 14px 0;font-family:{FontFamily};font-size:21px;font-weight:700;line-height:1.3;letter-spacing:-0.015em;color:#2D282A;\">{safeHeadline}</h1>");
        html.Append(innerBodyHtml);
        html.Append("</td>");
        html.Append("</tr>");

        // Footer
        html.Append("<tr>");
        html.Append(CultureInfo.InvariantCulture, $"<td class=\"rp-footer-pad rp-footer-border rp-neutral-box\" style=\"padding:{footerPad};background-color:#FAF7F8;border-top:1px solid #EAE3E4;\">");
        if (minimal)
        {
            html.Append(CultureInfo.InvariantCulture, $"<p class=\"rp-text-muted\" style=\"margin:0 0 4px 0;font-family:{FontFamily};font-size:12px;line-height:1.5;color:#776C70;\">RikiPath · Hỗ trợ: <a href=\"mailto:{safeSupportEmail}\" class=\"rp-link-accent\" style=\"color:#B53D5C;text-decoration:underline;font-weight:600;\">{safeSupportEmail}</a>");
            if (!string.IsNullOrEmpty(webUrl))
            {
                var safeWebUrl = Encode(webUrl);
                html.Append(CultureInfo.InvariantCulture, $" · <a href=\"{safeWebUrl}\" target=\"_blank\" rel=\"noopener noreferrer\" class=\"rp-link-accent\" style=\"color:#B53D5C;text-decoration:underline;font-weight:600;\">rikipath.web</a>");
            }
            html.Append("</p>");
            html.Append(CultureInfo.InvariantCulture, $"<p class=\"rp-text-muted\" style=\"margin:0;font-family:{FontFamily};font-size:11px;line-height:1.5;color:#776C70;\">&copy; {year} RikiPath. Email tự động, vui lòng không trả lời.</p>");
        }
        else
        {
            html.Append(CultureInfo.InvariantCulture, $"<p class=\"rp-text-strong\" style=\"margin:0 0 6px 0;font-family:{FontFamily};font-size:13px;font-weight:600;line-height:1.5;color:#2D282A;\">RikiPath Sakura · Nền tảng học tiếng Nhật &amp; luyện thi JLPT</p>");
            html.Append(CultureInfo.InvariantCulture, $"<p class=\"rp-text-muted\" style=\"margin:0 0 8px 0;font-family:{FontFamily};font-size:12px;line-height:1.6;color:#776C70;\">Cần hỗ trợ? Liên hệ <a href=\"mailto:{safeSupportEmail}\" class=\"rp-link-accent\" style=\"color:#B53D5C;text-decoration:underline;font-weight:600;\">{safeSupportEmail}</a>");
            if (!string.IsNullOrEmpty(webUrl))
            {
                var safeWebUrl = Encode(webUrl);
                html.Append(CultureInfo.InvariantCulture, $" hoặc truy cập <a href=\"{safeWebUrl}\" target=\"_blank\" rel=\"noopener noreferrer\" class=\"rp-link-accent\" style=\"color:#B53D5C;text-decoration:underline;font-weight:600;\">RikiPath Web</a>");
            }
            html.Append(".</p>");
            html.Append(CultureInfo.InvariantCulture, $"<p class=\"rp-text-muted\" style=\"margin:0;font-family:{FontFamily};font-size:11px;line-height:1.55;color:#776C70;\">Đây là email tự động từ hệ thống RikiPath. &copy; {year} RikiPath. Bảo lưu mọi quyền.</p>");
        }
        html.Append("</td>");
        html.Append("</tr>");

        html.Append("</table>");
        html.Append("<!--[if mso]></td></tr></table><![endif]-->");
        html.Append("</td>");
        html.Append("</tr>");
        html.Append("</table>");
        html.Append("</body>");
        html.Append("</html>");

        var plain = BuildPlainTextFooter(subject, plainLines, supportEmail, webUrl, year);
        return new RenderedEmailContent(subject, html.ToString(), plain);
    }

    private static string BuildPlainTextFooter(
        string subject,
        IEnumerable<string> bodyLines,
        string supportEmail,
        string? webUrl,
        string year)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RIKIPATH — Nền tảng học tiếng Nhật");
        sb.AppendLine(subject);
        sb.AppendLine(new string('-', 48));
        foreach (var line in bodyLines)
            sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine(new string('-', 48));
        sb.AppendLine(CultureInfo.InvariantCulture, $"Hỗ trợ: {supportEmail}");
        if (!string.IsNullOrEmpty(webUrl))
            sb.AppendLine(CultureInfo.InvariantCulture, $"Website: {webUrl}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Email tự động từ hệ thống RikiPath. © {year} RikiPath.");
        return sb.ToString().TrimEnd();
    }

    private static string RenderParagraph(string trustedOrPreEncodedHtml)
        => $"<p class=\"rp-text-body\" style=\"margin:0 0 14px 0;font-family:{FontFamily};font-size:15px;line-height:1.65;color:#5F5559;\">{trustedOrPreEncodedHtml}</p>";

    private static string RenderMutedNote(string text)
        => $"<p class=\"rp-text-muted\" style=\"margin:14px 0 0 0;font-family:{FontFamily};font-size:12px;line-height:1.55;color:#776C70;text-align:center;\">{Encode(text)}</p>";

    private static string RenderMinimalOtp(string safeOtp, int minutes)
    {
        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:18px 0 16px 0;">
                <tr>
                    <td align="center" style="padding:4px 0;text-align:center;">
                        <div class="rp-text-muted" style="margin:0 0 8px 0;font-family:{FontFamily};font-size:11px;font-weight:700;letter-spacing:0.1em;text-transform:uppercase;color:#776C70;">Mã xác thực</div>
                        <div class="rp-otp-code" style="margin:0 0 8px 0;padding-left:10px;font-family:{MonoFontFamily};font-size:36px;font-weight:700;letter-spacing:10px;line-height:1.15;color:#B53D5C;user-select:all;-webkit-user-select:all;">{safeOtp}</div>
                        <div class="rp-text-muted" style="margin:0;font-family:{FontFamily};font-size:12px;font-weight:500;line-height:1.45;color:#776C70;">Mã có hiệu lực trong <strong class="rp-brand-title" style="color:#B53D5C;">{minutes} phút</strong></div>
                    </td>
                </tr>
            </table>
            """;
    }

    private static string RenderPrimaryButton(string text, string safeUrl, bool compactMargin = false)
    {
        var safeText = Encode(text);
        var margin = compactMargin ? "16px 0 6px 0" : "22px 0";
        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:{margin};">
                <tr>
                    <td align="center">
                        <!--[if mso]>
                        <v:roundrect xmlns:v="urn:schemas-microsoft-com:vml" xmlns:w="urn:schemas-microsoft-com:office:word" href="{safeUrl}" style="height:44px;v-text-anchor:middle;width:240px;" arcsize="22%" stroke="f" fillcolor="#CC4B6B">
                            <w:anchorlock/>
                            <center style="color:#FFFFFF;font-family:Arial,sans-serif;font-size:14px;font-weight:bold;">{safeText}</center>
                        </v:roundrect>
                        <![endif]-->
                        <!--[if !mso]><!-->
                        <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                                <td align="center" bgcolor="#CC4B6B" style="border-radius:10px;background-color:#CC4B6B;">
                                    <a href="{safeUrl}" target="_blank" rel="noopener noreferrer" style="display:inline-block;padding:12px 28px;font-family:{FontFamily};font-size:14px;font-weight:700;line-height:20px;color:#FFFFFF;text-decoration:none;border-radius:10px;">{safeText}</a>
                                </td>
                            </tr>
                        </table>
                        <!--<![endif]-->
                    </td>
                </tr>
            </table>
            """;
    }

    private static string RenderCopyableLinkBox(string label, string safeUrl)
    {
        var safeLabel = Encode(label);
        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 18px 0;">
                <tr>
                    <td class="rp-neutral-box" style="padding:12px 14px;background-color:#FAF7F8;border:1px solid #EAE3E4;border-radius:10px;">
                        <div class="rp-text-muted" style="margin:0 0 4px 0;font-family:{FontFamily};font-size:12px;font-weight:600;color:#776C70;">{safeLabel}</div>
                        <div style="font-family:{MonoFontFamily};font-size:12px;line-height:1.5;word-break:break-all;color:#B53D5C;">
                            <a href="{safeUrl}" target="_blank" rel="noopener noreferrer" class="rp-link-accent" style="color:#B53D5C;text-decoration:underline;">{safeUrl}</a>
                        </div>
                    </td>
                </tr>
            </table>
            """;
    }

    private static string RenderHighlightBanner(string label, string value, SakuraEmailTone tone)
    {
        var (bg, border, ink) = GetToneColors(tone);
        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:16px 0;">
                <tr>
                    <td style="padding:14px 16px;background-color:{bg};border:1px solid {border};border-radius:12px;">
                        <div style="margin:0 0 4px 0;font-family:{FontFamily};font-size:11px;font-weight:700;letter-spacing:0.05em;text-transform:uppercase;color:{ink};">{Encode(label)}</div>
                        <div style="font-family:{FontFamily};font-size:16px;font-weight:700;line-height:1.4;color:#2D282A;">{Encode(value)}</div>
                    </td>
                </tr>
            </table>
            """;
    }

    private static string RenderDetailRows(IReadOnlyList<EmailDetailItem> items)
    {
        if (items.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"rp-neutral-box\" style=\"margin:16px 0;background-color:#FAF7F8;border:1px solid #EAE3E4;border-radius:12px;border-collapse:separate;\">");
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var borderStyle = i < items.Count - 1 ? "border-bottom:1px solid #EAE3E4;" : string.Empty;
            sb.Append("<tr>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"rp-detail-row rp-detail-lbl\" valign=\"top\" style=\"width:36%;padding:11px 14px;{borderStyle}font-family:{FontFamily};font-size:13px;font-weight:600;line-height:1.5;color:#776C70;\">{Encode(item.Label)}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"rp-detail-row rp-detail-val\" valign=\"top\" style=\"width:64%;padding:11px 14px;{borderStyle}font-family:{FontFamily};font-size:13px;font-weight:600;line-height:1.5;color:#2D282A;text-align:right;\">{Encode(item.Value)}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();
    }

    private static string RenderCallout(SakuraEmailTone tone, string title, string message)
    {
        var (bg, border, ink) = GetToneColors(tone);
        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:14px 0;">
                <tr>
                    <td style="padding:13px 15px;background-color:{bg};border:1px solid {border};border-left:4px solid {ink};border-radius:10px;">
                        <div style="margin:0 0 3px 0;font-family:{FontFamily};font-size:13px;font-weight:700;line-height:1.4;color:{ink};">{Encode(title)}</div>
                        <div style="font-family:{FontFamily};font-size:13px;line-height:1.55;color:#2D282A;">{Encode(message)}</div>
                    </td>
                </tr>
            </table>
            """;
    }

    private static void AppendPlainDetails(List<string> lines, IReadOnlyList<EmailDetailItem>? details)
    {
        if (details is not { Count: > 0 })
            return;

        lines.Add(string.Empty);
        foreach (var detail in details)
            lines.Add($"- {detail.Label}: {detail.Value}");
    }

    private static (string Bg, string Border, string Ink) GetToneColors(SakuraEmailTone tone) => tone switch
    {
        SakuraEmailTone.Success => ("#ECF8F0", "#BBE5C8", "#0D7A42"),
        SakuraEmailTone.Warning => ("#FFF9EB", "#F5DC9C", "#8C4D0C"),
        SakuraEmailTone.Danger => ("#FEF1F1", "#F3C4C4", "#C22A2A"),
        SakuraEmailTone.Info => ("#EEF5FD", "#CFE2F8", "#1D62AB"),
        _ => ("#FFF4F7", "#F2CCD7", "#B53D5C"),
    };

    private string ResolveSupportEmail()
    {
        var configured = appSettings?.SmtpSettings?.FromEmail?.Trim();
        if (!string.IsNullOrWhiteSpace(configured) && MailAddress.TryCreate(configured, out var addr))
            return addr.Address;
        return "no-reply@rikipath.com";
    }

    private string? ResolveFrontendBaseUrl()
    {
        var raw = appSettings?.FrontendBaseUrl?.Trim();
        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            return uri.AbsoluteUri.TrimEnd('/');
        return null;
    }

    private string? ResolveSafeUrl(string? candidate)
    {
        var raw = ResolveRawSafeUrl(candidate);
        return raw is null ? null : Encode(raw);
    }

    private string? ResolveRawSafeUrl(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        var trimmed = candidate.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var abs) && abs.Scheme is "http" or "https")
            return abs.AbsoluteUri;

        var root = ResolveFrontendBaseUrl();
        if (root is not null && trimmed.StartsWith('/'))
            return $"{root}{trimmed}";

        return null;
    }

    private static string NormalizeName(string? name)
        => string.IsNullOrWhiteSpace(name) ? "bạn" : name.Trim();

    private static string Encode(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam");
            }
        }
    }
}
