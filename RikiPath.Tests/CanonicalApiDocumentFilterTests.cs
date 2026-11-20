using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using RikiPath.WebApi.Controllers;
using RikiPath.WebApi.Swagger;

namespace RikiPath.Tests;

public sealed class CanonicalApiDocumentFilterTests
{
    [Fact]
    public void AdminAvailabilityControllerDeclaresOnlyTheCanonicalRoute()
    {
        var routes = typeof(AdminMentorAvailabilityController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Select(route => route.Template)
            .ToArray();

        Assert.Equal(["api/admin/mentor-availabilities"], routes);
    }

    [Fact]
    public void DocumentFilterRemovesUnusedControllerTagsAndUsesVietnameseDescriptions()
    {
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/api/learner/mentor-bookings"] = new OpenApiPathItem
                {
                    Operations =
                    {
                        [OperationType.Post] = new OpenApiOperation
                        {
                            Tags = [new OpenApiTag { Name = "Mentor Support" }]
                        }
                    }
                }
            },
            Tags =
            [
                new OpenApiTag { Name = "AdminCertificateSections", Description = "Nhóm quản trị." },
                new OpenApiTag { Name = "Mentor Support", Description = "Nhóm tư vấn." }
            ]
        };

        new CanonicalApiDocumentFilter().Apply(document, null!);

        Assert.Equal(["Mentor Support"], document.Tags!.Select(tag => tag.Name).ToArray());
        Assert.Equal("Nhóm chức năng Mentor Support của API RikiPath.", document.Tags![0].Description);
    }

    [Fact]
    public void NotificationControllerRegistersOnlyCanonicalPatchRoutes()
    {
        var actions = typeof(NotificationController).GetMethods()
            .SelectMany(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true)
                .Cast<HttpMethodAttribute>()
                .Select(attribute => (Method: method.Name, Verbs: attribute.HttpMethods, attribute.Template)))
            .ToArray();

        Assert.Contains(actions, action => action.Method == nameof(NotificationController.MarkAllAsRead)
            && action.Verbs.Contains("PATCH") && action.Template == "mark-all-read");
        Assert.Contains(actions, action => action.Method == nameof(NotificationController.MarkAsRead)
            && action.Verbs.Contains("PATCH") && action.Template == "{notificationId:int}/read");
        Assert.DoesNotContain(actions, action => action.Verbs.Contains("PUT")
            || action.Template?.Contains("read-all", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void AdminMentorVerificationUsesOnlyTheClientRoute()
    {
        var routes = typeof(AdminMentorController)
            .GetMethods()
            .Where(method => method.Name == "GetMentorProfiles")
            .SelectMany(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true)
                .Cast<HttpMethodAttribute>()
                .SelectMany(attribute => attribute.HttpMethods.Select(verb => (verb, attribute.Template))))
            .ToArray();

        Assert.Equal([("GET", "verifications")], routes);
    }

    [Fact]
    public void ControllersDoNotRegisterVersionedApiRoutes()
    {
        var routes = new List<(string Verb, string Path)>();
        foreach (var type in typeof(NotificationController).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(NotificationController).Namespace))
        {
            var controllerName = type.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? type.Name[..^"Controller".Length]
                : type.Name;
            var controllerRoutes = type.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
                .Cast<RouteAttribute>()
                .Select(route => route.Template.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (controllerRoutes.Length == 0) controllerRoutes = [string.Empty];

            foreach (var method in type.GetMethods())
            {
                foreach (var attribute in method
                    .GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true)
                    .Cast<HttpMethodAttribute>())
                {
                    var routePrefixes = attribute.Template?.StartsWith('/') == true ? [string.Empty] : controllerRoutes;
                    foreach (var verb in attribute.HttpMethods)
                    foreach (var prefix in routePrefixes)
                    {
                        var path = attribute.Template?.StartsWith('/') == true
                            ? attribute.Template.TrimStart('/')
                            : string.Join('/', new[] { prefix, attribute.Template }
                                .Where(part => !string.IsNullOrWhiteSpace(part)));
                        path = path.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase);
                        routes.Add((verb.ToUpperInvariant(), path));
                    }
                }
            }
        }

        Assert.DoesNotContain(routes, route => route.Path.Contains("api/v1", StringComparison.OrdinalIgnoreCase));
        var duplicateRoutes = routes.GroupBy(route => (route.Verb, route.Path.ToLowerInvariant()))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.Verb} {group.Key.Item2}")
            .ToArray();
        Assert.Empty(duplicateRoutes);
    }

    [Fact]
    public void OperationIdsAreStableAndDistinctForDifferentActions()
    {
        var cancel = CanonicalApiOperationFilter.BuildOperationId(
            "MentorBookingController", "POST", "api/mentor-bookings/{bookingId:int}/cancel");
        var release = CanonicalApiOperationFilter.BuildOperationId(
            "MentorBookingController", "POST", "api/learner/mentor-bookings/{bookingId:int}/release");

        Assert.NotEqual(cancel, release);
        Assert.DoesNotContain("/", cancel);
        Assert.DoesNotContain("{", cancel);
    }

    [Fact]
    public void FallbackSummariesUseVietnameseResourceDescriptions()
    {
        Assert.Equal(
            "Lấy thông tin nhóm chứng chỉ.",
            CanonicalApiOperationFilter.BuildFallbackSummary("GET", "api/admin/certificate-sections"));
        Assert.Equal(
            "Lấy thông tin hồ sơ Cố vấn.",
            CanonicalApiOperationFilter.BuildFallbackSummary("GET", "api/admin/mentors"));
    }

    [Fact]
    public async Task RuntimeSwaggerDocument_ExcludesLegacyApis_AndRetainsGroupLearningApis()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(AdminMentorGroupPolicyController).Assembly.GetName().Name
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddEndpointsApiExplorer();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(AdminMentorGroupPolicyController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "RikiPath API", Version = "v1" });
            c.DocumentFilter<CanonicalApiDocumentFilter>();
            c.OperationFilter<CanonicalApiOperationFilter>();
        });

        await using var app = builder.Build();
        app.UseSwagger();
        app.MapControllers();
        await app.StartAsync();

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            using var response = await http.GetAsync("/swagger/v1/swagger.json");
            response.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var paths = json.RootElement.GetProperty("paths")
                .EnumerateObject()
                .Select(p => p.Name)
                .ToArray();

            Assert.NotEmpty(paths);

            var forbiddenFragments = new[]
            {
                "mentor-booking",
                "mentor-plan",
                "mentor-chat",
                "payroll",
                "payout",
                "bank-account",
                "compensation",
                "credit-ledger",
                "dispute",
                "api/v1"
            };

            foreach (var fragment in forbiddenFragments)
            {
                Assert.DoesNotContain(
                    paths,
                    path => path.Contains(fragment, StringComparison.OrdinalIgnoreCase));
            }

            // Canonical Group Learning, Checkout, Homework PR #70, Attendance, and KPI endpoints remain present
            Assert.Contains(paths, p => p.StartsWith("/api/study-courses", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(paths, p => p.StartsWith("/api/homework", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(paths, p => p.StartsWith("/api/admin/mentor-group-policies", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(paths, p => p.Contains("kpi", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(paths, p => p.Contains("attendance", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(paths, p => p.Contains("payment", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
