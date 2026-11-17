using System.Runtime;

namespace RikiPath.Domain
{
    public class AppSettings
    {
        public ConnectionStrings ConnectionStrings { get; set; }
        public Logging Logging { get; set; }
        public string AllowedHosts { get; set; }
        public SecretToken SecretToken { get; set; }
        public SmtpSettings SmtpSettings { get; set; }
        public PayOsSettings PayOs { get; set; }
        public PayOsPayoutSettings PayOsPayout { get; set; } = new();
        public MentorBankSecuritySettings MentorBankSecurity { get; set; } = new();
        public SupabaseSettings Supabase { get; set; }
        public AiSettings Ai { get; set; } = new();
        public GoogleAuthSettings GoogleAuth { get; set; } = new();
        public string FrontendBaseUrl { get; set; }
        public GoogleAuthSettings Google { get; set; } = new();
        public AzureSpeechSettings AzureSpeechSettings { get; set; } = new();
        public QaPaymentSimulatorSettings QaPaymentSimulator { get; set; } = new();
        public GroupLearningSettings GroupLearning { get; set; } = new();
    }

    public sealed class GroupLearningSettings
    {
        /// <summary>
        /// Cổng an toàn: chuyển lớp sau khai giảng chỉ được bật khi Product Owner đã phê duyệt quyền lợi
        /// (buổi đã học, hoàn/thu chênh lệch). Mặc định TẮT.
        /// </summary>
        public bool PostStartTransferProductApproved { get; set; }
    }
    public class GoogleAuthSettings
    {
        public string? ClientId { get; set; }
    }
    public class ConnectionStrings
    {
        public string DefaultConnection { get; set; }
    }
    public class Logging
    {
        public LogLevel LogLevel { get; set; }
    }

    public class LogLevel
    {
        public string Default { get; set; }
        public string MicrosoftAspNetCore { get; set; }
    }
    public class SecretToken
    {
        public string Value { get; set; }
    }

    public class SmtpSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FromName { get; set; }
        public string FromEmail { get; set; }
    }

    public class PayOsSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ChecksumKey { get; set; } = string.Empty;
        public string WebReturnUrl { get; set; } = string.Empty;
        public string WebCancelUrl { get; set; } = string.Empty;

        public string MobileLandingBaseUrl { get; set; } = string.Empty;
    }

    public sealed class PayOsPayoutSettings
    {
        /// <summary>Live payout mặc định DISABLED để ngăn gửi tiền thật khi chưa được phê duyệt vận hành.</summary>
        public bool LivePayoutEnabled { get; set; } = false;
        public bool QaSimulatorEnabled { get; set; } = true;
        public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
        public string ClientId { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ChecksumKey { get; set; } = string.Empty;
    }

    public sealed class MentorBankSecuritySettings
    {
        public string EncryptionKey { get; set; } = "RikiPath-Mentor-Bank-AES256-Key-2026";
    }

    public class SupabaseSettings
    {
        public string Url { get; set; } = string.Empty;
        public string ServiceRoleKey { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public string VerificationBucket { get; set; } = string.Empty;
    }

    public class AiSettings
    {
        public AiRateLimitSettings RateLimit { get; set; } = new();
    }

    public class AiRateLimitSettings
    {
        // Số lượt gọi AI tối đa / user / ngày, tính riêng theo từng feature (Grading, LearningPath).
        public int MaxRequestsPerUserPerDay { get; set; } = 20;

        // Tổng số token (input + output cộng dồn, lấy từ usage.total_tokens của provider)
        // tối đa / user / ngày, tính riêng theo từng feature.
        public int MaxTokensPerUserPerDay { get; set; } = 50000;
    }

    public sealed class QaPaymentSimulatorSettings
    {
        public bool Enabled { get; set; }
        public string AllowedDatabaseName { get; set; } = string.Empty;
    }

    public class AzureSpeechSettings
    {
        // Region của resource Azure Speech Service (vd "southeastasia", "eastasia") - lấy trong
        // Azure Portal > resource Speech > Keys and Endpoint. PHẢI khớp đúng region của resource,
        // sai region sẽ báo lỗi 401/404 dù key đúng.
        public string Region { get; set; } = string.Empty;

        // 1 trong 2 key (KEY 1 hoặc KEY 2) trong Azure Portal > resource Speech > Keys and Endpoint.
        public string SubscriptionKey { get; set; } = string.Empty;

        // Locale nhận diện giọng nói - mặc định tiếng Nhật vì hệ thống luyện thi JLPT.
        public string Language { get; set; } = "ja-JP";
    }

}
