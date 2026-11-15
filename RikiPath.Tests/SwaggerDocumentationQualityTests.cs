using System;
using System.Linq;
using Microsoft.OpenApi.Models;
using RikiPath.WebApi.Swagger;
using Xunit;

namespace RikiPath.Tests;

/// <summary>
/// Kiểm thử tự động chất lượng tài liệu Swagger và phân nhóm Canonical Tags.
/// Đảm bảo không còn bất kỳ chuỗi mô tả boilerplate / placeholder chung chung nào.
/// </summary>
public sealed class SwaggerDocumentationQualityTests
{
    private static readonly string[] ProhibitedBoilerplates =
    [
        "kỳ bảng lương tháng của Cố vấn",
        "Giá trị tham số này được sử dụng trong yêu cầu.",
        "API áp dụng quyền truy cập và quy tắc nghiệp vụ đã cấu hình cho thao tác này.",
        "Yêu cầu được xử lý thành công.",
        "Thực hiện thao tác với dữ liệu liên quan.",
        "Dữ liệu đầu vào của yêu cầu.",
        "Giá trị của trường"
    ];

    [Fact]
    public void CatalogContainsAllCanonicalOperationsWithNonEmptyMetadata()
    {
        Assert.Equal(256, CanonicalApiOperationCatalog.Operations.Count);

        foreach (var (operationId, meta) in CanonicalApiOperationCatalog.Operations)
        {
            Assert.False(string.IsNullOrWhiteSpace(meta.Tag), $"Operation {operationId} has empty Tag.");
            Assert.False(string.IsNullOrWhiteSpace(meta.Summary), $"Operation {operationId} has empty Summary.");
            Assert.False(string.IsNullOrWhiteSpace(meta.Description), $"Operation {operationId} has empty Description.");

            foreach (var prohibited in ProhibitedBoilerplates)
            {
                Assert.DoesNotContain(prohibited, meta.Summary, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(prohibited, meta.Description, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void AllOperationsBelongToApprovedCanonicalTags()
    {
        var approvedTags = CanonicalApiOperationCatalog.TagDescriptions.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (operationId, meta) in CanonicalApiOperationCatalog.Operations)
        {
            Assert.Contains(meta.Tag, approvedTags);
        }
    }

    [Fact]
    public void ListAndDetailOperationsHaveDistinctSummaries()
    {
        // Kiểm tra phân biệt giữa List payrolls và Detail payrolls của Mentor
        var mentorList = CanonicalApiOperationCatalog.Operations["MentorCompensation_GET_api_mentor_compensation_payrolls"];
        var mentorDetail = CanonicalApiOperationCatalog.Operations["MentorCompensation_GET_api_mentor_compensation_payrolls_by_payrollPeriodId"];
        Assert.NotEqual(mentorList.Summary, mentorDetail.Summary);

        // Kiểm tra phân biệt giữa List payouts và Detail payout của Mentor
        var payoutList = CanonicalApiOperationCatalog.Operations["MentorCompensation_GET_api_mentor_compensation_payouts"];
        var payoutDetail = CanonicalApiOperationCatalog.Operations["MentorCompensation_GET_api_mentor_compensation_payouts_by_obligationId"];
        Assert.NotEqual(payoutList.Summary, payoutDetail.Summary);

        // Kiểm tra phân biệt giữa Admin List payrolls và Admin Detail payroll
        var adminList = CanonicalApiOperationCatalog.Operations["AdminMentorCompensation_GET_api_admin_mentor_compensation_payrolls"];
        var adminDetail = CanonicalApiOperationCatalog.Operations["AdminMentorCompensation_GET_api_admin_mentor_compensation_payrolls_by_payrollPeriodId"];
        Assert.NotEqual(adminList.Summary, adminDetail.Summary);
    }

    [Fact]
    public void ParameterDescriptionsDoNotContainBoilerplate()
    {
        Assert.NotEmpty(CanonicalApiOperationCatalog.ParameterDescriptions);

        foreach (var (paramName, desc) in CanonicalApiOperationCatalog.ParameterDescriptions)
        {
            Assert.False(string.IsNullOrWhiteSpace(desc), $"Param {paramName} has empty description.");
            Assert.DoesNotContain("Giá trị tham số này được sử dụng trong yêu cầu.", desc);
        }
    }

    [Fact]
    public void ResponseDescriptionGeneratesMeaningfulResponses()
    {
        var desc200 = CanonicalApiOperationCatalog.GetResponseDescription("200", "Xem danh sách các kỳ đối soát thù lao của Cố vấn.");
        Assert.Contains("Xem danh sách các kỳ đối soát thù lao của Cố vấn", desc200);
        Assert.DoesNotContain("Yêu cầu được xử lý thành công.", desc200);

        var desc400 = CanonicalApiOperationCatalog.GetResponseDescription("400");
        Assert.Contains("không hợp lệ", desc400);

        var desc403 = CanonicalApiOperationCatalog.GetResponseDescription("403");
        Assert.Contains("không có quyền", desc403);

        var desc404 = CanonicalApiOperationCatalog.GetResponseDescription("404");
        Assert.Contains("Không tìm thấy", desc404);
    }
}
