using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Notebook;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class NotebookMutationConsistencyTests
{
    private const int OwnerId = 970001;
    private const int OtherLearnerId = 970002;
    private const int OwnerListId = 970010;
    private const int OtherListId = 970011;

    [Fact]
    public async Task Owner_creates_entry_and_review_card_in_one_consistent_mutation()
    {
        await using var context = CreateContext(new NotebookMutationSaveInterceptor());
        await SeedNotebookAsync(context);
        context.ChangeTracker.Clear();
        var service = CreateService(context, OwnerId);

        var result = await service.AddManualEntryAsync(CreateManualRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.True(result.Result!.NoteEntryId > 0);
        Assert.True(result.Result.ReviewCardId > 0);
        Assert.Single(await context.LearnerNoteEntries.AsNoTracking().ToListAsync());
        Assert.Single(await context.ReviewCards.AsNoTracking().ToListAsync());

        var readResult = await service.GetListEntriesAsync(OwnerListId, CancellationToken.None);
        Assert.True(readResult.IsSuccess);
        Assert.Single(readResult.Result!);
        Assert.Equal(result.Result.NoteEntryId, readResult.Result![0].NoteEntryId);
    }

    [Fact]
    public async Task Cross_user_learner_note_is_rejected_before_create()
    {
        await using var context = CreateContext(new NotebookMutationSaveInterceptor());
        await SeedNotebookAsync(context);
        context.ChangeTracker.Clear();

        var request = CreateManualRequest();
        request.LearnerNoteId = OtherListId;
        var result = await CreateService(context, OwnerId)
            .AddManualEntryAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Empty(await context.LearnerNoteEntries.AsNoTracking().ToListAsync());
        Assert.Empty(await context.ReviewCards.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Invalid_learner_note_is_controlled_before_create()
    {
        await using var context = CreateContext(new NotebookMutationSaveInterceptor());
        await SeedNotebookAsync(context);
        context.ChangeTracker.Clear();

        var request = CreateManualRequest();
        request.LearnerNoteId = 999999;
        var result = await CreateService(context, OwnerId)
            .AddManualEntryAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        Assert.Empty(await context.LearnerNoteEntries.AsNoTracking().ToListAsync());
        Assert.Empty(await context.ReviewCards.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Identical_sequential_retry_returns_existing_entry_without_duplicate()
    {
        await using var context = CreateContext(new NotebookMutationSaveInterceptor());
        await SeedNotebookAsync(context);
        context.ChangeTracker.Clear();
        var service = CreateService(context, OwnerId);

        var first = await service.AddManualEntryAsync(CreateManualRequest(), CancellationToken.None);
        context.ChangeTracker.Clear();
        var retry = await service.AddManualEntryAsync(CreateManualRequest(), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Created, first.StatusCode);
        Assert.True(retry.IsSuccess);
        Assert.Equal((int)HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(first.Result!.NoteEntryId, retry.Result!.NoteEntryId);
        Assert.Equal(first.Result.ReviewCardId, retry.Result.ReviewCardId);
        Assert.Single(await context.LearnerNoteEntries.AsNoTracking().ToListAsync());
        Assert.Single(await context.ReviewCards.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Failed_review_card_write_does_not_leave_partial_manual_entry()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        var interceptor = new NotebookMutationSaveInterceptor();

        await using (var context = CreateContext(interceptor, databaseName, databaseRoot))
        {
            await SeedNotebookAsync(context);
            context.ChangeTracker.Clear();
            interceptor.ThrowWhenReviewCardIsAdded = true;

            var service = new NotebookService(new UnitOfWork(context), CreateClaims(OwnerId).Object);
            var result = await service.AddManualEntryAsync(
                new AddManualEntryRequest
                {
                    LearnerNoteId = OwnerListId,
                    Word = "atomic",
                    Meaning = "nguyên tử",
                },
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal((int)HttpStatusCode.InternalServerError, result.StatusCode);
            Assert.Null(result.Errors);
        }

        await using var verificationContext = CreateContext(
            new NotebookMutationSaveInterceptor(),
            databaseName,
            databaseRoot);
        Assert.Empty(await verificationContext.LearnerNoteEntries.AsNoTracking().ToListAsync());
        Assert.Empty(await verificationContext.ReviewCards.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Owner_deletes_entry_and_review_card_together()
    {
        await using var context = CreateContext(new NotebookMutationSaveInterceptor());
        await SeedNotebookAsync(context);
        var entryId = await SeedEntryWithReviewCardAsync(context, OwnerId, OwnerListId);
        context.ChangeTracker.Clear();

        var result = await CreateService(context, OwnerId)
            .RemoveEntryAsync(entryId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Empty(await context.LearnerNoteEntries.AsNoTracking().ToListAsync());
        Assert.Empty(await context.ReviewCards.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Cross_user_delete_is_rejected_without_mutation()
    {
        await using var context = CreateContext(new NotebookMutationSaveInterceptor());
        await SeedNotebookAsync(context);
        var entryId = await SeedEntryWithReviewCardAsync(context, OtherLearnerId, OtherListId);
        context.ChangeTracker.Clear();

        var result = await CreateService(context, OwnerId)
            .RemoveEntryAsync(entryId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Single(await context.LearnerNoteEntries.AsNoTracking().ToListAsync());
        Assert.Single(await context.ReviewCards.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Missing_entry_delete_is_controlled()
    {
        await using var context = CreateContext(new NotebookMutationSaveInterceptor());
        await SeedNotebookAsync(context);
        context.ChangeTracker.Clear();

        var result = await CreateService(context, OwnerId)
            .RemoveEntryAsync(999999, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        Assert.Null(result.Errors);
    }

    [Fact]
    public async Task Already_deleted_retry_returns_controlled_not_found()
    {
        await using var context = CreateContext(new NotebookMutationSaveInterceptor());
        await SeedNotebookAsync(context);
        var entryId = await SeedEntryWithReviewCardAsync(context, OwnerId, OwnerListId);
        context.ChangeTracker.Clear();
        var service = CreateService(context, OwnerId);

        var first = await service.RemoveEntryAsync(entryId, CancellationToken.None);
        context.ChangeTracker.Clear();
        var retry = await service.RemoveEntryAsync(entryId, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(retry.IsSuccess);
        Assert.Equal((int)HttpStatusCode.NotFound, retry.StatusCode);
        Assert.Null(retry.Errors);
    }

    [Fact]
    public async Task Failed_delete_does_not_half_delete_entry_or_review_card()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        var interceptor = new NotebookMutationSaveInterceptor();

        int entryId;
        await using (var context = CreateContext(interceptor, databaseName, databaseRoot))
        {
            await SeedNotebookAsync(context);
            entryId = await SeedEntryWithReviewCardAsync(context, OwnerId, OwnerListId);
            context.ChangeTracker.Clear();
            interceptor.ThrowWhenEntryOrReviewCardIsDeleted = true;

            var result = await CreateService(context, OwnerId)
                .RemoveEntryAsync(entryId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal((int)HttpStatusCode.InternalServerError, result.StatusCode);
            Assert.Null(result.Errors);
        }

        await using var verificationContext = CreateContext(
            new NotebookMutationSaveInterceptor(),
            databaseName,
            databaseRoot);
        Assert.Single(await verificationContext.LearnerNoteEntries.AsNoTracking().ToListAsync());
        Assert.Single(await verificationContext.ReviewCards.AsNoTracking().ToListAsync());
    }

    private static AppDbContext CreateContext(
        NotebookMutationSaveInterceptor interceptor,
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

    private static async Task SeedNotebookAsync(AppDbContext context)
    {
        context.Users.AddRange(
            CreateLearner(OwnerId, "notebook-owner@local.invalid"),
            CreateLearner(OtherLearnerId, "notebook-other@local.invalid"));
        context.LearnerNotes.AddRange(
            new LearnerNote
            {
                Id = OwnerListId,
                UserId = OwnerId,
                Name = "Owner notebook",
                CreatedDate = DateTime.UtcNow,
            },
            new LearnerNote
            {
                Id = OtherListId,
                UserId = OtherLearnerId,
                Name = "Other notebook",
                CreatedDate = DateTime.UtcNow,
            });
        await context.SaveChangesAsync();
    }

    private static async Task<int> SeedEntryWithReviewCardAsync(
        AppDbContext context,
        int userId,
        int learnerNoteId)
    {
        var entry = new LearnerNoteEntry
        {
            LearnerNoteId = learnerNoteId,
            ManualWord = "delete",
            ManualMeaning = "xóa",
            CreatedDate = DateTime.UtcNow,
        };
        context.LearnerNoteEntries.Add(entry);
        await context.SaveChangesAsync();

        context.ReviewCards.Add(new ReviewCard
        {
            UserId = userId,
            LearnerNoteEntryId = entry.Id,
            Repetitions = 0,
            EaseFactor = 2.5,
            IntervalDays = 0,
            NextReviewDate = DateTime.UtcNow.Date,
        });
        await context.SaveChangesAsync();
        return entry.Id;
    }

    private static AddManualEntryRequest CreateManualRequest() => new()
    {
        LearnerNoteId = OwnerListId,
        Word = " atomic ",
        Reading = " atomikku ",
        Meaning = " nguyên tử ",
        Note = "same logical request",
    };

    private static NotebookService CreateService(AppDbContext context, int userId)
        => new(new UnitOfWork(context), CreateClaims(userId).Object);

    private static UserAccount CreateLearner(int id, string email) => new()
    {
        Id = id,
        Email = email,
        FirstName = "Notebook",
        LastName = "Test",
        PasswordHash = [1],
        PasswordSalt = [2],
        Role = Role.Learner,
        IsEmailVerified = true,
    };

    private static Mock<IClaimService> CreateClaims(int userId)
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO
        {
            Id = userId,
            Role = Role.Learner,
        });
        return claims;
    }

    private sealed class NotebookMutationSaveInterceptor : SaveChangesInterceptor
    {
        public bool ThrowWhenReviewCardIsAdded { get; set; }
        public bool ThrowWhenEntryOrReviewCardIsDeleted { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (ThrowWhenReviewCardIsAdded &&
                eventData.Context?.ChangeTracker.Entries<ReviewCard>()
                    .Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new InvalidOperationException("Simulated dependent ReviewCard write failure.");
            }

            if (ThrowWhenEntryOrReviewCardIsDeleted &&
                eventData.Context?.ChangeTracker.Entries()
                    .Any(entry =>
                        entry.State == EntityState.Deleted &&
                        (entry.Entity is LearnerNoteEntry || entry.Entity is ReviewCard)) == true)
            {
                throw new InvalidOperationException("Simulated atomic delete failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
