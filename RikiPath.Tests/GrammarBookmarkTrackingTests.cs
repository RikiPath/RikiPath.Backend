using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class GrammarBookmarkTrackingTests
{
    private const int LearnerId = 980001;
    private const int GrammarId = 980002;
    private const int MappingId = 980003;
    private const int CertificateLevelId = 980004;

    [Fact]
    public async Task Public_grammar_bookmark_uses_only_scalar_fk_for_existing_graph()
    {
        var interceptor = new ReviewCardSaveInterceptor();
        await using var context = CreateContext(interceptor);
        await SeedPublicGrammarAsync(context);
        interceptor.CaptureReviewCardSave = true;

        var unitOfWork = new UnitOfWork(context);
        var service = new NotebookService(unitOfWork, CreateClaims().Object);

        var result = await service.BookmarkGrammarAsync(GrammarId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.Equal(GrammarId, result.Result.GrammarPatternId);
        Assert.True(result.Result.ReviewCardId > 0);

        var reviewCard = await context.ReviewCards.AsNoTracking().SingleAsync();
        Assert.Equal(LearnerId, reviewCard.UserId);
        Assert.Equal(GrammarId, reviewCard.GrammarPatternId);
        Assert.Null(reviewCard.GrammarPattern);
        Assert.Equal(1, await context.GrammarPatterns.CountAsync());
        Assert.Equal(1, await context.ContentLevelMappings.CountAsync());
        Assert.Equal(1, await context.CertificateLevels.CountAsync());

        Assert.True(interceptor.ReviewCardWasAdded);
        Assert.False(interceptor.GrammarWasAdded);
        Assert.False(interceptor.MappingWasAdded);
        Assert.False(interceptor.CertificateLevelWasAdded);

        var detail = await new SharedBankSearchService(unitOfWork)
            .GetGrammarDetailAsync(GrammarId, CancellationToken.None);
        Assert.True(detail.IsSuccess);
        Assert.Equal(GrammarId, detail.Result?.Id);
    }

    [Fact]
    public async Task Hidden_grammar_remains_rejected_without_creating_review_card()
    {
        var interceptor = new ReviewCardSaveInterceptor();
        await using var context = CreateContext(interceptor);
        await SeedPublicGrammarAsync(context, isDeleted: true);
        interceptor.CaptureReviewCardSave = true;

        var result = await new NotebookService(new UnitOfWork(context), CreateClaims().Object)
            .BookmarkGrammarAsync(GrammarId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        Assert.Empty(await context.ReviewCards.AsNoTracking().ToListAsync());
        Assert.Equal(0, interceptor.ReviewCardSaveCalls);
    }

    [Fact]
    public async Task Invalid_grammar_id_is_controlled_without_creating_review_card()
    {
        var interceptor = new ReviewCardSaveInterceptor();
        await using var context = CreateContext(interceptor);
        await SeedLearnerAsync(context);
        interceptor.CaptureReviewCardSave = true;

        var result = await new NotebookService(new UnitOfWork(context), CreateClaims().Object)
            .BookmarkGrammarAsync(999999, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        Assert.Empty(await context.ReviewCards.AsNoTracking().ToListAsync());
        Assert.Equal(0, interceptor.ReviewCardSaveCalls);
    }

    [Fact]
    public async Task Failed_save_does_not_persist_review_card()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        var interceptor = new ReviewCardSaveInterceptor();
        await using (var context = CreateContext(interceptor, databaseName, databaseRoot))
        {
            await SeedPublicGrammarAsync(context);
            interceptor.CaptureReviewCardSave = true;
            interceptor.ThrowOnReviewCardSave = true;

            var result = await new NotebookService(new UnitOfWork(context), CreateClaims().Object)
                .BookmarkGrammarAsync(GrammarId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal((int)HttpStatusCode.InternalServerError, result.StatusCode);
            Assert.Equal(1, interceptor.ReviewCardSaveCalls);
        }

        await using var verificationContext = CreateContext(
            new ReviewCardSaveInterceptor(),
            databaseName,
            databaseRoot);
        Assert.Empty(await verificationContext.ReviewCards.AsNoTracking().ToListAsync());
        Assert.Equal(1, await verificationContext.GrammarPatterns.CountAsync());
        Assert.Equal(1, await verificationContext.ContentLevelMappings.CountAsync());
        Assert.Equal(1, await verificationContext.CertificateLevels.CountAsync());
    }

    private static AppDbContext CreateContext(
        ReviewCardSaveInterceptor interceptor,
        string? databaseName = null,
        InMemoryDatabaseRoot? databaseRoot = null)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        if (databaseRoot is null)
            builder.UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString());
        else
            builder.UseInMemoryDatabase(databaseName!, databaseRoot);

        builder.AddInterceptors(interceptor);
        return new AppDbContext(builder.Options);
    }

    private static async Task SeedPublicGrammarAsync(AppDbContext context, bool isDeleted = false)
    {
        await SeedLearnerAsync(context);
        var grammar = new GrammarPattern
        {
            Id = GrammarId,
            Title = "～てみる",
            Structure = "Vて + みる",
            IsDeleted = isDeleted,
        };
        var certificateLevel = new CertificateLevel
        {
            Id = CertificateLevelId,
            CertificateTypeId = 1,
            Code = "N5",
            SortOrder = 1,
        };
        var mapping = new ContentLevelMapping
        {
            Id = MappingId,
            GrammarPatternId = GrammarId,
            GrammarPattern = grammar,
            CertificateLevelId = CertificateLevelId,
            CertificateLevel = certificateLevel,
        };
        grammar.ContentLevelMappings = [mapping];

        context.GrammarPatterns.Add(grammar);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task SeedLearnerAsync(AppDbContext context)
    {
        context.Users.Add(new UserAccount
        {
            Id = LearnerId,
            Email = "grammar-bookmark-test@local.invalid",
            FirstName = "Grammar",
            LastName = "Bookmark",
            PasswordHash = [1],
            PasswordSalt = [2],
            Role = Role.Learner,
            IsEmailVerified = true,
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static Mock<IClaimService> CreateClaims()
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO
        {
            Id = LearnerId,
            Role = Role.Learner,
        });
        return claims;
    }

    private sealed class ReviewCardSaveInterceptor : SaveChangesInterceptor
    {
        public bool CaptureReviewCardSave { get; set; }
        public bool ThrowOnReviewCardSave { get; set; }
        public int ReviewCardSaveCalls { get; private set; }
        public bool ReviewCardWasAdded { get; private set; }
        public bool GrammarWasAdded { get; private set; }
        public bool MappingWasAdded { get; private set; }
        public bool CertificateLevelWasAdded { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!CaptureReviewCardSave || eventData.Context is null)
                return base.SavingChangesAsync(eventData, result, cancellationToken);

            var entries = eventData.Context.ChangeTracker.Entries().ToList();
            ReviewCardWasAdded = entries.Any(x => x.Entity is ReviewCard && x.State == EntityState.Added);
            if (!ReviewCardWasAdded)
                return base.SavingChangesAsync(eventData, result, cancellationToken);

            ReviewCardSaveCalls++;
            GrammarWasAdded = entries.Any(x => x.Entity is GrammarPattern && x.State == EntityState.Added);
            MappingWasAdded = entries.Any(x => x.Entity is ContentLevelMapping && x.State == EntityState.Added);
            CertificateLevelWasAdded = entries.Any(x => x.Entity is CertificateLevel && x.State == EntityState.Added);

            if (ThrowOnReviewCardSave)
                throw new InvalidOperationException("Simulated atomic SaveChanges failure.");

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
