using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class ManualContentTests
{
    [Fact]
    public async Task Author_can_create_reuse_and_change_a_lesson_topic_through_approval()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Set<UserAccount>().Add(new UserAccount { Id = 7, Role = Role.ContentAuthor, PasswordHash = [], PasswordSalt = [] });
        db.CertificateTypes.Add(new CertificateType { Id = 41, Name = "JLPT", IsActive = true });
        db.CertificateLevels.Add(new CertificateLevel { Id = 42, CertificateTypeId = 41, Code = "N5" });
        db.LanguageSkills.Add(new LanguageSkill { Id = 43, Name = "Grammar" });
        db.Topics.Add(new Topic { Id = 44, Title = "Mua sắm" });
        await db.SaveChangesAsync();

        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = 7, Role = Role.ContentAuthor });
        var uow = new UnitOfWork(db);
        var author = new ManualContentService(uow, claims.Object);
        var review = new ContentApprovalService(uow, Mock.Of<IExcelParser>(), claims.Object, [], [], Mock.Of<IContentApprovalNotifierService>());
        static JsonElement Input(string title, string? topicTitle = null, int? topicId = null) =>
            JsonSerializer.SerializeToElement(new { title, certificateLevelId = 42, languageSkillId = 43, topicTitle, topicId });

        var invalid = await author.SaveAsync(ImportEntityType.Lesson, null, Input("Bài lỗi", topicId: 999), default);
        Assert.False(invalid.IsSuccess);
        var first = await author.SaveAsync(ImportEntityType.Lesson, null, Input("Chào buổi sáng", "  Chào hỏi  "), default);
        Assert.True(first.IsSuccess, first.ErrorMessage);
        var second = await author.SaveAsync(ImportEntityType.Lesson, null, Input("Tự giới thiệu", "chào hỏi"), default);
        Assert.True(second.IsSuccess, second.ErrorMessage);
        Assert.True((await author.SubmitAsync(first.Result, default)).IsSuccess);
        Assert.True((await author.SubmitAsync(second.Result, default)).IsSuccess);
        Assert.Single(await db.Topics.ToListAsync());
        Assert.Empty(await db.Lessons.ToListAsync());

        var firstReview = await review.ReviewRequestAsync(first.Result, new ReviewContentRequest { Approve = true }, default);
        Assert.True(firstReview.IsSuccess, firstReview.ErrorMessage);
        var secondReview = await review.ReviewLessonGroupAsync(second.Result, new ReviewContentRequest { Approve = true }, default);
        Assert.True(secondReview.IsSuccess, secondReview.ErrorMessage);
        var topic = await db.Topics.Include(x => x.Lessons).SingleAsync(x => x.Title == "Chào hỏi");
        Assert.Equal(2, topic.Lessons.Count);
        Assert.Equal(2, await db.Topics.CountAsync());

        var revision = await author.SaveAsync(ImportEntityType.Lesson, first.Result, Input("Chào buổi sáng", topicId: 44), default);
        Assert.True(revision.IsSuccess, revision.ErrorMessage);
        Assert.True((await author.SubmitAsync(revision.Result, default)).IsSuccess);
        var pending = await review.GetPendingApprovalsAsync(ImportEntityType.Lesson, default);
        Assert.True(pending.IsSuccess, pending.ErrorMessage);
        var change = Assert.Single(Assert.Single(pending.Result!).Fields, field => field.Field == "topicTitle");
        Assert.Equal("Chào hỏi", change.OldValue);
        Assert.Equal("Mua sắm", change.NewValue);
        Assert.True(change.IsChanged);
        var revised = await review.ReviewRequestAsync(revision.Result, new ReviewContentRequest { Approve = true }, default);
        Assert.True(revised.IsSuccess, revised.ErrorMessage);
        var saved = await db.Lessons.SingleAsync(x => x.Title == "Chào buổi sáng");
        Assert.Equal(44, saved.TopicId);
        var detail = await author.GetAsync(revision.Result, default);
        Assert.Equal(44, detail.Result.GetProperty("TopicId").GetInt32());

        var legacy = await author.SaveAsync(ImportEntityType.Lesson, revision.Result, Input("Chào buổi sáng"), default);
        Assert.True(legacy.IsSuccess, legacy.ErrorMessage);
        Assert.True((await author.SubmitAsync(legacy.Result, default)).IsSuccess);
        Assert.True((await review.ReviewRequestAsync(legacy.Result, new ReviewContentRequest { Approve = true }, default)).IsSuccess);
        Assert.Equal(44, saved.TopicId);

        var clear = await author.SaveAsync(ImportEntityType.Lesson, legacy.Result, Input("Chào buổi sáng", ""), default);
        Assert.True(clear.IsSuccess, clear.ErrorMessage);
        Assert.True((await author.SubmitAsync(clear.Result, default)).IsSuccess);
        Assert.True((await review.ReviewRequestAsync(clear.Result, new ReviewContentRequest { Approve = true }, default)).IsSuccess);
        Assert.Null(saved.TopicId);
    }

    [Fact]
    public async Task Author_edits_own_draft_and_published_revision_uses_same_content_id()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CertificateTypes.Add(new CertificateType { Id = 41, Name = "JLPT manual test", IsActive = true });
        db.CertificateLevels.Add(new CertificateLevel { Id = 42, CertificateTypeId = 41, Code = "N5" });
        await db.SaveChangesAsync();
        var claims = new Mock<IClaimService>();
        var actor = new ClaimDTO { Id = 7, Role = Role.ContentAuthor };
        claims.Setup(x => x.GetUserClaim()).Returns(() => actor);
        var uow = new UnitOfWork(db);
        var author = new ManualContentService(uow, claims.Object);
        var review = new ContentApprovalService(uow, Mock.Of<IExcelParser>(), claims.Object, [], [], Mock.Of<IContentApprovalNotifierService>());
        static JsonElement Vocabulary(string meaning) => JsonSerializer.SerializeToElement(new
        {
            certificateLevelId = 42, word = "山", reading = "やま", meaning
        });

        var created = await author.SaveAsync(ImportEntityType.Vocabulary, null, Vocabulary("núi"), default);
        Assert.True(created.IsSuccess, created.ErrorMessage);
        actor = new ClaimDTO { Id = 8, Role = Role.ContentAuthor };
        Assert.False((await author.GetAsync(created.Result, default)).IsSuccess);
        Assert.False((await author.SaveAsync(ImportEntityType.Vocabulary, created.Result, Vocabulary("khác"), default)).IsSuccess);

        actor = new ClaimDTO { Id = 7, Role = Role.ContentAuthor };
        var edited = await author.SaveAsync(ImportEntityType.Vocabulary, created.Result, Vocabulary("ngọn núi"), default);
        Assert.Equal(created.Result, edited.Result);
        Assert.True((await author.SubmitAsync(created.Result, default)).IsSuccess);
        Assert.Empty(await author.ListAsync(ImportEntityType.Vocabulary, ContentApprovalStatus.Draft, default));
        var pending = Assert.Single(await author.ListAsync(
            ImportEntityType.Vocabulary, ContentApprovalStatus.PendingReview, default));
        Assert.Equal(created.Result, pending.Id);
        Assert.Equal(42, pending.CertificateLevelId);
        Assert.False((await author.SaveAsync(ImportEntityType.Vocabulary, created.Result, Vocabulary("đồi"), default)).IsSuccess);
        Assert.True((await review.ReviewRequestAsync(created.Result, new ReviewContentRequest { Approve = true }, default)).IsSuccess);
        var published = await db.Vocabularies.SingleAsync();

        var revision = await author.SaveAsync(ImportEntityType.Vocabulary, created.Result, Vocabulary("ngọn núi, sơn"), default);
        Assert.True(revision.IsSuccess, revision.ErrorMessage);
        Assert.NotEqual(created.Result, revision.Result);
        Assert.Equal(published.Id, (await db.ContentApprovalRequests.FindAsync(revision.Result))!.TargetEntityId);
        Assert.Equal("ngọn núi", published.Meaning);
        Assert.True((await author.SubmitAsync(revision.Result, default)).IsSuccess);
        Assert.True((await review.ReviewRequestAsync(revision.Result, new ReviewContentRequest { Approve = true }, default)).IsSuccess);
        Assert.Equal(published.Id, (await db.Vocabularies.SingleAsync()).Id);
        Assert.Equal("ngọn núi, sơn", published.Meaning);
    }

    [Fact]
    public async Task Excel_drafts_are_reopened_by_owner_and_reach_admin_only_after_submission()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.AddRange(
            new UserAccount { Id = 7, Role = Role.ContentAuthor, PasswordHash = [], PasswordSalt = [] },
            new UserAccount { Id = 8, Role = Role.ContentAuthor, PasswordHash = [], PasswordSalt = [] });
        db.CertificateTypes.Add(new CertificateType { Id = 41, Name = "JLPT", IsActive = true });
        db.CertificateLevels.Add(new CertificateLevel { Id = 42, CertificateTypeId = 41, Code = "N5" });
        db.LanguageSkills.Add(new LanguageSkill { Id = 43, Name = "Vocabulary" });
        await db.SaveChangesAsync();

        var claims = new Mock<IClaimService>();
        var actor = new ClaimDTO { Id = 7, Role = Role.ContentAuthor };
        claims.Setup(service => service.GetUserClaim()).Returns(() => actor);
        var notifier = new Mock<IContentApprovalNotifierService>();
        var unit = new UnitOfWork(db);
        var importer = new ExcelContentImportService(unit, []);
        var author = new ManualContentService(unit, claims.Object, notifier.Object);
        var review = new ContentApprovalService(unit, Mock.Of<IExcelParser>(), claims.Object, [], [], Mock.Of<IContentApprovalNotifierService>());
        var request = new ConfirmImportRequest
        {
            BatchId = Guid.NewGuid(),
            SourceFileName = "bai-hoc.xlsx",
            Rows =
            [
                new() { EntityType = ImportEntityType.Lesson, Action = ImportRowAction.New, RowKey = "Mua sắm / N5",
                    SourceRowNumber = 2, PayloadJson = JsonSerializer.Serialize(new { Title = "Mua sắm",
                        CertificateLevelId = 42, CertificateLevelCode = "N5", LanguageSkillId = 43 }) },
                new() { EntityType = ImportEntityType.Vocabulary, Action = ImportRowAction.New, RowKey = "店 / みせ",
                    SourceRowNumber = 2, PayloadJson = JsonSerializer.Serialize(new { Word = "店", Reading = "みせ",
                        Meaning = "tiệm", LessonTitle = "Mua sắm", CertificateLevelId = 42, CertificateLevelCode = "N5" }) }
            ]
        };
        var saved = await importer.ConfirmAsync(request, 7, default);
        Assert.True(saved.IsSuccess, saved.ErrorMessage);
        var batchId = saved.Result!.BatchId;
        var retry = await importer.ConfirmAsync(request, 7, default);
        Assert.Equal(batchId, retry.Result!.BatchId);
        db.ChangeTracker.Clear();
        var history = Assert.Single(await author.GetImportBatchesAsync(default));
        Assert.Equal(2, history.DraftCount);
        var items = await author.ListAsync(null, null, default, batchId);
        Assert.Equal(2, items.Count);
        var lesson = Assert.Single(items, item => item.EntityType == ImportEntityType.Lesson);
        var word = Assert.Single(items, item => item.EntityType == ImportEntityType.Vocabulary);
        Assert.Equal(word.Id, Assert.Single(await author.ListAsync(ImportEntityType.Vocabulary, ContentApprovalStatus.Draft, default)).Id);
        Assert.Equal("tiệm", (await author.GetAsync(word.Id, default)).Result.GetProperty("Meaning").GetString());
        Assert.Empty((await review.GetPendingApprovalsAsync(null, default)).Result!);
        Assert.False((await review.ReviewRequestAsync(word.Id, new ReviewContentRequest { Approve = true }, default)).IsSuccess);
        Assert.Empty(await db.Vocabularies.ToListAsync());
        notifier.VerifyNoOtherCalls();

        actor = new ClaimDTO { Id = 8, Role = Role.ContentAuthor };
        Assert.Empty(await author.GetImportBatchesAsync(default));
        Assert.Empty(await author.ListAsync(null, null, default, batchId));
        Assert.Equal(404, (await author.GetAsync(word.Id, default)).StatusCode);
        Assert.Equal(404, (await author.SubmitBatchAsync(batchId, default)).StatusCode);

        actor = new ClaimDTO { Id = 7, Role = Role.ContentAuthor };
        var edit = await author.SaveAsync(ImportEntityType.Vocabulary, word.Id, JsonSerializer.SerializeToElement(new
        {
            Word = "店", Reading = "みせ", Meaning = "cửa hàng", LessonTitle = "Mua sắm", CertificateLevelId = 42
        }), default);
        Assert.True(edit.IsSuccess, edit.ErrorMessage);
        Assert.True((await author.SubmitAsync(lesson.Id, default)).IsSuccess);
        var pending = Assert.Single((await review.GetPendingApprovalsAsync(null, default)).Result!);
        Assert.Empty(pending.RelatedLessonContent);
        Assert.True((await review.ReviewLessonGroupAsync(lesson.Id, new ReviewContentRequest { Approve = true }, default)).IsSuccess);
        Assert.Empty(await db.Vocabularies.ToListAsync());
        Assert.Equal(1, (await author.SubmitBatchAsync(batchId, default)).Result);
        Assert.Equal(0, (await author.SubmitBatchAsync(batchId, default)).Result);
        notifier.Verify(service => service.NotifyAdminsOnRequestCreatedAsync(1, "bai-hoc.xlsx", It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.True((await review.ReviewRequestAsync(word.Id, new ReviewContentRequest { Approve = true }, default)).IsSuccess);
        Assert.Equal("cửa hàng", (await db.Vocabularies.SingleAsync()).Meaning);
        Assert.Equal(2, Assert.Single(await author.GetImportBatchesAsync(default)).ApprovedCount);
    }

    [Fact]
    public async Task Manual_listening_question_requires_mp3_before_saving()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CertificateTypes.Add(new CertificateType { Id = 41, Name = "JLPT manual test", IsActive = true });
        db.CertificateLevels.Add(new CertificateLevel { Id = 42, CertificateTypeId = 41, Code = "N5" });
        db.CertificateSections.Add(new CertificateSection { CertificateLevelId = 42, Name = "Nghe hiểu", Code = "N5_LISTENING" });
        db.LanguageSkills.Add(new LanguageSkill { Id = 44, Name = "Listening" });
        await db.SaveChangesAsync();
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = 7, Role = Role.ContentAuthor });
        var author = new ManualContentService(new UnitOfWork(db), claims.Object);
        var input = JsonSerializer.SerializeToElement(new
        {
            title = "Đề N5", certificateLevelId = 42, timeLimitMinutes = 60,
            sections = new[] { new
            {
                title = "Nghe hiểu", certificateSectionCode = "N5_LISTENING", languageSkillId = 44,
                questions = new[] { new
                {
                    questionText = "何時ですか？", options = new[]
                    {
                        new { optionText = "八時", isCorrect = true },
                        new { optionText = "九時", isCorrect = false },
                    },
                } },
            } },
        });
        var result = await author.SaveAsync(ImportEntityType.MockTest, null, input, default);
        Assert.False(result.IsSuccess);
        Assert.Contains("thiếu AudioUrl", result.ErrorMessage);
        Assert.Empty(await db.ContentApprovalRequests.ToListAsync());
    }

    [Fact]
    public async Task Author_can_save_edit_and_publish_a_grammar_ordering_question()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CertificateTypes.Add(new CertificateType { Id = 41, Name = "JLPT", IsActive = true });
        db.CertificateLevels.Add(new CertificateLevel { Id = 42, CertificateTypeId = 41, Code = "N5" });
        var skills = new[] { "Vocabulary", "Grammar", "Reading", "Listening" };
        foreach (var (skill, index) in skills.Select((skill, index) => (skill, index)))
        {
            db.LanguageSkills.Add(new LanguageSkill { Id = 50 + index, Name = skill });
            db.CertificateSections.Add(new CertificateSection { CertificateLevelId = 42, Name = skill, Code = $"N5_{skill.ToUpperInvariant()}" });
        }
        await db.SaveChangesAsync();

        var claims = new Mock<IClaimService>();
        var actor = new ClaimDTO { Id = 7, Role = Role.ContentAuthor };
        claims.Setup(x => x.GetUserClaim()).Returns(() => actor);
        var uow = new UnitOfWork(db);
        var author = new ManualContentService(uow, claims.Object);
        var review = new ContentApprovalService(uow, Mock.Of<IExcelParser>(), claims.Object, [], [], Mock.Of<IContentApprovalNotifierService>());
        var tokens = new[] { "読みます", "毎日", "本を" };
        var answer = new[] { "毎日", "本を", "読みます" };
        JsonElement Input(string[] answerKey, string explanation) => JsonSerializer.SerializeToElement(new
        {
            title = "Đề N5 có câu sắp xếp",
            certificateLevelId = 42,
            timeLimitMinutes = 60,
            sections = skills.Select((skill, index) => new
            {
                title = skill,
                certificateSectionCode = $"N5_{skill.ToUpperInvariant()}",
                languageSkillId = 50 + index,
                questions = new object[] { skill == "Grammar"
                    ? new { questionText = "Sắp xếp thành câu", questionType = "Ordering",
                        interactionData = new { tokens }, answerKey, options = Array.Empty<object>(), explanation }
                    : new { questionText = "Chọn đáp án", audioUrl = skill == "Listening" ? "https://example.com/listen.mp3" : null,
                        options = new[] { new { optionText = "A", isCorrect = true }, new { optionText = "B", isCorrect = false } } } },
            }),
        });

        var invalid = await author.SaveAsync(ImportEntityType.MockTest, null, Input(["毎日", "本を", "本を"], ""), default);
        Assert.False(invalid.IsSuccess);
        Assert.Contains("Câu sắp xếp", invalid.ErrorMessage);

        var created = await author.SaveAsync(ImportEntityType.MockTest, null, Input(answer, ""), default);
        Assert.True(created.IsSuccess, created.ErrorMessage);
        var edited = await author.SaveAsync(ImportEntityType.MockTest, created.Result, Input(answer, "Đọc sách mỗi ngày."), default);
        Assert.True(edited.IsSuccess, edited.ErrorMessage);
        Assert.Equal(created.Result, edited.Result);
        var draft = await author.GetAsync(created.Result, default);
        Assert.True(draft.IsSuccess, draft.ErrorMessage);
        var draftQuestion = draft.Result.GetProperty("Sections")[1].GetProperty("Questions")[0];
        Assert.Equal(answer, draftQuestion.GetProperty("AnswerKey").Deserialize<string[]>());
        Assert.True((await author.SubmitAsync(created.Result, default)).IsSuccess);

        actor = new ClaimDTO { Id = 8, Role = Role.Admin };
        var approved = await review.ReviewRequestAsync(created.Result, new ReviewContentRequest { Approve = true }, default);
        Assert.True(approved.IsSuccess, approved.ErrorMessage);
        var published = await db.MockQuestions.SingleAsync(question => question.QuestionType == "Ordering");
        Assert.Equal("Đọc sách mỗi ngày.", published.Explanation);
        Assert.Equal(tokens, JsonDocument.Parse(published.InteractionDataJson!).RootElement.GetProperty("tokens").Deserialize<string[]>());
        Assert.Equal(answer, JsonSerializer.Deserialize<string[]>(published.AnswerKeyJson!));
    }
}

