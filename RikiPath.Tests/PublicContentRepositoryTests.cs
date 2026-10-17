using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Repositories;

namespace RikiPath.Tests;

public sealed class PublicContentRepositoryTests
{
    [Fact]
    public async Task Vocabulary_published_projection_appears_in_public_search()
    {
        await using var context = CreateContext();
        context.Vocabularies.Add(Vocabulary(910001, "visible-vocabulary"));
        await context.SaveChangesAsync();

        var (items, total) = await new VocabularyRepository(context)
            .SearchPublicAsync(null, "visible-vocabulary", 1, 20);

        Assert.Equal(1, total);
        Assert.Equal(910001, Assert.Single(items).Id);
    }

    [Fact]
    public async Task Vocabulary_draft_staging_row_does_not_appear_in_public_search()
        => await AssertStagedVocabularyHidden(ContentApprovalStatus.Draft, "draft-vocabulary");

    [Fact]
    public async Task Vocabulary_pending_review_staging_row_does_not_appear_in_public_search()
        => await AssertStagedVocabularyHidden(ContentApprovalStatus.PendingReview, "pending-vocabulary");

    [Fact]
    public async Task Vocabulary_rejected_staging_row_does_not_appear_in_public_search()
        => await AssertStagedVocabularyHidden(ContentApprovalStatus.Rejected, "rejected-vocabulary");

    [Fact]
    public async Task Vocabulary_archived_projection_is_soft_deleted_and_hidden()
    {
        await using var context = CreateContext();
        context.Vocabularies.Add(Vocabulary(910005, "archived-vocabulary", isDeleted: true));
        await context.SaveChangesAsync();

        var (items, total) = await new VocabularyRepository(context)
            .SearchPublicAsync(null, "archived-vocabulary", 1, 20);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task Vocabulary_deleted_row_does_not_appear_in_public_search()
    {
        await using var context = CreateContext();
        context.Vocabularies.Add(Vocabulary(910006, "deleted-vocabulary", isDeleted: true));
        await context.SaveChangesAsync();

        var (items, _) = await new VocabularyRepository(context)
            .SearchPublicAsync(null, "deleted-vocabulary", 1, 20);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Vocabulary_published_projection_detail_succeeds()
    {
        await using var context = CreateContext();
        context.Vocabularies.Add(Vocabulary(910007, "detail-vocabulary"));
        await context.SaveChangesAsync();

        var item = await new VocabularyRepository(context).GetPublicByIdAsync(910007);

        Assert.NotNull(item);
    }

    [Fact]
    public async Task Vocabulary_unmaterialized_hidden_detail_does_not_return_object()
    {
        await using var context = CreateContext();
        context.ContentApprovalRequests.Add(ApprovalRequest(
            919008, ImportEntityType.Vocabulary, ContentApprovalStatus.PendingReview, "hidden-detail"));
        await context.SaveChangesAsync();

        var item = await new VocabularyRepository(context).GetPublicByIdAsync(919008);

        Assert.Null(item);
    }

    [Fact]
    public async Task Vocabulary_deleted_detail_does_not_return_object()
    {
        await using var context = CreateContext();
        context.Vocabularies.Add(Vocabulary(910009, "deleted-detail", isDeleted: true));
        await context.SaveChangesAsync();

        Assert.Null(await new VocabularyRepository(context).GetPublicByIdAsync(910009));
    }

    [Fact]
    public async Task Kanji_published_projection_appears_in_public_search()
    {
        await using var context = CreateContext();
        context.Kanjis.Add(Kanji(920001, "可", "visible-kanji"));
        await context.SaveChangesAsync();

        var (items, total) = await new KanjiRepository(context)
            .SearchPublicAsync(null, "visible-kanji", null, null, 1, 20);

        Assert.Equal(1, total);
        Assert.Equal(920001, Assert.Single(items).Id);
    }

    [Fact]
    public async Task Kanji_non_published_staging_content_is_hidden()
    {
        await using var context = CreateContext();
        context.ContentApprovalRequests.Add(ApprovalRequest(
            929002, ImportEntityType.Kanji, ContentApprovalStatus.Draft, "未"));
        await context.SaveChangesAsync();

        var (items, total) = await new KanjiRepository(context)
            .SearchPublicAsync(null, "未", null, null, 1, 20);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task Kanji_deleted_content_is_hidden()
    {
        await using var context = CreateContext();
        context.Kanjis.Add(Kanji(920003, "削", "deleted-kanji", isDeleted: true));
        await context.SaveChangesAsync();

        var (items, _) = await new KanjiRepository(context)
            .SearchPublicAsync(null, "deleted-kanji", null, null, 1, 20);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Kanji_published_projection_detail_succeeds()
    {
        await using var context = CreateContext();
        context.Kanjis.Add(Kanji(920004, "公", "public-kanji"));
        await context.SaveChangesAsync();

        Assert.NotNull(await new KanjiRepository(context).GetPublicByIdAsync(920004));
    }

    [Fact]
    public async Task Kanji_hidden_detail_is_not_found()
    {
        await using var context = CreateContext();
        context.Kanjis.Add(Kanji(920005, "隠", "hidden-kanji", isDeleted: true));
        await context.SaveChangesAsync();

        Assert.Null(await new KanjiRepository(context).GetPublicByIdAsync(920005));
    }

    [Fact]
    public async Task Grammar_published_projection_appears_in_public_search()
    {
        await using var context = CreateContext();
        context.GrammarPatterns.Add(Grammar(930001, "visible-grammar"));
        await context.SaveChangesAsync();

        var (items, total) = await new GrammarPatternRepository(context)
            .SearchPublicAsync(null, "visible-grammar", 1, 20);

        Assert.Equal(1, total);
        Assert.Equal(930001, Assert.Single(items).Id);
    }

    [Fact]
    public async Task Grammar_non_published_staging_content_is_hidden()
    {
        await using var context = CreateContext();
        context.ContentApprovalRequests.Add(ApprovalRequest(
            939002, ImportEntityType.Grammar, ContentApprovalStatus.Rejected, "hidden-grammar"));
        await context.SaveChangesAsync();

        var (items, total) = await new GrammarPatternRepository(context)
            .SearchPublicAsync(null, "hidden-grammar", 1, 20);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task Grammar_deleted_content_is_hidden()
    {
        await using var context = CreateContext();
        context.GrammarPatterns.Add(Grammar(930003, "deleted-grammar", isDeleted: true));
        await context.SaveChangesAsync();

        var (items, _) = await new GrammarPatternRepository(context)
            .SearchPublicAsync(null, "deleted-grammar", 1, 20);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Grammar_published_projection_detail_succeeds()
    {
        await using var context = CreateContext();
        context.GrammarPatterns.Add(Grammar(930004, "public-grammar"));
        await context.SaveChangesAsync();

        Assert.NotNull(await new GrammarPatternRepository(context).GetPublicByIdAsync(930004));
    }

    [Fact]
    public async Task Grammar_hidden_detail_is_not_found()
    {
        await using var context = CreateContext();
        context.GrammarPatterns.Add(Grammar(930005, "hidden-grammar", isDeleted: true));
        await context.SaveChangesAsync();

        Assert.Null(await new GrammarPatternRepository(context).GetPublicByIdAsync(930005));
    }

    [Fact]
    public async Task Administrative_lookup_remains_unfiltered_for_non_public_content()
    {
        await using var context = CreateContext();
        context.Vocabularies.Add(Vocabulary(940001, "admin-visible", isDeleted: true));
        await context.SaveChangesAsync();

        var repository = new VocabularyRepository(context);

        Assert.NotNull(await repository.GetByIdAsync(940001));
        Assert.Null(await repository.GetPublicByIdAsync(940001));
    }

    private static async Task AssertStagedVocabularyHidden(ContentApprovalStatus status, string rowKey)
    {
        await using var context = CreateContext();
        context.ContentApprovalRequests.Add(ApprovalRequest(
            950000 + (int)status, ImportEntityType.Vocabulary, status, rowKey));
        await context.SaveChangesAsync();

        var (items, total) = await new VocabularyRepository(context)
            .SearchPublicAsync(null, rowKey, 1, 20);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Vocabulary Vocabulary(int id, string word, bool isDeleted = false) => new()
    {
        Id = id,
        Word = word,
        Reading = "reading",
        Meaning = word,
        IsDeleted = isDeleted,
    };

    private static Kanji Kanji(int id, string character, string meaning, bool isDeleted = false) => new()
    {
        Id = id,
        Character = character,
        Meaning = meaning,
        StrokeCount = 1,
        IsDeleted = isDeleted,
    };

    private static GrammarPattern Grammar(int id, string title, bool isDeleted = false) => new()
    {
        Id = id,
        Title = title,
        Structure = title,
        IsDeleted = isDeleted,
    };

    private static ContentApprovalRequest ApprovalRequest(
        int id,
        ImportEntityType entityType,
        ContentApprovalStatus status,
        string rowKey) => new()
    {
        Id = id,
        BatchId = Guid.NewGuid(),
        EntityType = entityType,
        Action = ImportRowAction.New,
        RowKey = rowKey,
        PayloadJson = "{}",
        SourceRowNumber = 1,
        SourceFileName = "isolated-test.xlsx",
        Status = status,
        SubmittedByUserId = 999999,
        SubmittedAt = DateTime.UtcNow,
    };
}
