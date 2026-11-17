using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RikiPath.WebApi.Swagger;

/// <summary>
/// Đồng bộ và loại bỏ các nhóm tag mồ côi khỏi tài liệu OpenAPI sau khi gộp operation theo nhóm nghiệp vụ.
/// </summary>
public sealed class CanonicalApiDocumentFilter : IDocumentFilter
{
    private static readonly IReadOnlyDictionary<string, string> TagDescriptions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin - Dashboard"] = "Số liệu thống kê tổng quan cho Quản trị viên.",
            ["Admin - Mentors"] = "Quản lý Cố vấn: xác minh hồ sơ, lịch tư vấn, phân quyền JLPT và KPI.",
            ["Admin - Content"] = "Quản trị danh mục chứng chỉ, cấu trúc đề và phê duyệt nội dung.",
            ["Admin - Users & Operations"] = "Quản lý tài khoản người dùng và vận hành hệ thống.",
            ["Analytics"] = "Thống kê tiến độ học tập, chuỗi ngày học và phân tích kỹ năng.",
            ["Authentication"] = "Đăng ký, đăng nhập và xác thực tài khoản.",
            ["Content Authoring"] = "Biên soạn, import và lập kế hoạch nội dung học tập.",
            ["File Storage"] = "Tải lên và quản lý tệp đa phương tiện.",
            ["Learner - Sessions & Credits"] = "Học viên quản lý lượt tư vấn, đặt lịch hẹn 1-1 và khiếu nại.",
            ["Learning & Roadmap"] = "Lộ trình học JLPT, bài học, từ điển và ngân hàng kiến thức.",
            ["Mentor Discovery"] = "Tìm kiếm Cố vấn công khai, hồ sơ và lịch rảnh.",
            ["Mentor Operations & KPI"] = "Lịch làm việc, phòng họp trực tuyến, buổi tư vấn, đánh giá, khiếu nại và KPI Cố vấn.",
            ["Mock & Placement Tests"] = "Bài đánh giá năng lực đầu vào và đề thi thử JLPT.",
            ["Notebook"] = "Sổ tay ghi chú cá nhân và danh mục ôn tập.",
            ["Notifications"] = "Thông báo hệ thống và đăng ký thiết bị FCM.",
            ["Payments & Subscriptions"] = "Gói học, đăng ký dịch vụ và webhook thanh toán.",
            ["Practice & Review"] = "Luyện tập Kana, Kanji, viết luận AI, bài tập về nhà và SRS.",
            ["Study Courses"] = "Khóa học kèm cặp 1-1/nhóm theo lộ trình và lịch học cam kết.",
            ["User Profile"] = "Hồ sơ cá nhân, mục tiêu JLPT và cài đặt người dùng.",
        };

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var usedTags = swaggerDoc.Paths.Values
            .SelectMany(path => path.Operations.Values)
            .SelectMany(operation => operation.Tags)
            .Select(tag => tag.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        swaggerDoc.Tags ??= [];

        foreach (var unusedTag in swaggerDoc.Tags.Where(tag => !usedTags.Contains(tag.Name)).ToArray())
        {
            swaggerDoc.Tags.Remove(unusedTag);
        }

        foreach (var tag in swaggerDoc.Tags)
        {
            tag.Description = ResolveTagDescription(tag.Name);
        }

        var existing = swaggerDoc.Tags
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tagName in usedTags.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (existing.Add(tagName))
            {
                swaggerDoc.Tags.Add(new OpenApiTag
                {
                    Name = tagName,
                    Description = ResolveTagDescription(tagName)
                });
            }
        }
    }

    private static string ResolveTagDescription(string tagName) =>
        TagDescriptions.TryGetValue(tagName, out var desc)
            ? desc
            : $"Nhóm chức năng {tagName} của API RikiPath.";
}
