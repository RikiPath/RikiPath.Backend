using System.Text.RegularExpressions;

namespace RikiPath.Application.Common;

/// <summary>
/// Phrase-safe Vietnamese display normalizer for system-generated notifications
/// (including legacy plain-text notifications already stored in the database).
/// Preserves user-generated content such as proper names (e.g. "Mentor One"),
/// class titles, document names, and original feedback text.
/// </summary>
public static partial class NotificationDisplayNormalizer
{
    private static readonly (Regex Pattern, string Replacement)[] PhraseRules =
    [
        // Mixed English-Vietnamese legacy phrasing
        (new Regex(@"\bAdmin\s+đã\s+approve\s+proposal\s+của\s+Mentor\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Quản trị viên đã phê duyệt đề xuất mở lớp của giáo viên"),
        (new Regex(@"\bAdmin\s+đã\s+approve\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Quản trị viên đã phê duyệt"),

        // Admin / Ban quản trị / Bộ phận quản trị → Quản trị viên (system actor phrases only)
        (new Regex(@"\bBan quản trị\b", RegexOptions.Compiled), "Quản trị viên"),
        (new Regex(@"\bBộ phận quản trị\b", RegexOptions.Compiled), "Quản trị viên"),
        (new Regex(@"\bQuản trị viên\s*\(Admin\)", RegexOptions.Compiled), "Quản trị viên"),
        (new Regex(@"\bAdmin\s+(đã|yêu cầu|từ chối|đang|sẽ|cần|duyệt|xử lý|chọn|phân công|cấp quyền|xem xét|phê duyệt|dời|mới|phải|có thể)\b", RegexOptions.Compiled), "Quản trị viên $1"),
        (new Regex(@"\b(chờ|gửi|cho|từ|của|đề nghị|liên hệ|nhờ|bởi|bị|lên|để|với)\s+[Aa]dmin\b", RegexOptions.Compiled), "$1 Quản trị viên"),
        (new Regex(@"\b(Kết luận|Quyết định|Ghi chú|Phản hồi)\s+Admin\b", RegexOptions.Compiled), "$1 của Quản trị viên"),

        // Cố vấn / Mentor → Giáo viên (system role phrases only — does NOT touch proper names like "Mentor One")
        (new Regex(@"\bLớp có cố vấn chính mới\b", RegexOptions.Compiled), "Lớp học có Giáo viên mới"),
        (new Regex(@"\bHồ sơ ứng tuyển cố vấn\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Hồ sơ Giáo viên"),
        (new Regex(@"\bHồ sơ cố vấn\b", RegexOptions.Compiled), "Hồ sơ Giáo viên"),
        (new Regex(@"\bhồ sơ cố vấn\b", RegexOptions.Compiled), "hồ sơ Giáo viên"),
        (new Regex(@"\bCố vấn\s+(đã|chính|mới|thay thế|khác|không|được|chưa|hủy|vắng|phản hồi|liên quan)\b", RegexOptions.Compiled), "Giáo viên $1"),
        (new Regex(@"\bcố vấn\s+(đã|chính|mới|thay thế|khác|không|được|chưa|này|hủy|vắng|phản hồi|liên quan)\b", RegexOptions.Compiled), "Giáo viên $1"),
        (new Regex(@"\b(của|cho|từ|do|thay|đánh giá|chọn|tìm thấy|chỉ|Chờ|Yêu cầu)\s+[Cc]ố vấn\b", RegexOptions.Compiled), "$1 Giáo viên"),
        (new Regex(@"\bMentor\s+(đã|chính|dạy thay|hủy|vắng|liên quan|không|chưa|có thể|mới)\b", RegexOptions.Compiled), "Giáo viên $1"),
        (new Regex(@"\b[Mm]entor\s+(chính|dạy thay|mới được|được tạo|được gửi)\b", RegexOptions.Compiled), "Giáo viên $1"),
        (new Regex(@"\b(của|cho|từ|thay|đánh giá|Thái độ|Nhận xét|Phản hồi của|chỉ|Chỉ|Chờ|Yêu cầu)\s+[Mm]entor\b", RegexOptions.Compiled), "$1 Giáo viên"),
        (new Regex(@"\bBuổi học Mentor\b", RegexOptions.Compiled), "Buổi dạy cùng Giáo viên"),

        // Learner → Học viên (role phrases only)
        (new Regex(@"\b(của|cho|để)\s+Learner\b", RegexOptions.Compiled), "$1 Học viên"),
        (new Regex(@"\bLearner\s+(mới|chưa|đã|đang)\b", RegexOptions.Compiled), "Học viên $1"),

        // Content Author → Quản lý nội dung
        (new Regex(@"\bContent\s*Author\b", RegexOptions.Compiled), "Quản lý nội dung"),

        // Common abbreviations & raw complaint category codes in legacy notifications
        (new Regex(@"\bBTVN\b", RegexOptions.Compiled), "bài tập về nhà"),
        (new Regex(@"\(TeachingQuality\)", RegexOptions.Compiled), "(Chất lượng giảng dạy)"),
        (new Regex(@"\(MentorConduct\)", RegexOptions.Compiled), "(Tác phong Giáo viên)"),
        (new Regex(@"\(Schedule\)", RegexOptions.Compiled), "(Lịch học)"),
        (new Regex(@"\(PaymentEnrollment\)", RegexOptions.Compiled), "(Thanh toán & Ghi danh)"),
        (new Regex(@"\(Other\)", RegexOptions.Compiled), "(Khác)"),
    ];

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        var result = text;
        foreach (var (pattern, replacement) in PhraseRules)
        {
            if (pattern.IsMatch(result))
                result = pattern.Replace(result, replacement);
        }

        return result;
    }
}
