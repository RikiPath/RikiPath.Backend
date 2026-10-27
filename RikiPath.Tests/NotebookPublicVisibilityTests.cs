using System.Net;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Notebook;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Tests;

public sealed class NotebookPublicVisibilityTests
{
    [Fact]
    public async Task Published_vocabulary_can_be_bookmarked()
    {
        var fixture = CreateFixture();
        fixture.Vocabularies
            .Setup(x => x.GetPublicByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Vocabulary
            {
                Id = 101,
                Word = "公開",
                Reading = "こうかい",
                Meaning = "published",
            });

        var result = await fixture.Service.BookmarkVocabularyAsync(
            new BookmarkVocabularyRequest { LearnerNoteId = 10, VocabularyId = 101 },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Equal(101, result.Result!.VocabularyId);
        Assert.Null(result.Result.KanjiId);
        fixture.NoteEntries.Verify(
            x => x.AddAsync(It.IsAny<LearnerNoteEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.ReviewCards.Verify(
            x => x.AddAsync(It.IsAny<ReviewCard>(), It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.UnitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Repeated_vocabulary_bookmark_returns_existing_source_identity_without_duplicate_write()
    {
        var fixture = CreateFixture();
        var vocabulary = new Vocabulary
        {
            Id = 101,
            Word = "公開",
            Reading = "こうかい",
            Meaning = "published",
        };
        fixture.Vocabularies
            .Setup(x => x.GetPublicByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vocabulary);
        fixture.NoteEntries
            .Setup(x => x.GetByUserAsync(1, 10))
            .ReturnsAsync([
                new LearnerNoteEntry
                {
                    Id = 77,
                    LearnerNoteId = 10,
                    VocabularyId = 101,
                    Vocabulary = vocabulary,
                    CreatedDate = DateTime.UtcNow,
                    ReviewCards = [new ReviewCard { Id = 88, UserId = 1 }],
                },
            ]);

        var result = await fixture.Service.BookmarkVocabularyAsync(
            new BookmarkVocabularyRequest { LearnerNoteId = 10, VocabularyId = 101 },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(77, result.Result!.NoteEntryId);
        Assert.Equal(88, result.Result.ReviewCardId);
        Assert.Equal(101, result.Result.VocabularyId);
        Assert.Null(result.Result.KanjiId);
        fixture.NoteEntries.Verify(
            x => x.AddAsync(It.IsAny<LearnerNoteEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.UnitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Hidden_vocabulary_cannot_be_bookmarked()
    {
        var fixture = CreateFixture();
        fixture.Vocabularies
            .Setup(x => x.GetPublicByIdAsync(102, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vocabulary?)null);

        var result = await fixture.Service.BookmarkVocabularyAsync(
            new BookmarkVocabularyRequest { LearnerNoteId = 10, VocabularyId = 102 },
            CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        fixture.NoteEntries.Verify(
            x => x.AddAsync(It.IsAny<LearnerNoteEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Hidden_kanji_cannot_be_bookmarked()
    {
        var fixture = CreateFixture();
        fixture.Kanjis
            .Setup(x => x.GetPublicByIdAsync(103, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Kanji?)null);

        var result = await fixture.Service.BookmarkKanjiAsync(
            new BookmarkKanjiRequest { LearnerNoteId = 10, KanjiId = 103 },
            CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        fixture.NoteEntries.Verify(
            x => x.AddAsync(It.IsAny<LearnerNoteEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Published_kanji_can_be_bookmarked_atomically()
    {
        var fixture = CreateFixture();
        fixture.Kanjis
            .Setup(x => x.GetPublicByIdAsync(106, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Kanji
            {
                Id = 106,
                Character = "学",
                Meaning = "study",
            });

        var result = await fixture.Service.BookmarkKanjiAsync(
            new BookmarkKanjiRequest { LearnerNoteId = 10, KanjiId = 106 },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Equal(106, result.Result!.KanjiId);
        Assert.Null(result.Result.VocabularyId);
        fixture.NoteEntries.Verify(
            x => x.AddAsync(It.IsAny<LearnerNoteEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.ReviewCards.Verify(
            x => x.AddAsync(It.IsAny<ReviewCard>(), It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.UnitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Hidden_grammar_cannot_be_bookmarked()
    {
        var fixture = CreateFixture();
        fixture.GrammarPatterns
            .Setup(x => x.GetPublicByIdAsync(104, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GrammarPattern?)null);

        var result = await fixture.Service.BookmarkGrammarAsync(104, CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        fixture.ReviewCards.Verify(
            x => x.AddAsync(It.IsAny<ReviewCard>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Deleted_referenced_content_cannot_be_bookmarked()
    {
        var fixture = CreateFixture();
        fixture.Vocabularies
            .Setup(x => x.GetPublicByIdAsync(105, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vocabulary?)null);

        var result = await fixture.Service.BookmarkVocabularyAsync(
            new BookmarkVocabularyRequest { LearnerNoteId = 10, VocabularyId = 105 },
            CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static NotebookFixture CreateFixture()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var claims = new Mock<IClaimService>();
        var notes = new Mock<ILearnerNoteRepository>();
        var noteEntries = new Mock<ILearnerNoteEntryRepository>();
        var reviewCards = new Mock<IReviewCardRepository>();
        var vocabularies = new Mock<IVocabularyRepository>();
        var kanjis = new Mock<IKanjiRepository>();
        var grammarPatterns = new Mock<IGrammarPatternRepository>();

        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = 1, Role = Role.Learner });
        notes.Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LearnerNote
            {
                Id = 10,
                UserId = 1,
                Name = "Safe test notebook",
                CreatedDate = DateTime.UtcNow,
            });
        noteEntries
            .Setup(x => x.AddAsync(It.IsAny<LearnerNoteEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LearnerNoteEntry entry, CancellationToken _) => entry);
        noteEntries
            .Setup(x => x.GetByUserAsync(1, 10))
            .ReturnsAsync([]);
        reviewCards
            .Setup(x => x.AddAsync(It.IsAny<ReviewCard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewCard card, CancellationToken _) => card);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        unitOfWork.SetupGet(x => x.LearnerNotes).Returns(notes.Object);
        unitOfWork.SetupGet(x => x.LearnerNoteEntries).Returns(noteEntries.Object);
        unitOfWork.SetupGet(x => x.ReviewCards).Returns(reviewCards.Object);
        unitOfWork.SetupGet(x => x.Vocabularies).Returns(vocabularies.Object);
        unitOfWork.SetupGet(x => x.Kanjis).Returns(kanjis.Object);
        unitOfWork.SetupGet(x => x.GrammarPatterns).Returns(grammarPatterns.Object);

        return new NotebookFixture(
            new NotebookService(unitOfWork.Object, claims.Object),
            unitOfWork,
            noteEntries,
            reviewCards,
            vocabularies,
            kanjis,
            grammarPatterns);
    }

    private sealed record NotebookFixture(
        NotebookService Service,
        Mock<IUnitOfWork> UnitOfWork,
        Mock<ILearnerNoteEntryRepository> NoteEntries,
        Mock<IReviewCardRepository> ReviewCards,
        Mock<IVocabularyRepository> Vocabularies,
        Mock<IKanjiRepository> Kanjis,
        Mock<IGrammarPatternRepository> GrammarPatterns);
}
