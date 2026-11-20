using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using RikiPath.WebApi.Controllers;
using RikiPath.WebApi.Swagger;
using Swashbuckle.AspNetCore.Swagger;

namespace RikiPath.Tests;

/// <summary>
/// Kiểm tra tài liệu OpenAPI/Swagger sinh tự động từ Controller, XML comments và filters tối giản.
/// </summary>
public sealed class SwaggerDocumentationQualityTests
{
    private static readonly string[] ProhibitedPlaceholders =
    [
        "API thực hiện thao tác theo cấu hình nghiệp vụ của hệ thống.",
        "Giá trị tham số này được sử dụng trong yêu cầu.",
        "Dữ liệu đầu vào của yêu cầu.",
        "Dữ liệu thông tin đầu vào của yêu cầu.",
        "Mô hình dữ liệu ",
        "Giá trị của trường ",
        "Thực hiện thao tác với dữ liệu liên quan."
    ];

    private static (OpenApiDocument Document, IReadOnlyList<ApiDescription> ApiDescriptions) BuildOpenApiDocument()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            var webApiAsm = typeof(AuthController).Assembly;
            var baseDir = Path.GetDirectoryName(webApiAsm.Location)!;
            foreach (var file in new[] { $"{webApiAsm.GetName().Name}.xml", "RikiPath.Application.xml" })
            {
                var xmlPath = Path.Combine(baseDir, file);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                }
            }

            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "RikiPath API",
                Version = "v1",
                Description = "API RikiPath sử dụng các đường dẫn nghiệp vụ chuẩn, không gắn phiên bản."
            });
            c.DocumentFilter<CanonicalApiDocumentFilter>();
            c.OperationFilter<CanonicalApiOperationFilter>();

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Nhập JWT Bearer theo định dạng: Bearer {token}.",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };
            c.AddSecurityDefinition("Bearer", securityScheme);
            c.AddSecurityRequirement(new OpenApiSecurityRequirement { { securityScheme, Array.Empty<string>() } });
        });

        using var app = builder.Build();
        app.MapControllers();

        var doc = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var apiDescriptions = app.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .ToList();

        return (doc, apiDescriptions);
    }

    [Fact]
    public void GeneratedOpenApiDocument_CoversAllActions_WithUniqueOperationIdsAndCleanMetadata()
    {
        var (doc, apiDescriptions) = BuildOpenApiDocument();

        Assert.NotEmpty(doc.Paths);
        var operations = doc.Paths
            .SelectMany(p => p.Value.Operations.Select(o => (Path: p.Key, Method: o.Key, Op: o.Value)))
            .ToList();

        Assert.Equal(apiDescriptions.Count, operations.Count);

        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, method, op) in operations)
        {
            Assert.False(string.IsNullOrWhiteSpace(op.OperationId), $"{method} {path} missing operationId.");
            Assert.True(operationIds.Add(op.OperationId!), $"Duplicate operationId {op.OperationId}.");
            Assert.False(string.IsNullOrWhiteSpace(op.Summary), $"{method} {path} missing summary.");
            Assert.NotEmpty(op.Tags);

            foreach (var prohibited in ProhibitedPlaceholders)
            {
                Assert.DoesNotContain(prohibited, op.Summary, StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(op.Description))
                {
                    Assert.DoesNotContain(prohibited, op.Description, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        // Mọi schema không bị gắn câu mô tả boilerplate nhân tạo.
        Assert.NotEmpty(doc.Components.Schemas);
        foreach (var (schemaName, schema) in doc.Components.Schemas)
        {
            if (!string.IsNullOrWhiteSpace(schema.Description))
            {
                Assert.DoesNotContain("Mô hình dữ liệu ", schema.Description, StringComparison.OrdinalIgnoreCase);
            }
            foreach (var (_, prop) in schema.Properties)
            {
                if (!string.IsNullOrWhiteSpace(prop.Description))
                {
                    Assert.DoesNotContain("Mô hình dữ liệu ", prop.Description, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Giá trị của trường ", prop.Description, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    [Fact]
    public void DocumentTags_HaveNoOrphans_AndAllUsedTagsHaveDescriptions()
    {
        var (doc, _) = BuildOpenApiDocument();
        var usedTags = doc.Paths.Values
            .SelectMany(p => p.Operations.Values)
            .SelectMany(o => o.Tags)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotNull(doc.Tags);
        Assert.Equal(usedTags.Count, doc.Tags!.Count);
        foreach (var tag in doc.Tags)
        {
            Assert.Contains(tag.Name, usedTags);
            Assert.False(string.IsNullOrWhiteSpace(tag.Description), $"Tag {tag.Name} missing description.");
        }
    }

    [Fact]
    public void XmlControllerDocumentation_FlowsThroughWithoutManualCatalogOverride()
    {
        var (doc, _) = BuildOpenApiDocument();

        var loginOp = doc.Paths["/api/Auth/login"].Operations[OperationType.Post];
        Assert.Equal("Auth_POST_api_Auth_login", loginOp.OperationId);
        Assert.Contains("JWT token", loginOp.Summary);
        Assert.Contains("LoginRequest", loginOp.RequestBody.Description);

        // Endpoint không có XML comment (StudyCourses / MentorKpi) vẫn nhận summary/tag tự động.
        var kpiOp = doc.Paths["/api/mentor/kpi/summary"].Operations[OperationType.Get];
        Assert.Equal("MentorKpi_GET_api_mentor_kpi_summary", kpiOp.OperationId);
        Assert.False(string.IsNullOrWhiteSpace(kpiOp.Summary));

        // JSON serialize hợp lệ OpenAPI 3.0.
        var json = doc.SerializeAsJson(Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0);
        Assert.False(string.IsNullOrWhiteSpace(json));
    }

    [Fact]
    public void AllowAnonymousEndpoints_ClearBearerSecurityRequirement()
    {
        var (doc, apiDescriptions) = BuildOpenApiDocument();

        foreach (var api in apiDescriptions)
        {
            var isAnon = api.ActionDescriptor.EndpointMetadata?.OfType<IAllowAnonymous>().Any() == true;
            if (!isAnon) continue;

            var pathKey = "/" + api.RelativePath!.TrimStart('/');
            if (!doc.Paths.TryGetValue(pathKey, out var pathItem)) continue;

            var opType = Enum.Parse<OperationType>(api.HttpMethod!, ignoreCase: true);
            if (pathItem.Operations.TryGetValue(opType, out var op))
            {
                Assert.NotNull(op.Security);
                Assert.Empty(op.Security);
            }
        }
    }
}
