using System.Linq.Expressions;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Tests;

public sealed class KanjiKnowledgeLibraryTests
{
    [Fact]
    public async Task Library_returns_all_target_level_characters_including_future_reviews_without_creating_cards()
    {
        var uow = new Mock<IUnitOfWork>();
        var users = new Mock<IUserAccountRepository>();
        var kanjis = new Mock<IKanjiRepository>();
        var cards = new Mock<IReviewCardRepository>();
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = 7 });
        uow.SetupGet(x => x.UserAccounts).Returns(users.Object);
        uow.SetupGet(x => x.Kanjis).Returns(kanjis.Object);
        uow.SetupGet(x => x.ReviewCards).Returns(cards.Object);
        users.Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccount { TargetCertificateLevelId = 3 });
        var data = Enumerable.Range(1, 205).Select(id => new Kanji
        {
            Id = id, Character = "日", Meaning = "Ngày",
            ContentLevelMappings = [new() { CertificateLevelId = 3 }]
        }).Concat([
            new Kanji { Id = 300, ContentLevelMappings = [new() { CertificateLevelId = 4 }] },
            new Kanji { Id = 301, ContentLevelMappings = [new() { CertificateLevelId = 3, IsDeleted = true }] }
        ]).ToList();
        kanjis.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Kanji, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Kanji, bool>> predicate, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<Kanji>>(data.Where(predicate.Compile()).ToList()));
        cards.Setup(x => x.FindAsync(It.IsAny<Expression<Func<ReviewCard, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReviewCard> { new() {
                UserId = 7, KanjiId = 1, Mode = ReviewMode.Writing,
                NextReviewDate = DateTime.UtcNow.AddDays(7), WritingScore = 80
            } });

        var result = await new KanjiWritingPracticeService(uow.Object, claims.Object)
            .GetAllForPracticeAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(205, result.Result!.Count);
        Assert.DoesNotContain(result.Result, x => x.KanjiId >= 300);
        Assert.Equal(80, result.Result[0].WritingScore);
        Assert.False(result.Result[0].IsNew);
        cards.Verify(x => x.AddAsync(It.IsAny<ReviewCard>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
