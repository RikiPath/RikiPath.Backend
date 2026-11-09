using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RikiPath.WebApi.Swagger;

/// <summary>
/// Bổ sung metadata tối thiểu, ổn định cho mọi operation được công bố.
/// </summary>
public sealed class CanonicalApiOperationFilter : IOperationFilter
{
    private static readonly Regex WordBoundary = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);
    private static readonly Regex InvalidOperationIdCharacters = new("[^A-Za-z0-9_]", RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<string, string> ActionSummaries = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["BookSlot"] = "Đặt lịch tư vấn với Cố vấn",
        ["ReleaseBookingHold"] = "Giải phóng lịch giữ chỗ",
        ["CancelBooking"] = "Hủy lịch tư vấn",
        ["RescheduleBooking"] = "Đổi lịch tư vấn",
        ["CheckCancellationEligibility"] = "Kiểm tra điều kiện hủy lịch",
        ["ReviewCancellationException"] = "Xét duyệt ngoại lệ hủy lịch",
        ["GetMyBalance"] = "Xem số dư lượt tư vấn",
        ["GetMyHistory"] = "Xem lịch sử lượt tư vấn",
        ["ReconcileLearnerCredits"] = "Đối soát lượt tư vấn của Học viên",
        ["GetMyScheduleRules"] = "Xem quy tắc lịch làm việc của tôi",
        ["UpdateMyScheduleRules"] = "Cập nhật quy tắc lịch làm việc của tôi",
        ["PreviewSlots"] = "Xem trước khung giờ tư vấn",
        ["PublishSlots"] = "Công bố khung giờ tư vấn",
        ["DisableSlot"] = "Ngừng cung cấp khung giờ tư vấn",
        ["GetMentorScheduleForAdmin"] = "Xem lịch làm việc của Cố vấn",
        ["SearchMentors"] = "Tìm kiếm Cố vấn",
        ["GetMentorProfile"] = "Xem hồ sơ Cố vấn",
        ["GetPublicSlots"] = "Xem khung giờ trống của Cố vấn",
        ["GetAvailableDates"] = "Xem ngày còn khung giờ tư vấn",
        ["GetPublicSlotById"] = "Xem khung giờ tư vấn",
        ["CreateReview"] = "Gửi đánh giá buổi tư vấn",
        ["GetMentorReviews"] = "Xem đánh giá Cố vấn",
        ["GetMentorRatingSummary"] = "Xem tổng hợp đánh giá Cố vấn",
        ["CreateDispute"] = "Gửi khiếu nại buổi tư vấn",
        ["GetLearnerDisputes"] = "Xem khiếu nại của tôi",
        ["GetMentorDisputes"] = "Xem khiếu nại liên quan đến tôi",
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.ApiDescription.HttpMethod ?? "GET";
        var route = context.ApiDescription.RelativePath ?? "api";
        var controllerName = context.MethodInfo.DeclaringType?.Name.Replace("Controller", string.Empty, StringComparison.Ordinal)
            ?? "Api";
        operation.OperationId = BuildOperationId(controllerName, method, route);

        var hasCatalog = CanonicalApiOperationCatalog.TryGetOperation(operation.OperationId, out var catalogMeta);

        // 1. Phân nhóm Canonical Tags theo chuẩn Product Owner
        var tagName = hasCatalog
            ? catalogMeta.Tag
            : GetBusinessTag(route);
        operation.Tags = [new OpenApiTag { Name = tagName }];

        // 2. Summary: Ưu tiên XML comment nếu có và hợp lệ, nếu không lấy từ Catalog hoặc Fallback
        if (string.IsNullOrWhiteSpace(operation.Summary) || IsBoilerplateSummary(operation.Summary))
        {
            if (hasCatalog)
            {
                operation.Summary = catalogMeta.Summary;
            }
            else
            {
                operation.Summary = ActionSummaries.TryGetValue(context.MethodInfo.Name, out var knownSummary)
                    ? knownSummary
                    : BuildFallbackSummary(method, route);
            }
        }

        // 3. Description: Ưu tiên XML remarks nếu có và hợp lệ, nếu không lấy từ Catalog
        if (string.IsNullOrWhiteSpace(operation.Description) || IsBoilerplateDescription(operation.Description))
        {
            if (hasCatalog)
            {
                operation.Description = catalogMeta.Description;
            }
            else
            {
                operation.Description = $"{operation.Summary}. API thực hiện thao tác theo cấu hình nghiệp vụ của hệ thống.";
            }
        }

        // 4. Parameter Descriptions
        foreach (var parameter in operation.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Description) || parameter.Description == "Giá trị tham số này được sử dụng trong yêu cầu.")
            {
                parameter.Description = CanonicalApiOperationCatalog.GetParameterDescription(parameter.Name, route);
            }
        }

        // 5. Request Body Description
        if (operation.RequestBody is not null)
        {
            if (string.IsNullOrWhiteSpace(operation.RequestBody.Description) || operation.RequestBody.Description == "Dữ liệu đầu vào của yêu cầu.")
            {
                operation.RequestBody.Description = CanonicalApiOperationCatalog.GetRequestBodyDescription(operation.OperationId);
            }
        }

        // 6. Response Descriptions
        foreach (var response in operation.Responses)
        {
            if (string.IsNullOrWhiteSpace(response.Value.Description) || IsBoilerplateResponse(response.Value.Description))
            {
                response.Value.Description = CanonicalApiOperationCatalog.GetResponseDescription(response.Key, operation.Summary);
            }
        }

        if (context.ApiDescription.ActionDescriptor.EndpointMetadata?.OfType<IAllowAnonymous>().Any() == true)
        {
            // Ghi đè security ở cấp document cho endpoint công khai.
            operation.Security = [];
        }
    }

    private static bool IsBoilerplateSummary(string summary) =>
        summary.Contains("kỳ bảng lương tháng của Cố vấn") ||
        summary.Contains("tài nguyên API") ||
        summary.Contains("dữ liệu liên quan");

    private static bool IsBoilerplateDescription(string description) =>
        description.Contains("API áp dụng quyền truy cập và quy tắc nghiệp vụ đã cấu hình cho thao tác này.") ||
        description.Contains("API áp dụng quyền truy cập");

    private static bool IsBoilerplateResponse(string description) =>
        description == "Yêu cầu được xử lý thành công." ||
        description == "Tài nguyên được tạo thành công." ||
        description.StartsWith("Phản hồi HTTP");

    public static string BuildOperationId(string controllerName, string method, string route)
    {
        var routeTokens = Regex.Replace(route, @"\{([^}:]+)(?::[^}]+)?\}", "by_$1");
        var identifier = InvalidOperationIdCharacters.Replace($"{controllerName}_{method}_{routeTokens}", "_");
        return Regex.Replace(identifier, "_+", "_").Trim('_');
    }

    public static string BuildFallbackSummary(string method, string route)
    {
        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !segment.Equals("api", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("v1", StringComparison.OrdinalIgnoreCase)
                && !segment.StartsWith('{'))
            .ToArray();
        var resource = segments.Length == 0 ? "tài nguyên API" : DescribeResource(segments[^1]);
        var verb = method.ToUpperInvariant() switch
        {
            "GET" => "Lấy thông tin",
            "POST" => "Thực hiện thao tác với",
            "PUT" or "PATCH" => "Cập nhật thông tin",
            "DELETE" => "Xóa thông tin",
            _ => "Xử lý yêu cầu"
        };
        return $"{verb} {resource}.";
    }

    private static string DescribeResource(string value) => value.ToLowerInvariant() switch
    {
        "mentor-bookings" or "bookings" => "lịch tư vấn",
        "mentor-meetings" or "meetings" => "buổi tư vấn",
        "mentor-credits" or "credits" or "balance" => "lượt tư vấn",
        "mentors" or "mentor" => "hồ sơ Cố vấn",
        "mentor-reviews" or "reviews" or "rating-summary" => "đánh giá Cố vấn",
        "mentor-disputes" or "disputes" => "khiếu nại buổi tư vấn",
        "certificate-sections" => "nhóm chứng chỉ",
        "dashboard" => "bảng điều khiển",
        "verifications" => "hồ sơ xác minh",
        "verification-document" => "tài liệu xác minh",
        "content" => "nội dung",
        "pending" => "hồ sơ chờ xử lý",
        "mentor-availabilities" => "khung giờ tư vấn",
        "review" => "yêu cầu xét duyệt",
        "review-cancel" => "yêu cầu hủy lịch",
        "mentor-plans" => "gói tư vấn",
        "users" => "người dùng",
        "qa-payments" => "thanh toán thử nghiệm",
        "study-streak" => "chuỗi ngày học",
        "completion-stats" => "thống kê hoàn thành",
        "skill-breakdown" => "kết quả theo kỹ năng",
        "register" => "đăng ký",
        "resend-verification-email" => "gửi lại thư xác minh",
        "firebase-phone-register" => "đăng ký bằng số điện thoại",
        "verify-email" => "xác minh địa chỉ thư điện tử",
        "google" => "đăng nhập bằng Google",
        "login" => "đăng nhập",
        "firebase-phone-login" => "đăng nhập bằng số điện thoại",
        "update-email" => "địa chỉ thư điện tử",
        "change-password" => "mật khẩu",
        "certificatelevel" or "jlpt" => "cấp độ JLPT",
        "by-certificate" => "cấp độ chứng chỉ",
        "certificate-types" => "loại chứng chỉ",
        "submit-import" => "gửi yêu cầu nhập dữ liệu",
        "mine" => "dữ liệu của tôi",
        "requests" => "yêu cầu xét duyệt",
        "batch-review" => "xét duyệt hàng loạt",
        "preview" => "bản xem trước",
        "confirm" => "xác nhận",
        "unread-count" => "số thông báo chưa đọc",
        "mark-all-read" => "đánh dấu đã đọc",
        "upload" => "tệp tải lên",
        "search" => "kết quả tìm kiếm",
        "due" => "nội dung đến hạn",
        "all" => "toàn bộ nội dung",
        "scores" => "điểm số",
        "scan" => "kết quả chấm bài",
        "rescan" => "kết quả chấm lại",
        "latest" => "lộ trình học mới nhất",
        "active" => "lộ trình học hiện tại",
        "history" => "lịch sử học tập",
        "placement" => "bài kiểm tra xếp lớp",
        "detailed" => "kết quả chi tiết",
        "lists" => "danh sách ghi chú",
        "entries" => "mục ghi chú",
        "bookmark" => "nội dung đã lưu",
        "vocabulary" => "từ vựng",
        "kanji" => "Kanji",
        "grammar" => "ngữ pháp",
        "webhook" => "thông báo thanh toán",
        "status" => "trạng thái",
        "attempts" => "lượt làm bài",
        "start" => "bài làm mới",
        "submit" => "bài làm đã nộp",
        "complete" => "kết quả hoàn thành",
        "lessons" => "bài học",
        "playback-position" => "vị trí phát bài học",
        "skip" => "lựa chọn bỏ qua",
        "daily-queue" => "danh sách ôn tập hôm nay",
        "submit-result" => "kết quả ôn tập",
        "devices" => "thiết bị",
        "notifications" or "notification" => "thông báo",
        "admin" => "dữ liệu quản trị",
        "auth" => "tài khoản",
        "userprofile" => "hồ sơ người dùng",
        "learningpath" or "standardroadmap" or "lessonprogress" => "tiến độ học tập",
        "schedule" or "rules" => "lịch làm việc",
        "availability" or "slots" => "khung giờ tư vấn",
        "mentor-compensation" or "compensation" or "overview" => "thù lao và đối soát Cố vấn",
        "hourly-rates" => "đơn giá thù lao theo giờ của Cố vấn",
        "jlpt-coefficients" => "hệ số thù lao theo trình độ JLPT",
        "qualifications" or "grant" or "revoke" => "quyền giảng dạy trình độ JLPT của Cố vấn",
        "attendance-approval" or "attendance" or "evaluate" => "xác nhận thời lượng buổi tư vấn",
        "earnings" => "sổ cái thù lao buổi tư vấn",
        "payrolls" or "generate" or "approve" => "kỳ bảng lương tháng của Cố vấn",
        "adjustments" => "bút toán điều chỉnh bảng lương",
        "bank-accounts" or "bank-account" or "verify" => "tài khoản ngân hàng nhận thù lao",
        "organization-payout-accounts" => "tài khoản nguồn chi trả của tổ chức",
        "payout-obligations" or "payouts" or "select-method" or "method" => "nghĩa vụ chi trả thù lao",
        "manual-initiate" or "manual-confirm" or "initiate" => "chi trả chuyển khoản ngân hàng thủ công",
        "payos-execute" or "batch-payout" or "batch" or "balance" or "estimate-credit" or "payos" or "execute" => "chi trả qua cổng payOS Payout",
        "reconcile" => "đối soát trạng thái chi trả",
        _ => "dữ liệu liên quan"
    };

    private static string Humanize(string value)
    {
        var words = WordBoundary.Replace(value.Replace('-', ' ').Replace('_', ' '), "$1 $2");
        return words.Length == 0 ? "resource" : char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static string GetBusinessTag(string route)
    {
        var segment = route.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(value => !value.Equals("api", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("v1", StringComparison.OrdinalIgnoreCase));
        return segment?.ToLowerInvariant() switch
        {
            "admin" => "Administration",
            "mentor" or "mentors" or "mentor-bookings" or "mentor-meetings" or "mentor-credits" or "mentor-reviews" or "mentor-disputes" or "mentor-schedule" or "mentor-compensation" => "Mentor Support",
            "learner" or "credits" => "Learner",
            "auth" => "Authentication",
            "userprofile" => "User Profile",
            "learningpath" or "standardroadmap" or "lessonprogress" => "Learning",
            "devices" or "notification" => "Notifications",
            null => "API",
            _ => Humanize(segment)
        };
    }

    private static string GetResponseDescription(string statusCode) => statusCode switch
    {
        "200" => "Yêu cầu được xử lý thành công.",
        "201" => "Tài nguyên được tạo thành công.",
        "204" => "Yêu cầu được xử lý thành công và không có nội dung trả về.",
        "400" => "Dữ liệu yêu cầu không hợp lệ.",
        "401" => "Yêu cầu cần được xác thực.",
        "403" => "Tài khoản không có quyền thực hiện thao tác này.",
        "404" => "Không tìm thấy tài nguyên được yêu cầu.",
        "409" => "Yêu cầu xung đột với trạng thái hiện tại của tài nguyên.",
        "500" => "Đã xảy ra lỗi khi xử lý yêu cầu.",
        _ => $"Phản hồi HTTP {statusCode}.",
    };
}
