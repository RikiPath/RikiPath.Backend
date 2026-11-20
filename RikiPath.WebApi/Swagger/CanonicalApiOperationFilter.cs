using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RikiPath.WebApi.Swagger;

/// <summary>
/// Chuẩn hóa operationId, nhóm tag nghiệp vụ và bảo mật Swagger từ metadata Controller/OpenAPI.
/// </summary>
public sealed class CanonicalApiOperationFilter : IOperationFilter
{
    private static readonly Regex WordBoundary = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);
    private static readonly Regex InvalidOperationIdCharacters = new("[^A-Za-z0-9_]", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> ResourceLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["certificate-sections"] = "nhóm chứng chỉ",
            ["mentors"] = "hồ sơ Cố vấn",
            ["mentor"] = "hồ sơ Cố vấn",
            ["mentor-bookings"] = "lịch tư vấn",
            ["bookings"] = "lịch tư vấn",
            ["mentor-meetings"] = "buổi tư vấn",
            ["mentor-credits"] = "lượt tư vấn",
            ["credits"] = "lượt tư vấn",
            ["mentor-reviews"] = "đánh giá Cố vấn",
            ["reviews"] = "đánh giá Cố vấn",
            ["mentor-disputes"] = "khiếu nại buổi tư vấn",
            ["disputes"] = "khiếu nại buổi tư vấn",
            ["mentor-availabilities"] = "khung giờ tư vấn",
            ["availability"] = "khung giờ tư vấn",
            ["mentor-plans"] = "gói tư vấn",
            ["qualifications"] = "quyền giảng dạy JLPT",
            ["kpi"] = "chỉ số KPI Cố vấn",
            ["attendance"] = "điểm danh buổi học",
            ["study-courses"] = "khóa học",
            ["cohorts"] = "lớp nhóm",
            ["homework"] = "bài tập",
            ["users"] = "người dùng",
            ["dashboard"] = "bảng điều khiển",
            ["notifications"] = "thông báo",
            ["subscriptions"] = "gói đăng ký",
        };

    private static readonly IReadOnlyDictionary<string, string> DefaultResponseDescriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["200"] = "Yêu cầu thành công.",
            ["201"] = "Tạo tài nguyên thành công.",
            ["204"] = "Hoàn tất (không có nội dung trả về).",
            ["400"] = "Dữ liệu yêu cầu không hợp lệ.",
            ["401"] = "Chưa xác thực hoặc phiên đăng nhập hết hạn.",
            ["403"] = "Không có quyền truy cập.",
            ["404"] = "Không tìm thấy tài nguyên.",
            ["409"] = "Xung đột trạng thái dữ liệu.",
            ["500"] = "Lỗi xử lý máy chủ.",
        };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.ApiDescription.HttpMethod ?? "GET";
        var route = context.ApiDescription.RelativePath ?? "api";
        var controllerName = context.MethodInfo.DeclaringType?.Name.Replace("Controller", string.Empty, StringComparison.Ordinal)
            ?? "Api";

        operation.OperationId = BuildOperationId(controllerName, method, route);

        var explicitTags = context.ApiDescription.ActionDescriptor.EndpointMetadata?
            .OfType<TagsAttribute>()
            .SelectMany(attr => attr.Tags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToArray();

        var tagNames = explicitTags is { Length: > 0 }
            ? explicitTags
            : [GetBusinessTag(route)];
        operation.Tags = tagNames.Select(name => new OpenApiTag { Name = name }).ToList();

        // Chỉ sinh summary dự phòng khi Controller chưa có XML <summary> hoặc [EndpointSummary].
        if (string.IsNullOrWhiteSpace(operation.Summary))
        {
            operation.Summary = BuildFallbackSummary(method, route, context.MethodInfo.Name);
        }

        foreach (var response in operation.Responses)
        {
            if (string.IsNullOrWhiteSpace(response.Value.Description))
            {
                response.Value.Description = DefaultResponseDescriptions.TryGetValue(response.Key, out var desc)
                    ? desc
                    : $"HTTP {response.Key}.";
            }
        }

        if (context.ApiDescription.ActionDescriptor.EndpointMetadata?.OfType<IAllowAnonymous>().Any() == true)
        {
            // Endpoint [AllowAnonymous] không yêu cầu JWT Bearer trên Swagger UI.
            operation.Security = [];
        }
    }

    public static string BuildOperationId(string controllerName, string method, string route)
    {
        var routeTokens = Regex.Replace(route, @"\{([^}:]+)(?::[^}]+)?\}", "by_$1");
        var identifier = InvalidOperationIdCharacters.Replace($"{controllerName}_{method}_{routeTokens}", "_");
        return Regex.Replace(identifier, "_+", "_").Trim('_');
    }

    public static string BuildFallbackSummary(string method, string route, string? actionName = null)
    {
        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !segment.Equals("api", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("v1", StringComparison.OrdinalIgnoreCase)
                && !segment.StartsWith('{'))
            .ToArray();

        var resource = segments.Length > 0 && ResourceLabels.TryGetValue(segments[^1], out var knownLabel)
            ? knownLabel
            : Humanize(actionName ?? (segments.Length > 0 ? segments[^1] : "endpoint"), lowercaseFirst: true);

        var verb = method.ToUpperInvariant() switch
        {
            "GET" => "Lấy thông tin",
            "POST" => "Thực hiện thao tác",
            "PUT" or "PATCH" => "Cập nhật",
            "DELETE" => "Xóa",
            _ => "Xử lý"
        };

        return $"{verb} {resource}.";
    }

    private static string GetBusinessTag(string route)
    {
        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !s.Equals("api", StringComparison.OrdinalIgnoreCase)
                && !s.Equals("v1", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.ToLowerInvariant())
            .ToArray();

        if (segments.Length == 0) return "API";

        var first = segments[0];
        var second = segments.Length > 1 ? segments[1] : string.Empty;

        return first switch
        {
            "admin" => second switch
            {
                "dashboard" => "Admin - Dashboard",
                "certificate-sections" or "certificate-types" => "Admin - Content",
                "mentor-availabilities" or "mentor-compensation" or "mentor-plans" or "mentors" or "mentor-quality" or "bookings" => "Admin - Mentors",
                _ => "Admin - Users & Operations"
            },
            "content-approvals" or "content-import" => "Admin - Content",
            "content-authoring" or "content-author" => "Content Authoring",
            "auth" => "Authentication",
            "analytics" => "Analytics",
            "userprofile" => "User Profile",
            "devices" or "notification" => "Notifications",
            "filestorage" => "File Storage",
            "notebook" => "Notebook",
            "payment" or "subscriptions" => "Payments & Subscriptions",
            "study-courses" => "Study Courses",
            "certificatelevel" or "certificate-types" or "japanese-dictionary" or "learningpath" or "lessonprogress" or "sharedbank" or "standardroadmap" => "Learning & Roadmap",
            "mocktest" or "placementtest" => "Mock & Placement Tests",
            "homework" or "kana-writing-practice" or "kanjiwritingpractice" or "learner-essays" or "practiceexercise" or "practice-exercises" or "practicesubmission" or "review" or "speech" => "Practice & Review",
            "learner" => "Learner - Sessions & Credits",
            "mentor-credits" => "Learner - Sessions & Credits",
            "mentors" when second != "me" => "Mentor Discovery",
            "mentors" or "mentor" or "mentor-bookings" or "mentor-meetings" or "mentor-chat" or "mentor-reviews" or "mentor-disputes" or "mentor-schedule" or "mentor-weekly" => "Mentor Operations & KPI",
            _ => Humanize(first)
        };
    }

    private static string Humanize(string value, bool lowercaseFirst = false)
    {
        var cleaned = WordBoundary.Replace(value.Replace('-', ' ').Replace('_', ' '), "$1 $2").Trim();
        if (cleaned.Length == 0) return "API";
        var normalized = lowercaseFirst ? char.ToLowerInvariant(cleaned[0]) : char.ToUpperInvariant(cleaned[0]);
        return normalized + cleaned[1..];
    }
}
