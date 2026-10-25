using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application.IRepositories;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Repositories;
using RikiPath.WebApi.Controllers;

namespace RikiPath.Tests;

public sealed class JapaneseDictionaryTests
{
    [Theory]
    [InlineData("き")]
    [InlineData("キ")]
    [InlineData("ｷ")]
    public async Task Search_matches_admin_kun_reading_and_returns_existing_character(string query)
    {
        await using var db = Context();
        db.Kanjis.Add(Kanji(1, "木", "モク、ボク", "き / こ"));
        await db.SaveChangesAsync();
        var response = await Service(db).SearchAsync(query);
        var candidate = Assert.Single(response.Result!);
        Assert.Equal("木", candidate.Surface);
        Assert.Equal("き", candidate.ReadingKana);
        Assert.Equal("Mộc", candidate.SinoVietnamese);
        Assert.Equal("Cây; gỗ", candidate.Meaning);
    }

    [Fact]
    public async Task Exact_reading_precedes_prefix_and_duplicate_readings_are_collapsed()
    {
        await using var db = Context();
        db.Kanjis.AddRange(Kanji(2, "金", "キン", null), Kanji(1, "木", "キ, き", "き"));
        await db.SaveChangesAsync();
        var response = await Service(db).SearchAsync("き");
        Assert.Equal(new[] { "木", "金" }, response.Result!.Select(x => x.Surface));
    }

    [Theory]
    [InlineData("た", "食", "た")]
    [InlineData("たべる", "食べる", "たべる")]
    public async Task Dotted_kun_reading_preserves_okurigana(string query, string surface, string reading)
    {
        await using var db = Context();
        db.Kanjis.Add(Kanji(1, "食", "ショク", "た.べる；く.う"));
        await db.SaveChangesAsync();
        var candidate = (await Service(db).SearchAsync(query)).Result!.First();
        Assert.Equal(surface, candidate.Surface);
        Assert.Equal(reading, candidate.ReadingKana);
    }

    [Fact]
    public async Task Deleted_or_missing_readings_are_not_suggestions()
    {
        await using var db = Context();
        var deleted = Kanji(1, "木", "モク", "き");
        deleted.IsDeleted = true;
        db.Kanjis.AddRange(deleted, Kanji(2, "金", null, null));
        await db.SaveChangesAsync();
        Assert.Empty((await Service(db).SearchAsync("き")).Result!);
    }

    [Fact]
    public async Task Admin_addition_is_available_on_next_search_without_writing_progress()
    {
        await using var db = Context();
        var service = Service(db);
        Assert.Empty((await service.SearchAsync("き")).Result!);
        db.Kanjis.Add(Kanji(1, "木", "モク", "き"));
        await db.SaveChangesAsync();
        Assert.Single((await service.SearchAsync("き")).Result!);
    }

    [Fact]
    public async Task Results_are_limited_to_twenty_unique_surfaces()
    {
        await using var db = Context();
        db.Kanjis.AddRange(Enumerable.Range(1, 30).Select(i => Kanji(i, $"字{i}", "ジ", null)));
        await db.SaveChangesAsync();
        var matches = (await Service(db).SearchAsync("じ")).Result!;
        Assert.Equal(20, matches.Count);
        Assert.Equal(20, matches.Select(x => x.Surface).Distinct().Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("watashi")]
    [InlineData("%_")]
    public async Task Invalid_input_returns_400_without_reading_database(string? query)
    {
        var repository = new Mock<IKanjiRepository>(MockBehavior.Strict);
        var controller = new JapaneseDictionaryController(new JapaneseDictionaryService(repository.Object));
        var result = Assert.IsType<ObjectResult>(await controller.Search(query, default));
        Assert.Equal(400, result.StatusCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task No_match_returns_successful_empty_array()
    {
        await using var db = Context();
        var response = await Service(db).SearchAsync("わたし");
        Assert.True(response.IsSuccess);
        Assert.Equal(200, response.StatusCode);
        Assert.Empty(response.Result!);
    }

    private static JapaneseDictionaryService Service(AppDbContext db) => new(new KanjiRepository(db));
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Kanji Kanji(int id, string character, string? on, string? kun) => new()
    {
        Id = id, Character = character, Meaning = "Cây; gỗ", SinoVietnamese = "Mộc",
        OnYomi = on, KunYomi = kun,
    };
}
