using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RikiPath.Application.Requests.CertificateSections;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Infrastructure;
using RikiPath.WebApi.Controllers;

namespace RikiPath.Tests;

public sealed class CertificateSectionManagementTests
{
    [Fact]
    public void Controller_is_admin_only_and_uses_admin_route()
    {
        var controllerType = typeof(AdminCertificateSectionsController);
        var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var route = controllerType.GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize!.Roles);
        Assert.Equal("api/admin/certificate-sections", route!.Template);
    }

    [Fact]
    public async Task List_filters_archived_rows_and_orders_by_level_then_sort_order()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.CreateAsync("SECOND", sortOrder: 2);
        await fixture.CreateAsync("FIRST", sortOrder: 1);
        var archived = await fixture.CreateAsync("ARCHIVED", sortOrder: 3);
        await fixture.Service.ArchiveAsync(archived.Result!.Id, CancellationToken.None);

        var active = await fixture.Service.GetAllAsync(1, false, CancellationToken.None);
        var all = await fixture.Service.GetAllAsync(1, true, CancellationToken.None);

        Assert.True(active.IsSuccess);
        Assert.Equal(["FIRST", "SECOND"], active.Result!
            .Where(section => section.Code is "FIRST" or "SECOND")
            .Select(section => section.Code));
        Assert.Equal(5, active.Result.Count); // three N5 seeds plus two created here
        Assert.Equal(6, all.Result!.Count);
        Assert.True(all.Result.Single(section => section.Code == "ARCHIVED").IsArchived);
    }

    [Fact]
    public async Task Create_accepts_valid_section_and_preserves_nullable_description()
    {
        await using var fixture = await CreateFixtureAsync();
        var request = ValidCreateRequest("READING");
        request.Description = "   ";

        var result = await fixture.Service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Equal(1, result.Result!.CertificateLevelId);
        Assert.Equal("N5", result.Result.CertificateLevelCode);
        Assert.Null(result.Result.Description);
        Assert.Equal(8, await fixture.Context.CertificateSections.CountAsync());
        Assert.Single(await fixture.Context.CertificateSections.Where(section => section.Code == "READING").ToListAsync());
    }

    [Theory]
    [InlineData(0, 60, 19, 1)]
    [InlineData(10, 0, 0, 1)]
    [InlineData(10, 60, -1, 1)]
    [InlineData(10, 60, 61, 1)]
    [InlineData(10, 60, 19, 0)]
    public async Task Create_rejects_invalid_numeric_values(
        int durationMinutes,
        int maxScore,
        int passingScore,
        int sortOrder)
    {
        await using var fixture = await CreateFixtureAsync();
        var request = ValidCreateRequest("INVALID");
        request.DurationMinutes = durationMinutes;
        request.MaxScore = maxScore;
        request.PassingScore = passingScore;
        request.SortOrder = sortOrder;

        var result = await fixture.Service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(7, await fixture.Context.CertificateSections.CountAsync());
        Assert.Empty(await fixture.Context.CertificateSections.Where(section => section.Code == "INVALID").ToListAsync());
    }

    [Fact]
    public async Task Create_rejects_missing_deleted_and_non_jlpt_levels()
    {
        await using var fixture = await CreateFixtureAsync();
        var missing = await fixture.Service.CreateAsync(
            ValidCreateRequest("MISSING", certificateLevelId: 999),
            CancellationToken.None);

        var deletedLevel = await fixture.Context.CertificateLevels.SingleAsync(level => level.Id == 1);
        deletedLevel.IsDeleted = true;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var deleted = await fixture.Service.CreateAsync(
            ValidCreateRequest("DELETED"),
            CancellationToken.None);

        var otherType = new CertificateType
        {
            Id = 20,
            Name = "OTHER",
            IsActive = true,
            SortOrder = 1,
        };
        fixture.Context.CertificateTypes.Add(otherType);
        fixture.Context.CertificateLevels.Add(new CertificateLevel
        {
            Id = 20,
            CertificateTypeId = otherType.Id,
            Code = "L1",
            SortOrder = 1,
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var nonJlpt = await fixture.Service.CreateAsync(
            ValidCreateRequest("OTHER", certificateLevelId: 20),
            CancellationToken.None);

        Assert.All([missing, deleted, nonJlpt], result =>
        {
            Assert.False(result.IsSuccess);
            Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        });
        Assert.Equal(7, await fixture.Context.CertificateSections.CountAsync());
        Assert.Empty(await fixture.Context.CertificateSections
            .Where(section => section.Code == "MISSING" || section.Code == "DELETED" || section.Code == "OTHER")
            .ToListAsync());
    }

    [Fact]
    public async Task Update_changes_section_and_missing_section_returns_not_found()
    {
        await using var fixture = await CreateFixtureAsync();
        var created = await fixture.CreateAsync("OLD", sortOrder: 2);
        var request = new UpdateCertificateSectionRequest
        {
            CertificateLevelId = 1,
            Name = "Updated section",
            Code = "UPDATED",
            DurationMinutes = 30,
            MaxScore = 60,
            PassingScore = 20,
            SortOrder = 1,
        };

        var updated = await fixture.Service.UpdateAsync(
            created.Result!.Id,
            request,
            CancellationToken.None);
        var missing = await fixture.Service.UpdateAsync(999, request, CancellationToken.None);

        Assert.True(updated.IsSuccess);
        Assert.Equal("UPDATED", updated.Result!.Code);
        Assert.Equal(1, updated.Result.SortOrder);
        Assert.Equal((int)HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Archive_is_idempotent_and_preserves_mock_test_reference()
    {
        await using var fixture = await CreateFixtureAsync();
        var created = await fixture.CreateAsync("READING", sortOrder: 1);
        var sectionId = created.Result!.Id;
        fixture.Context.MockTests.Add(new MockTest
        {
            Id = 50,
            Title = "QA mock test",
            TimeLimitMinutes = 60,
            CertificateLevelId = 1,
        });
        fixture.Context.MockTestSections.Add(new MockTestSection
        {
            Id = 60,
            Title = "Reading",
            SortOrder = 1,
            MockTestId = 50,
            LanguageSkillId = 5,
            CertificateSectionId = sectionId,
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var first = await fixture.Service.ArchiveAsync(sectionId, CancellationToken.None);
        var modifiedAt = first.Result!.ModifiedDate;
        var second = await fixture.Service.ArchiveAsync(sectionId, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(second.Result!.IsArchived);
        Assert.Equal(modifiedAt, second.Result.ModifiedDate);
        Assert.Equal(sectionId, (await fixture.Context.MockTestSections.SingleAsync()).CertificateSectionId);
    }

    [Fact]
    public async Task Missing_detail_and_archive_return_not_found()
    {
        await using var fixture = await CreateFixtureAsync();

        var detail = await fixture.Service.GetByIdAsync(999, CancellationToken.None);
        var archive = await fixture.Service.ArchiveAsync(999, CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal((int)HttpStatusCode.NotFound, archive.StatusCode);
    }

    [Fact]
    public async Task Certificate_level_code_index_is_not_unique_in_current_model()
    {
        await using var fixture = await CreateFixtureAsync();
        var entityType = fixture.Context.Model.FindEntityType(typeof(CertificateSection));
        var properties = new[]
        {
            entityType!.FindProperty(nameof(CertificateSection.CertificateLevelId))!,
            entityType.FindProperty(nameof(CertificateSection.Code))!,
        };
        var index = entityType.FindIndex(properties);

        Assert.NotNull(index);
        Assert.False(index!.IsUnique);
    }

    private static CreateCertificateSectionRequest ValidCreateRequest(
        string code,
        int certificateLevelId = 1,
        int sortOrder = 1) => new()
        {
            CertificateLevelId = certificateLevelId,
            Name = $"Section {code}",
            Code = code,
            Description = "Description",
            DurationMinutes = 25,
            MaxScore = 60,
            PassingScore = 19,
            SortOrder = sortOrder,
        };

    private static async Task<CertificateSectionFixture> CreateFixtureAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"certificate-section-{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        Assert.Equal(7, await context.CertificateSections.CountAsync());
        var service = new AdminCertificateSectionService(
            new UnitOfWork(context),
            NullLogger<AdminCertificateSectionService>.Instance);
        return new CertificateSectionFixture(context, service);
    }

    private sealed class CertificateSectionFixture(
        AppDbContext context,
        AdminCertificateSectionService service) : IAsyncDisposable
    {
        public AppDbContext Context { get; } = context;
        public AdminCertificateSectionService Service { get; } = service;

        public Task<RikiPath.Application.Responses.ApiResponse<
            RikiPath.Application.Responses.CertificateSections.CertificateSectionResponse>> CreateAsync(
            string code,
            int sortOrder)
            => Service.CreateAsync(
                ValidCreateRequest(code, sortOrder: sortOrder),
                CancellationToken.None);

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
