using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IRepositories;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Repositories;

namespace RikiPath.Tests;

public sealed class JlptCatalogTests
{
    [Fact]
    public async Task Learner_catalog_returns_real_ids_in_deterministic_order()
    {
        await using var context = CreateContext();
        var jlpt = CertificateType(100, "JLPT");
        context.CertificateTypes.Add(jlpt);
        context.CertificateLevels.AddRange(
            Level(950003, jlpt, "N3", sortOrder: 3),
            Level(950001, jlpt, "N5", sortOrder: 1),
            Level(950002, jlpt, "N4", sortOrder: 2),
            Level(950005, jlpt, "N1", sortOrder: 5),
            Level(950004, jlpt, "N2", sortOrder: 4));
        await context.SaveChangesAsync();

        var result = await new CertificateLevelRepository(context)
            .GetLearnerVisibleJlptLevelsAsync();

        Assert.Equal(
            new[] { 950001, 950002, 950003, 950004, 950005 },
            result.Select(level => level.Id));
        Assert.Equal(
            new[] { "N5", "N4", "N3", "N2", "N1" },
            result.Select(level => level.Code));
    }

    [Fact]
    public async Task Learner_catalog_excludes_deleted_levels_and_unavailable_certificate_types()
    {
        await using var context = CreateContext();
        var jlpt = CertificateType(110, "JLPT");
        var inactiveJlpt = CertificateType(111, "JLPT", isActive: false);
        var deletedJlpt = CertificateType(112, "JLPT", isDeleted: true);
        var other = CertificateType(113, "NAT-TEST");
        context.CertificateTypes.AddRange(jlpt, inactiveJlpt, deletedJlpt, other);
        context.CertificateLevels.AddRange(
            Level(951001, jlpt, "N5", sortOrder: 1),
            Level(951002, jlpt, "N4", sortOrder: 2, isDeleted: true),
            Level(951003, inactiveJlpt, "N3", sortOrder: 3),
            Level(951004, deletedJlpt, "N2", sortOrder: 4),
            Level(951005, other, "5Q", sortOrder: 1));
        await context.SaveChangesAsync();

        var result = await new CertificateLevelRepository(context)
            .GetLearnerVisibleJlptLevelsAsync();

        Assert.Equal(951001, Assert.Single(result).Id);
    }

    [Fact]
    public async Task Learner_catalog_uses_id_as_stable_tie_breaker()
    {
        await using var context = CreateContext();
        var jlpt = CertificateType(120, "JLPT");
        context.CertificateTypes.Add(jlpt);
        context.CertificateLevels.AddRange(
            Level(952002, jlpt, "N4", sortOrder: 1),
            Level(952001, jlpt, "N5", sortOrder: 1));
        await context.SaveChangesAsync();

        var result = await new CertificateLevelRepository(context)
            .GetLearnerVisibleJlptLevelsAsync();

        Assert.Equal(new[] { 952001, 952002 }, result.Select(level => level.Id));
    }

    [Fact]
    public async Task Service_returns_controlled_empty_catalog()
    {
        var repository = new Mock<ICertificateLevelRepository>();
        repository
            .Setup(x => x.GetLearnerVisibleJlptLevelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CertificateLevel>());
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.CertificateLevels).Returns(repository.Object);

        var result = await new JlptCatalogService(unitOfWork.Object).GetLevelsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task Service_preserves_nullable_metadata_without_failing()
    {
        var repository = new Mock<ICertificateLevelRepository>();
        repository
            .Setup(x => x.GetLearnerVisibleJlptLevelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CertificateLevel
                {
                    Id = 953007,
                    CertificateTypeId = 130,
                    Code = "N5",
                    Description = null,
                    SortOrder = 7,
                },
            });
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.CertificateLevels).Returns(repository.Object);

        var result = await new JlptCatalogService(unitOfWork.Object).GetLevelsAsync();

        var level = Assert.Single(result.Result!);
        Assert.Equal(953007, level.Id);
        Assert.Equal("N5", level.Code);
        Assert.Null(level.Description);
        Assert.Equal(7, level.SortOrder);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CertificateType CertificateType(
        int id,
        string name,
        bool isActive = true,
        bool isDeleted = false) => new()
    {
        Id = id,
        Name = name,
        IsActive = isActive,
        SortOrder = id,
        IsDeleted = isDeleted,
    };

    private static CertificateLevel Level(
        int id,
        CertificateType certificateType,
        string code,
        int sortOrder,
        bool isDeleted = false) => new()
    {
        Id = id,
        CertificateTypeId = certificateType.Id,
        CertificateType = certificateType,
        Code = code,
        Description = $"JLPT {code}",
        SortOrder = sortOrder,
        IsDeleted = isDeleted,
    };
}
