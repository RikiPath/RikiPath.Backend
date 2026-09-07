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
        public SupabaseSettings Supabase { get; set; }
        public AiSettings Ai { get; set; }
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
    }

    public class SupabaseSettings
    {
        public string Url { get; set; } = string.Empty;
        public string ServiceRoleKey { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
    }

    public class AiSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;

        // Model dùng để chấm bài viết/kanji - nên dùng model mạnh hơn vì sai lệch ở đây ảnh hưởng
        // trực tiếp tới điểm học viên. Mặc định openai/gpt-oss-120b.
        public string GradingModel { get; set; } = "openai/gpt-oss-120b";

        // Model dùng để gợi ý lộ trình học - có thể dùng model nhẹ/rẻ hơn (vd "openai/gpt-oss-20b")
        // vì yêu cầu suy luận thấp hơn chấm điểm. Đổi trực tiếp trong appsettings.json, không cần
        // build lại và không cần sửa code.
        public string LearningPathModel { get; set; } = "openai/gpt-oss-120b";

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
}
