using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses.MentorScheduling;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using Xunit;

namespace RikiPath.Tests;

// Bộ kiểm thử chuyên sâu cho Sổ cái tín dụng Mentor 1-1 (Ticket, Payment & Credit Ledger - Stage 06).
public sealed class MentorCreditLedgerTests
{
    private static AppDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static IClaimService MockClaims(Role role, int userId)
    {
        var mock = new Mock<IClaimService>();
        mock.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = userId, Role = role });
        return mock.Object;
    }

    // 1. Cấp lượt tư vấn thành công từ gói học Paid
    [Fact]
    public async Task CreateGrantFromPaidSubscriptionAsync_WhenPaid_CreatesGrantAndEntrySuccessfully()
    {
        await using var db = CreateInMemoryDb();
        var learner = new UserAccount { Id = 301, Role = Role.Learner, FirstName = "Van", LastName = "Nguyen", Email = "van@example.com", PasswordHash = [], PasswordSalt = [] };
        db.Users.Add(learner);

        var sub = new UserSubscription
        {
            Id = 501,
            UserId = 301,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 3,
            MeetingSessionsUsed = 0,
            PurchasedAt = DateTime.UtcNow,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(90)
        };
        db.UserSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var claims = MockClaims(Role.Learner, 301);
        var service = new MentorCreditLedgerService(uow, claims, NullLogger<MentorCreditLedgerService>.Instance);

        var result = await service.CreateGrantFromPaidSubscriptionAsync(501);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Result);
        Assert.Equal(3, result.Result.InitialQuantity);
        Assert.Equal(3, result.Result.AvailableQuantity);
        Assert.Equal(501, result.Result.UserSubscriptionId);

        // Kiểm tra bút toán sổ cái được ghi nhận
        var entries = await uow.MentorCreditLedger.GetEntriesByLearnerIdAsync(301, 0, 10);
        Assert.Single(entries);
        Assert.Equal(CreditEntryType.Grant, entries[0].EntryType);
        Assert.Equal(3, entries[0].Amount);
    }

    [Fact]
    public async Task CreateGrantFromPaidSubscriptionAsync_WhenLegacySessionsWereUsed_GrantsOnlyRemainingSessions()
    {
        await using var db = CreateInMemoryDb();
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = 509,
            UserId = 309,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 3,
            MeetingSessionsUsed = 1,
            PurchasedAt = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(90)
        });
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new MentorCreditLedgerService(uow, MockClaims(Role.Learner, 309), NullLogger<MentorCreditLedgerService>.Instance);

        var result = await service.CreateGrantFromPaidSubscriptionAsync(509);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Result!.InitialQuantity);
        Assert.Equal(2, result.Result.AvailableQuantity);
        var entry = Assert.Single(await uow.MentorCreditLedger.GetEntriesByLearnerIdAsync(309, 0, 10));
        Assert.Equal(2, entry.Amount);
        Assert.Equal(1, (await uow.UserSubscriptions.GetByIdAsync(509))!.MeetingSessionsUsed);
    }

    [Fact]
    public async Task CreateGrantFromPaidSubscriptionAsync_WhenLegacySessionsAreExhausted_DoesNotCreateCredit()
    {
        await using var db = CreateInMemoryDb();
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = 510,
            UserId = 310,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 1,
            MeetingSessionsUsed = 1,
            PurchasedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new MentorCreditLedgerService(uow, MockClaims(Role.Learner, 310), NullLogger<MentorCreditLedgerService>.Instance);

        var result = await service.CreateGrantFromPaidSubscriptionAsync(510);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Empty(await uow.MentorCreditLedger.GetAllGrantsForLearnerAsync(310));
        Assert.Empty(await uow.MentorCreditLedger.GetEntriesByLearnerIdAsync(310, 0, 10));
    }

    // 2. Chống cấp trùng (Idempotent): Gọi nhiều lần không cấp thêm lượt
    [Fact]
    public async Task CreateGrantFromPaidSubscriptionAsync_WhenCalledMultipleTimes_IsIdempotentAndDoesNotDuplicate()
    {
        await using var db = CreateInMemoryDb();
        var learner = new UserAccount { Id = 302, Role = Role.Learner, FirstName = "Hai", LastName = "Le", Email = "hai@example.com", PasswordHash = [], PasswordSalt = [] };
        db.Users.Add(learner);

        var sub = new UserSubscription
        {
            Id = 502,
            UserId = 302,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 1,
            MeetingSessionsUsed = 0,
            PurchasedAt = DateTime.UtcNow
        };
        db.UserSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var claims = MockClaims(Role.Learner, 302);
        var service = new MentorCreditLedgerService(uow, claims, NullLogger<MentorCreditLedgerService>.Instance);

        var res1 = await service.CreateGrantFromPaidSubscriptionAsync(502);
        var res2 = await service.CreateGrantFromPaidSubscriptionAsync(502);

        Assert.True(res1.IsSuccess);
        Assert.True(res2.IsSuccess);
        Assert.Equal(res1.Result!.GrantId, res2.Result!.GrantId);

        var grants = await uow.MentorCreditLedger.GetAllGrantsForLearnerAsync(302);
        Assert.Single(grants);
        Assert.Equal(1, grants[0].InitialQuantity);
        Assert.Equal(1, grants[0].AvailableQuantity);
    }

    // 3. Từ chối cấp lượt khi đơn hàng chưa thanh toán (Pending)
    [Fact]
    public async Task CreateGrantFromPaidSubscriptionAsync_WhenPaymentPending_FailsWithBadRequest()
    {
        await using var db = CreateInMemoryDb();
        var sub = new UserSubscription
        {
            Id = 503,
            UserId = 303,
            PaymentStatus = PaymentStatus.Pending,
            MeetingSessionsIncluded = 1
        };
        db.UserSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new MentorCreditLedgerService(uow, MockClaims(Role.Learner, 303), NullLogger<MentorCreditLedgerService>.Instance);

        var result = await service.CreateGrantFromPaidSubscriptionAsync(503);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Chỉ gói học đã thanh toán thành công", result.ErrorMessage);
    }

    // 4. Trừ lượt tư vấn thành công khi đặt lịch
    [Fact]
    public async Task SpendCreditAsync_WhenBalanceAvailable_DecrementsGrantAndAppendsSpendEntry()
    {
        await using var db = CreateInMemoryDb();
        var grant = new MentorCreditGrant
        {
            Id = 701,
            LearnerId = 304,
            UserSubscriptionId = 504,
            InitialQuantity = 3,
            AvailableQuantity = 3,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.MentorCreditGrants.Add(grant);

        var sub = new UserSubscription
        {
            Id = 504,
            UserId = 304,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 3,
            MeetingSessionsUsed = 0
        };
        db.UserSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new MentorCreditLedgerService(uow, MockClaims(Role.Learner, 304), NullLogger<MentorCreditLedgerService>.Instance);

        var appointmentTime = DateTime.UtcNow.AddDays(2);
        var result = await service.SpendCreditAsync(304, 101, "spend:booking:101", appointmentTime);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.RemainingBalance);

        // Kiểm tra số dư nguồn cấp giảm
        var updatedGrant = await uow.MentorCreditLedger.GetGrantByIdAsync(701);
        Assert.Equal(2, updatedGrant!.AvailableQuantity);

        // Kiểm tra bút toán Spend được thêm vào sổ cái
        var entry = await uow.MentorCreditLedger.GetEntryByOperationKeyAsync("spend:booking:101");
        Assert.NotNull(entry);
        Assert.Equal(CreditEntryType.Spend, entry.EntryType);
        Assert.Equal(-1, entry.Amount);
        Assert.Equal(101, entry.MentorBookingId);

        // Kiểm tra counter lịch sử của UserSubscription được đồng bộ
        var updatedSub = await uow.UserSubscriptions.GetByIdAsync(504);
        Assert.Equal(1, updatedSub!.MeetingSessionsUsed);
    }

    // 5. Trừ lượt theo nguyên tắc FIFO (gói sắp hết hạn nhất dùng trước)
    [Fact]
    public async Task SpendCreditAsync_UsesFifoOrder_SpendsFromEarliestExpiringGrantFirst()
    {
        await using var db = CreateInMemoryDb();
        // Gói A: hết hạn trong 5 ngày
        var grantA = new MentorCreditGrant
        {
            Id = 702,
            LearnerId = 305,
            UserSubscriptionId = 505,
            InitialQuantity = 1,
            AvailableQuantity = 1,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(5)
        };
        // Gói B: hết hạn trong 30 ngày
        var grantB = new MentorCreditGrant
        {
            Id = 703,
            LearnerId = 305,
            UserSubscriptionId = 506,
            InitialQuantity = 1,
            AvailableQuantity = 1,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        };
        db.MentorCreditGrants.AddRange(grantA, grantB);
        db.UserSubscriptions.AddRange(
            new UserSubscription { Id = 505, UserId = 305, MeetingSessionsIncluded = 1, MeetingSessionsUsed = 0 },
            new UserSubscription { Id = 506, UserId = 305, MeetingSessionsIncluded = 1, MeetingSessionsUsed = 0 });
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new MentorCreditLedgerService(uow, MockClaims(Role.Learner, 305), NullLogger<MentorCreditLedgerService>.Instance);

        var appointmentTime = DateTime.UtcNow.AddDays(2);
        var result = await service.SpendCreditAsync(305, 102, "spend:booking:102", appointmentTime);

        Assert.True(result.IsSuccess);
        Assert.Equal(702, result.CreditGrantId); // Phải trừ từ Gói A (FIFO)
        Assert.Equal(0, result.RemainingBalance);

        var reloadedA = await uow.MentorCreditLedger.GetGrantByIdAsync(702);
        var reloadedB = await uow.MentorCreditLedger.GetGrantByIdAsync(703);
        Assert.Equal(0, reloadedA!.AvailableQuantity);
        Assert.Equal(1, reloadedB!.AvailableQuantity); // Gói B còn nguyên
    }

    // 6. Ngăn chặn Double-Spend khi số dư bằng 0
    [Fact]
    public async Task SpendCreditAsync_WhenZeroBalance_FailsWithInsufficientCredit()
    {
        await using var db = CreateInMemoryDb();
        var grant = new MentorCreditGrant
        {
            Id = 704,
            LearnerId = 306,
            UserSubscriptionId = 507,
            InitialQuantity = 1,
            AvailableQuantity = 0, // ĐÃ DÙNG HẾT
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-10),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(10)
        };
        db.MentorCreditGrants.Add(grant);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new MentorCreditLedgerService(uow, MockClaims(Role.Learner, 306), NullLogger<MentorCreditLedgerService>.Instance);

        var result = await service.SpendCreditAsync(306, 103, "spend:booking:103", DateTime.UtcNow.AddDays(1));

        Assert.False(result.IsSuccess);
        Assert.Contains("không còn đủ lượt tư vấn hợp lệ", result.ErrorMessage);
    }

    // 7. Từ chối đặt lịch khi thời điểm hẹn sau ngày hết hạn gói
    [Fact]
    public async Task SpendCreditAsync_WhenAppointmentAfterGrantExpiry_Fails()
    {
        await using var db = CreateInMemoryDb();
        var grant = new MentorCreditGrant
        {
            Id = 705,
            LearnerId = 307,
            UserSubscriptionId = 508,
            InitialQuantity = 1,
            AvailableQuantity = 1,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-5),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(3) // Hết hạn trong 3 ngày
        };
        db.MentorCreditGrants.Add(grant);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new MentorCreditLedgerService(uow, MockClaims(Role.Learner, 307), NullLogger<MentorCreditLedgerService>.Instance);

        // Buổi hẹn diễn ra sau 5 ngày (đã quá hạn của gói)
        var result = await service.SpendCreditAsync(307, 104, "spend:booking:104", DateTime.UtcNow.AddDays(5));

        Assert.False(result.IsSuccess);
        Assert.Contains("không còn đủ lượt tư vấn hợp lệ hoặc lượt tư vấn đã hết hạn", result.ErrorMessage);
    }

    // 8. Chống trừ lặp (Idempotent Spend): Gọi nhiều lần cùng OperationKey không trừ 2 lần
    [Fact]
    public async Task SpendCreditAsync_WhenCalledWithSameOperationKey_IsIdempotentAndDoesNotSpendTwice()
    {
        await using var db = CreateInMemoryDb();
        var grant = new MentorCreditGrant
        {
            Id = 706,
            LearnerId = 308,
            UserSubscriptionId = 509,
            InitialQuantity = 2,
            AvailableQuantity = 2,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(20)
        };
        db.MentorCreditGrants.Add(grant);
        db.UserSubscriptions.Add(new UserSubscription { Id = 509, UserId = 308, MeetingSessionsIncluded = 2, MeetingSessionsUsed = 0 });
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new MentorCreditLedgerService(uow, MockClaims(Role.Learner, 308), NullLogger<MentorCreditLedgerService>.Instance);

        var apptTime = DateTime.UtcNow.AddDays(1);
        var res1 = await service.SpendCreditAsync(308, 105, "spend:booking:105", apptTime);
        var res2 = await service.SpendCreditAsync(308, 105, "spend:booking:105", apptTime);

        Assert.True(res1.IsSuccess);
        Assert.True(res2.IsSuccess);
        Assert.Equal(res1.EntryId, res2.EntryId);

        var reloadedGrant = await uow.MentorCreditLedger.GetGrantByIdAsync(706);
        Assert.Equal(1, reloadedGrant!.AvailableQuantity); // Chỉ trừ 1 lần, không bị trừ thành 0!
    }

    // 9. Hoàn lại lượt tư vấn thành công về đúng nguồn cấp ban đầu khi hủy lịch hợp lệ
    [Fact]
    public async Task RefundCreditAsync_RestoresCreditToOriginalGrantAndAppendsRefundEntry()
    {
        await using var db = CreateInMemoryDb();
        var grant = new MentorCreditGrant
        {
            Id = 707,
            LearnerId = 309,
            UserSubscriptionId = 510,
            InitialQuantity = 3,
            AvailableQuantity = 2, // Đã bị trừ 1
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-5),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(25)
        };
        db.MentorCreditGrants.Add(grant);

        var sub = new UserSubscription { Id = 510, UserId = 309, MeetingSessionsIncluded = 3, MeetingSessionsUsed = 1 };
        db.UserSubscriptions.Add(sub);

        // Bút toán Spend ban đầu
        var spendEntry = new MentorCreditEntry
        {
            Id = 901,
            CreditGrantId = 707,
            LearnerId = 309,
            EntryType = CreditEntryType.Spend,
            Amount = -1,
            OperationKey = "spend:booking:106",
            MentorBookingId = 106,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        db.MentorCreditEntries.Add(spendEntry);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new MentorCreditLedgerService(uow, MockClaims(Role.Learner, 309), NullLogger<MentorCreditLedgerService>.Instance);

        var refundResult = await service.RefundCreditAsync(309, 106, "refund:booking:106", "Học viên hủy trước 24 giờ");

        Assert.True(refundResult.IsSuccess);
        Assert.Equal(3, refundResult.RestoredBalance);

        // Nguồn cấp được cộng lại 1
        var reloadedGrant = await uow.MentorCreditLedger.GetGrantByIdAsync(707);
        Assert.Equal(3, reloadedGrant!.AvailableQuantity);

        // Bút toán Refund xuất hiện trong sổ cái
        var refundEntry = await uow.MentorCreditLedger.GetEntryByOperationKeyAsync("refund:booking:106");
        Assert.NotNull(refundEntry);
        Assert.Equal(CreditEntryType.Refund, refundEntry.EntryType);
        Assert.Equal(1, refundEntry.Amount);

        // Counter của UserSubscription được giảm
        var reloadedSub = await uow.UserSubscriptions.GetByIdAsync(510);
        Assert.Equal(0, reloadedSub!.MeetingSessionsUsed);
    }

    // 10. Chống hoàn lặp (Idempotent Refund): Gọi nhiều lần chỉ hoàn 1 lần
    [Fact]
    public async Task RefundCreditAsync_WhenCalledTwice_IsIdempotentAndDoesNotRefundTwice()
    {
        await using var db = CreateInMemoryDb();
        var grant = new MentorCreditGrant
        {
            Id = 708,
            LearnerId = 310,
            UserSubscriptionId = 511,
            InitialQuantity = 1,
            AvailableQuantity = 0,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(20)
        };
        db.MentorCreditGrants.Add(grant);
        db.UserSubscriptions.Add(new UserSubscription { Id = 511, UserId = 310, MeetingSessionsIncluded = 1, MeetingSessionsUsed = 1 });

        var spendEntry = new MentorCreditEntry
        {
            Id = 902,
            CreditGrantId = 708,
            LearnerId = 310,
            EntryType = CreditEntryType.Spend,
            Amount = -1,
            OperationKey = "spend:booking:107",
            MentorBookingId = 107
        };
        db.MentorCreditEntries.Add(spendEntry);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new MentorCreditLedgerService(uow, MockClaims(Role.Learner, 310), NullLogger<MentorCreditLedgerService>.Instance);

        var res1 = await service.RefundCreditAsync(310, 107, "refund:booking:107", "Hủy hợp lệ");
        var res2 = await service.RefundCreditAsync(310, 107, "refund:booking:107", "Hủy hợp lệ");

        Assert.True(res1.IsSuccess);
        Assert.True(res2.IsSuccess);
        Assert.Equal(res1.EntryId, res2.EntryId);

        var reloadedGrant = await uow.MentorCreditLedger.GetGrantByIdAsync(708);
        Assert.Equal(1, reloadedGrant!.AvailableQuantity); // Không bao giờ vượt quá InitialQuantity = 1!
    }

    // 11. Từ chối hoàn khi không tìm thấy bút toán trừ lượt ban đầu
    [Fact]
    public async Task RefundCreditAsync_WhenNoSpendFound_Fails()
    {
        await using var db = CreateInMemoryDb();
        var uow = new UnitOfWork(db);
        var service = new MentorCreditLedgerService(uow, MockClaims(Role.Learner, 311), NullLogger<MentorCreditLedgerService>.Instance);

        var result = await service.RefundCreditAsync(311, 999, "refund:booking:999", "Hủy buổi chưa từng trừ");

        Assert.False(result.IsSuccess);
        Assert.Contains("Không tìm thấy bút toán trừ lượt", result.ErrorMessage);
    }

    // 12. Quản trị viên đối soát tính toàn vẹn của sổ cái (Reconciliation Audit)
    [Fact]
    public async Task ReconcileLearnerCreditsForAdminAsync_VerifiesAuditLedgerConsistency()
    {
        await using var db = CreateInMemoryDb();
        // Cấp gói 3 buổi
        var grant = new MentorCreditGrant
        {
            Id = 710,
            LearnerId = 312,
            UserSubscriptionId = 512,
            InitialQuantity = 3,
            AvailableQuantity = 3,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        };
        db.MentorCreditGrants.Add(grant);
        db.MentorCreditEntries.Add(new MentorCreditEntry
        {
            CreditGrantId = 710,
            LearnerId = 312,
            EntryType = CreditEntryType.Grant,
            Amount = 3,
            OperationKey = "grant:sub:512"
        });
        db.UserSubscriptions.Add(new UserSubscription { Id = 512, UserId = 312, MeetingSessionsIncluded = 3, MeetingSessionsUsed = 0 });
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var adminClaims = MockClaims(Role.Admin, 900001);
        var service = new MentorCreditLedgerService(uow, adminClaims, NullLogger<MentorCreditLedgerService>.Instance);

        // 1. Trừ 1 buổi
        var spendRes = await service.SpendCreditAsync(312, 108, "spend:booking:108", DateTime.UtcNow.AddDays(1));
        Assert.True(spendRes.IsSuccess);

        // 2. Đối soát: Số dư còn lại 2, tổng bút toán (+3 - 1 = +2) -> Khớp đúng 100%!
        var recon1 = await service.ReconcileLearnerCreditsForAdminAsync(312);
        Assert.True(recon1.IsSuccess);
        Assert.True(recon1.Result!.IsConsistent);
        Assert.Equal(0, recon1.Result.Discrepancy);
        Assert.Equal(2, recon1.Result.TotalGrantsAvailable);
        Assert.Equal(2, recon1.Result.TotalEntriesNet);

        // 3. Hoàn lại buổi hẹn
        var refundRes = await service.RefundCreditAsync(312, 108, "refund:booking:108", "Học viên hủy trước 24h");
        Assert.True(refundRes.IsSuccess);

        // 4. Đối soát lại: Số dư 3, tổng bút toán (+3 - 1 + 1 = +3) -> Khớp đúng 100%!
        var recon2 = await service.ReconcileLearnerCreditsForAdminAsync(312);
        Assert.True(recon2.IsSuccess);
        Assert.True(recon2.Result!.IsConsistent);
        Assert.Equal(0, recon2.Result.Discrepancy);
        Assert.Equal(3, recon2.Result.TotalGrantsAvailable);
        Assert.Equal(3, recon2.Result.TotalEntriesNet);
    }

    // 13. Học viên xem số dư và thông tin gói sắp hết hạn nhất
    [Fact]
    public async Task GetMyCreditBalanceAsync_ReturnsCorrectTotalAndEarliestExpiry()
    {
        await using var db = CreateInMemoryDb();
        var expirySoon = DateTime.UtcNow.AddDays(7);
        var expiryLater = DateTime.UtcNow.AddDays(45);

        db.MentorCreditGrants.AddRange(
            new MentorCreditGrant
            {
                Id = 711,
                LearnerId = 313,
                UserSubscriptionId = 513,
                InitialQuantity = 1,
                AvailableQuantity = 1,
                EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
                ExpiresAtUtc = expirySoon
            },
            new MentorCreditGrant
            {
                Id = 712,
                LearnerId = 313,
                UserSubscriptionId = 514,
                InitialQuantity = 3,
                AvailableQuantity = 2,
                EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
                ExpiresAtUtc = expiryLater
            });
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var learnerClaims = MockClaims(Role.Learner, 313);
        var service = new MentorCreditLedgerService(uow, learnerClaims, NullLogger<MentorCreditLedgerService>.Instance);

        var result = await service.GetMyCreditBalanceAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Result);
        Assert.Equal(3, result.Result.TotalAvailableCredits); // 1 + 2 = 3
        Assert.Equal(expirySoon, result.Result.ExpiringSoonestAtUtc);
        Assert.Equal(2, result.Result.ActiveGrants.Count);
    }
}
