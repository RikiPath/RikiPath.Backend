using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorCompensation;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Clients;
using RikiPath.Infrastructure.Services;
using Xunit;

namespace RikiPath.Tests;

public sealed class MentorCompensationAndPayoutTests
{
    private static AppDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        SeedCertificateLevels(db);
        return db;
    }

    private static void SeedCertificateLevels(AppDbContext db)
    {
        if (db.CertificateLevels.Any()) return;
        db.CertificateLevels.AddRange(
            new CertificateLevel { Id = 1, CertificateTypeId = 1, Code = "N5", Description = "JLPT N5", SortOrder = 1 },
            new CertificateLevel { Id = 2, CertificateTypeId = 1, Code = "N4", Description = "JLPT N4", SortOrder = 2 },
            new CertificateLevel { Id = 3, CertificateTypeId = 1, Code = "N3", Description = "JLPT N3", SortOrder = 3 },
            new CertificateLevel { Id = 4, CertificateTypeId = 1, Code = "N2", Description = "JLPT N2", SortOrder = 4 },
            new CertificateLevel { Id = 5, CertificateTypeId = 1, Code = "N1", Description = "JLPT N1", SortOrder = 5 }
        );
        db.SaveChanges();
    }

    private sealed class MutableClaimService : IClaimService
    {
        public int UserId { get; set; } = 1;
        public Role Role { get; set; } = Role.Admin;
        public ClaimDTO GetUserClaim() => new() { Id = UserId, Role = Role };
    }

    private static (MentorCompensationService CompService, PayOsPayoutClient PayoutClient, AppSettings Settings, MutableClaimService Claims)
        CreateCompensationStack(AppDbContext db, bool qaSimulatorEnabled = true, bool livePayoutEnabled = false)
    {
        var settings = new AppSettings
        {
            FrontendBaseUrl = "https://rikipath.test",
            SecretToken = new SecretToken { Value = "unit-test-secret-token-32bytes-minimum!!" },
            MentorBankSecurity = new MentorBankSecuritySettings
            {
                EncryptionKey = "unit-test-mentor-bank-aes256-key-2026!!"
            },
            PayOsPayout = new PayOsPayoutSettings
            {
                LivePayoutEnabled = livePayoutEnabled,
                QaSimulatorEnabled = qaSimulatorEnabled,
                BaseUrl = "https://api-merchant.payos.vn",
                ClientId = "test-client-id",
                ApiKey = "test-api-key",
                ChecksumKey = "test-checksum-secret-key-2026"
            }
        };

        var claims = new MutableClaimService { UserId = 1, Role = Role.Admin };
        var payoutClient = new PayOsPayoutClient(new HttpClient(), settings, NullLogger<PayOsPayoutClient>.Instance);
        var compService = new MentorCompensationService(db, claims, payoutClient, settings, NullLogger<MentorCompensationService>.Instance);
        return (compService, payoutClient, settings, claims);
    }

    // =========================================================================
    // 1. LEVEL-BOUND PACKAGE CATALOG & TICKET LEDGER (N5–N2 ONLY; N1 REJECTED)
    // =========================================================================

    [Fact]
    public async Task MentorPlanService_AllowsN5ToN2_AndRejectsN1()
    {
        await using var db = CreateInMemoryDb();
        var claims = new MutableClaimService { UserId = 1, Role = Role.Admin };
        var uow = new UnitOfWork(db);
        var planService = new MentorPlanService(uow, claims);

        var n1Result = await planService.SaveAsync(null, new SaveMentorPlanRequest(
            Name: "Gói tư vấn JLPT N1",
            Description: "Không được phép",
            MeetingSessionCount: 1,
            Price: 500_000m,
            IsActive: true,
            JlptLevel: "N1"), CancellationToken.None);

        Assert.False(n1Result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, n1Result.StatusCode);

        var n3Result = await planService.SaveAsync(null, new SaveMentorPlanRequest(
            Name: "Gói tư vấn JLPT N3 3 buổi",
            Description: "Hiệu lực 90 ngày",
            MeetingSessionCount: 3,
            Price: 1_200_000m,
            IsActive: true,
            JlptLevel: "n3"), CancellationToken.None);

        Assert.True(n3Result.IsSuccess);
        var savedPlan = await db.SubscriptionPlans.FindAsync(n3Result.Result);
        Assert.NotNull(savedPlan);
        Assert.Equal(3, savedPlan!.CertificateLevelId);
        Assert.Equal(90, savedPlan.DurationDays);
    }

    // =========================================================================
    // 2. LEVEL-MATCHED BOOKING, QUALIFICATION GATE & FROZEN SNAPSHOT
    // =========================================================================

    [Fact]
    public async Task BookSlotAsync_EnforcesLevelMatch_Qualification_AndFreezesImmutableSnapshot()
    {
        await using var db = CreateInMemoryDb();
        var (compService, _, settings, claims) = CreateCompensationStack(db);

        var admin = new UserAccount { Id = 1, Role = Role.Admin, FirstName = "Admin", LastName = "One", Email = "admin@test.com", PasswordHash = [], PasswordSalt = [] };
        var mentor = new UserAccount { Id = 10, Role = Role.Mentor, FirstName = "Kenji", LastName = "Sato", Email = "mentor@test.com", PasswordHash = [], PasswordSalt = [] };
        var learner = new UserAccount { Id = 20, Role = Role.Learner, FirstName = "Lan", LastName = "Tran", Email = "learner@test.com", PasswordHash = [], PasswordSalt = [] };
        db.Users.AddRange(admin, mentor, learner);
        db.MentorProfiles.Add(new MentorProfile
        {
            MentorId = mentor.Id,
            VerificationStatus = MentorVerificationStatus.Approved,
            Bio = "Chuyên gia luyện thi JLPT"
        });

        var slotStart = DateTime.UtcNow.Date.AddDays(3).AddHours(2);
        var slot1 = new MentorAvailability
        {
            Id = 1001,
            MentorId = mentor.Id,
            StartTime = slotStart,
            EndTime = slotStart.AddMinutes(90),
            MaxLearners = 1,
            BookedCount = 0,
            IsApproved = true,
            RoomId = Guid.NewGuid()
        };
        var slot2 = new MentorAvailability
        {
            Id = 1002,
            MentorId = mentor.Id,
            StartTime = slotStart.AddDays(1),
            EndTime = slotStart.AddDays(1).AddMinutes(90),
            MaxLearners = 1,
            BookedCount = 0,
            IsApproved = true,
            RoomId = Guid.NewGuid()
        };
        db.MentorAvailabilities.AddRange(slot1, slot2);

        // Learner has an N5 ticket (CertificateLevelId = 1) and an N3 ticket (CertificateLevelId = 3)
        var subN5 = new UserSubscription
        {
            Id = 2001,
            UserId = learner.Id,
            SubscriptionPlanId = 1,
            CertificateLevelId = 1,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 1,
            PurchasedAt = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var subN3 = new UserSubscription
        {
            Id = 2002,
            UserId = learner.Id,
            SubscriptionPlanId = 2,
            CertificateLevelId = 3,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 3,
            PurchasedAt = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(90)
        };
        db.UserSubscriptions.AddRange(subN5, subN3);

        db.MentorCreditGrants.AddRange(
            new MentorCreditGrant
            {
                Id = 3001,
                LearnerId = learner.Id,
                UserSubscriptionId = subN5.Id,
                CertificateLevelId = 1, // N5
                InitialQuantity = 1,
                AvailableQuantity = 1,
                EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                CreatedAtUtc = DateTime.UtcNow
            },
            new MentorCreditGrant
            {
                Id = 3002,
                LearnerId = learner.Id,
                UserSubscriptionId = subN3.Id,
                CertificateLevelId = 3, // N3
                InitialQuantity = 3,
                AvailableQuantity = 3,
                EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(90),
                CreatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        // Admin configures hourly rate (200,000 VND/h) and N3 coefficient (1.25), N2 coefficient (1.45)
        claims.Role = Role.Admin;
        claims.UserId = admin.Id;

        var rateRes = await compService.ConfigureMentorHourlyRateAsync(new ConfigureMentorHourlyRateRequest
        {
            MentorUserId = mentor.Id,
            HourlyRateVnd = 200_000m,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-10)
        });
        Assert.True(rateRes.IsSuccess);

        var coeffN3Res = await compService.ConfigureJlptCoefficientAsync(new ConfigureJlptCoefficientRequest
        {
            JlptLevel = "N3",
            Coefficient = 1.25m,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-10)
        });
        Assert.True(coeffN3Res.IsSuccess);

        var coeffN2Res = await compService.ConfigureJlptCoefficientAsync(new ConfigureJlptCoefficientRequest
        {
            JlptLevel = "N2",
            Coefficient = 1.45m,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-10)
        });
        Assert.True(coeffN2Res.IsSuccess);

        // N1 qualification grant is rejected
        var n1QualRes = await compService.GrantTeachingQualificationAsync(new GrantMentorTeachingQualificationRequest
        {
            MentorUserId = mentor.Id,
            JlptLevel = "N1"
        });
        Assert.False(n1QualRes.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, n1QualRes.StatusCode);

        var uow = new UnitOfWork(db);
        var creditLedger = new MentorCreditLedgerService(uow, claims, NullLogger<MentorCreditLedgerService>.Instance);
        var bookingService = new MentorBookingService(db, uow, claims, creditLedger, settings, NullLogger<MentorBookingService>.Instance);

        // 1) Learner tries to book N3 before Mentor is granted N3 qualification -> 409 Conflict
        claims.Role = Role.Learner;
        claims.UserId = learner.Id;
        var unqualifiedBook = await bookingService.BookSlotAsync(new BookIncludedMeetingRequest
        {
            MentorAvailabilityId = slot1.Id,
            JlptLevel = "N3"
        });
        Assert.False(unqualifiedBook.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, unqualifiedBook.StatusCode);

        // 2) Admin grants Mentor N3 & N2 qualification
        claims.Role = Role.Admin;
        claims.UserId = admin.Id;
        Assert.True((await compService.GrantTeachingQualificationAsync(new GrantMentorTeachingQualificationRequest
        {
            MentorUserId = mentor.Id,
            JlptLevel = "N3"
        })).IsSuccess);
        Assert.True((await compService.GrantTeachingQualificationAsync(new GrantMentorTeachingQualificationRequest
        {
            MentorUserId = mentor.Id,
            JlptLevel = "N2"
        })).IsSuccess);

        // 3) Learner tries to book N2 using N5/N3 tickets (cross-level mismatch) -> 409 Conflict
        claims.Role = Role.Learner;
        claims.UserId = learner.Id;
        var crossLevelBook = await bookingService.BookSlotAsync(new BookIncludedMeetingRequest
        {
            MentorAvailabilityId = slot1.Id,
            JlptLevel = "N2"
        });
        Assert.False(crossLevelBook.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, crossLevelBook.StatusCode);

        // 4) Learner books N1 -> 400 BadRequest
        var n1Book = await bookingService.BookSlotAsync(new BookIncludedMeetingRequest
        {
            MentorAvailabilityId = slot1.Id,
            JlptLevel = "N1"
        });
        Assert.False(n1Book.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, n1Book.StatusCode);

        // 5) Learner books N3 -> Succeeds, deducts N3 grant (leaves N5 grant untouched), and freezes snapshot!
        var validBook = await bookingService.BookSlotAsync(new BookIncludedMeetingRequest
        {
            MentorAvailabilityId = slot1.Id,
            JlptLevel = "N3",
            Question = "Tư vấn đọc hiểu N3"
        });
        Assert.True(validBook.IsSuccess);
        Assert.Equal("N3", validBook.Result!.JlptLevel);

        var n5GrantAfter = await db.MentorCreditGrants.FindAsync(3001L);
        var n3GrantAfter = await db.MentorCreditGrants.FindAsync(3002L);
        Assert.Equal(1, n5GrantAfter!.AvailableQuantity);
        Assert.Equal(2, n3GrantAfter!.AvailableQuantity);

        var persistedBooking = await db.MentorBookings.FindAsync(validBook.Result.Id);
        Assert.NotNull(persistedBooking);
        Assert.Equal(3, persistedBooking!.CompensationLevelId);
        Assert.Equal(200_000m, persistedBooking.FrozenHourlyRate);
        Assert.Equal(1.25m, persistedBooking.FrozenLevelCoefficient);
        // 200,000 * (90/60) * 1.25 = 375,000 VND
        Assert.Equal(375_000m, persistedBooking.ScheduledCompensationAmount);
        Assert.Equal("VND", persistedBooking.CompensationCurrency);
        Assert.Equal(1, persistedBooking.CompensationFormulaVersion);

        // 6) Admin updates Mentor hourly rate for the future and revokes N3 qualification:
        //    - Frozen snapshot on persistedBooking MUST NOT change
        //    - New N3 booking on slot2 MUST be blocked (409 Conflict)
        claims.Role = Role.Admin;
        claims.UserId = admin.Id;
        var futureRateRes = await compService.ConfigureMentorHourlyRateAsync(new ConfigureMentorHourlyRateRequest
        {
            MentorUserId = mentor.Id,
            HourlyRateVnd = 350_000m,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-1)
        });
        Assert.True(futureRateRes.IsSuccess);

        var revokeRes = await compService.RevokeTeachingQualificationAsync(new RevokeMentorTeachingQualificationRequest
        {
            MentorUserId = mentor.Id,
            JlptLevel = "N3",
            Reason = "Tạm ngưng nhận lịch N3"
        });
        Assert.True(revokeRes.IsSuccess);

        var reloadedBooking = await db.MentorBookings.FindAsync(persistedBooking.Id);
        Assert.Equal(200_000m, reloadedBooking!.FrozenHourlyRate);
        Assert.Equal(375_000m, reloadedBooking.ScheduledCompensationAmount);

        claims.Role = Role.Learner;
        claims.UserId = learner.Id;
        var bookAfterRevoke = await bookingService.BookSlotAsync(new BookIncludedMeetingRequest
        {
            MentorAvailabilityId = slot2.Id,
            JlptLevel = "N3"
        });
        Assert.False(bookAfterRevoke.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, bookAfterRevoke.StatusCode);
    }

    // =========================================================================
    // 3. ATTENDANCE APPROVAL, LEGACY NULL-SNAPSHOT POLICY & DISPUTE HOLD
    // =========================================================================

    [Fact]
    public async Task AttendanceAndEarningLedger_HandlesLegacyNullSnapshot_DisputeHold_ProratedApproval_AndIdempotency()
    {
        await using var db = CreateInMemoryDb();
        var (compService, _, _, claims) = CreateCompensationStack(db);

        var admin = new UserAccount { Id = 1, Role = Role.Admin, FirstName = "Admin", LastName = "One", Email = "admin@test.com", PasswordHash = [], PasswordSalt = [] };
        var mentor = new UserAccount { Id = 10, Role = Role.Mentor, FirstName = "Kenji", LastName = "Sato", Email = "mentor@test.com", PasswordHash = [], PasswordSalt = [] };
        var learner = new UserAccount { Id = 20, Role = Role.Learner, FirstName = "Lan", LastName = "Tran", Email = "learner@test.com", PasswordHash = [], PasswordSalt = [] };
        db.Users.AddRange(admin, mentor, learner);
        db.MentorProfiles.Add(new MentorProfile
        {
            MentorId = mentor.Id,
            VerificationStatus = MentorVerificationStatus.Approved,
            Bio = "Chuyên gia luyện thi JLPT"
        });

        var sub = new UserSubscription { Id = 100, UserId = learner.Id, MentorId = mentor.Id, PaymentStatus = PaymentStatus.Paid, MeetingSessionsIncluded = 3, PurchasedAt = DateTime.UtcNow };
        db.UserSubscriptions.Add(sub);

        var avail1 = new MentorAvailability { Id = 201, MentorId = mentor.Id, StartTime = DateTime.UtcNow.AddHours(-3), EndTime = DateTime.UtcNow.AddHours(-1.5), IsApproved = true };
        var avail2 = new MentorAvailability { Id = 202, MentorId = mentor.Id, StartTime = DateTime.UtcNow.AddHours(-6), EndTime = DateTime.UtcNow.AddHours(-4.5), IsApproved = true };
        var avail3 = new MentorAvailability { Id = 203, MentorId = mentor.Id, StartTime = DateTime.UtcNow.AddHours(-9), EndTime = DateTime.UtcNow.AddHours(-7.5), IsApproved = true };
        db.MentorAvailabilities.AddRange(avail1, avail2, avail3);

        // Booking 1: Legacy booking with NULL compensation snapshot
        var legacyBooking = new MentorBooking
        {
            Id = 5001,
            UserSubscriptionId = sub.Id,
            MentorAvailabilityId = avail1.Id,
            Status = MentorStatus.Completed,
            CompensationLevelId = null,
            FrozenHourlyRate = null,
            FrozenLevelCoefficient = null,
            ScheduledCompensationAmount = null
        };

        // Booking 2: Valid N2 booking (200,000 VND/h * 1.45 = 435,000 VND for 90m)
        var validBooking = new MentorBooking
        {
            Id = 5002,
            UserSubscriptionId = sub.Id,
            MentorAvailabilityId = avail2.Id,
            Status = MentorStatus.Accepted,
            CompensationLevelId = 4, // N2
            FrozenHourlyRate = 200_000m,
            FrozenLevelCoefficient = 1.45m,
            ScheduledCompensationAmount = 435_000m,
            CompensationCurrency = "VND",
            CompensationFormulaVersion = 1
        };

        // Booking 3: Disputed booking
        var disputedBooking = new MentorBooking
        {
            Id = 5003,
            UserSubscriptionId = sub.Id,
            MentorAvailabilityId = avail3.Id,
            Status = MentorStatus.Completed,
            CompensationLevelId = 3, // N3
            FrozenHourlyRate = 200_000m,
            FrozenLevelCoefficient = 1.25m,
            ScheduledCompensationAmount = 375_000m,
            CompensationCurrency = "VND",
            CompensationFormulaVersion = 1
        };

        db.MentorBookings.AddRange(legacyBooking, validBooking, disputedBooking);
        db.MentorDisputes.Add(new MentorDispute
        {
            Id = 9001,
            MentorBookingId = disputedBooking.Id,
            LearnerId = learner.Id,
            MentorId = mentor.Id,
            Reason = "Cố vấn rời phòng sớm",
            Category = MentorDisputeCategory.Other,
            Status = MentorDisputeStatus.Submitted
        });
        await db.SaveChangesAsync();

        // A) Legacy booking evaluation -> LegacyIneligible, 0 earning entries created
        var legacyEval = await compService.EvaluateBookingAttendanceAsync(legacyBooking.Id);
        Assert.True(legacyEval.IsSuccess);
        Assert.Equal(MentorAttendanceApprovalStatus.LegacyIneligible, legacyEval.Result!.Status);
        Assert.Null(legacyEval.Result.EarningEntry);
        Assert.Empty(await db.MentorEarningEntries.Where(e => e.MentorBookingId == legacyBooking.Id).ToListAsync());

        // Attempting to force-approve legacy booking -> 409 Conflict
        var legacyForceApprove = await compService.ReviewBookingAttendanceAsync(legacyBooking.Id, new ReviewBookingAttendanceRequest
        {
            Approve = true,
            ApprovedEligibleMinutes = 90,
            Reason = "Try force approve"
        });
        Assert.False(legacyForceApprove.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, legacyForceApprove.StatusCode);

        // B) Disputed booking evaluation -> HeldDisputed, 0 earning entries created
        var disputeEval = await compService.EvaluateBookingAttendanceAsync(disputedBooking.Id);
        Assert.True(disputeEval.IsSuccess);
        Assert.Equal(MentorAttendanceApprovalStatus.HeldDisputed, disputeEval.Result!.Status);
        Assert.Null(disputeEval.Result.EarningEntry);

        // C) Incomplete attendance evidence on validBooking -> PendingAdminReview
        var incompleteEval = await compService.EvaluateBookingAttendanceAsync(validBooking.Id);
        Assert.True(incompleteEval.IsSuccess);
        Assert.Equal(MentorAttendanceApprovalStatus.PendingAdminReview, incompleteEval.Result!.Status);
        Assert.Null(incompleteEval.Result.EarningEntry);

        // Admin manual approval without reason when evidence is incomplete -> 400 BadRequest
        var manualNoReason = await compService.ReviewBookingAttendanceAsync(validBooking.Id, new ReviewBookingAttendanceRequest
        {
            Approve = true,
            ApprovedEligibleMinutes = 60,
            Reason = ""
        });
        Assert.False(manualNoReason.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, manualNoReason.StatusCode);

        // Admin manual approval with reason and 60 prorated minutes -> 200,000 * (60/60) * 1.45 = 290,000 VND
        var manualApprove = await compService.ReviewBookingAttendanceAsync(validBooking.Id, new ReviewBookingAttendanceRequest
        {
            Approve = true,
            ApprovedEligibleMinutes = 60,
            Reason = "Xác minh log phòng họp đạt 60 phút hợp lệ",
            OperationKey = "op:earning:5002"
        });
        Assert.True(manualApprove.IsSuccess);
        Assert.Equal(MentorAttendanceApprovalStatus.ApprovedForEarning, manualApprove.Result!.Status);
        Assert.NotNull(manualApprove.Result.EarningEntry);
        Assert.Equal(290_000L, manualApprove.Result.EarningEntry!.EligibleAmountVnd);
        Assert.Equal(435_000L, manualApprove.Result.EarningEntry.ScheduledAmountVnd);
        Assert.Equal("N2", manualApprove.Result.EarningEntry.JlptLevel);

        // Re-running approval is idempotent and does NOT create a second earning entry
        var duplicateApprove = await compService.ReviewBookingAttendanceAsync(validBooking.Id, new ReviewBookingAttendanceRequest
        {
            Approve = true,
            ApprovedEligibleMinutes = 60,
            Reason = "Replay",
            OperationKey = "op:earning:5002"
        });
        Assert.True(duplicateApprove.IsSuccess);
        Assert.Equal(manualApprove.Result.EarningEntry.Id, duplicateApprove.Result!.EarningEntry!.Id);
        Assert.Equal(1, await db.MentorEarningEntries.CountAsync(e => e.MentorBookingId == validBooking.Id));
    }

    // =========================================================================
    // 4. MONTHLY PAYROLL, ADJUSTMENTS, BANK ENCRYPTION & DUAL PAYOUT E2E
    // =========================================================================

    [Fact]
    public async Task PayrollAndDualPayout_EnforcesBankVerification_ManualEvidence_PayOsProcessingAndTimeoutGates_AndPreventsDoubleSettlement()
    {
        await using var db = CreateInMemoryDb();
        var (compService, payoutClient, _, claims) = CreateCompensationStack(db, qaSimulatorEnabled: true, livePayoutEnabled: false);

        var admin = new UserAccount { Id = 1, Role = Role.Admin, FirstName = "Admin", LastName = "Finance", Email = "finance@test.com", PasswordHash = [], PasswordSalt = [] };
        var mentor1 = new UserAccount { Id = 11, Role = Role.Mentor, FirstName = "Akira", LastName = "Tanaka", Email = "akira@test.com", PasswordHash = [], PasswordSalt = [] };
        var mentor2 = new UserAccount { Id = 12, Role = Role.Mentor, FirstName = "Yuki", LastName = "ito", Email = "yuki@test.com", PasswordHash = [], PasswordSalt = [] };
        var learner = new UserAccount { Id = 21, Role = Role.Learner, FirstName = "Minh", LastName = "Pham", Email = "minh@test.com", PasswordHash = [], PasswordSalt = [] };
        db.Users.AddRange(admin, mentor1, mentor2, learner);

        var sub1 = new UserSubscription { Id = 301, UserId = learner.Id, MentorId = mentor1.Id, PaymentStatus = PaymentStatus.Paid, MeetingSessionsIncluded = 3, PurchasedAt = DateTime.UtcNow };
        var sub2 = new UserSubscription { Id = 302, UserId = learner.Id, MentorId = mentor2.Id, PaymentStatus = PaymentStatus.Paid, MeetingSessionsIncluded = 3, PurchasedAt = DateTime.UtcNow };
        db.UserSubscriptions.AddRange(sub1, sub2);

        var avail1 = new MentorAvailability { Id = 401, MentorId = mentor1.Id, StartTime = DateTime.UtcNow.AddHours(-5), EndTime = DateTime.UtcNow.AddHours(-3.5), IsApproved = true };
        var avail2 = new MentorAvailability { Id = 402, MentorId = mentor2.Id, StartTime = DateTime.UtcNow.AddHours(-5), EndTime = DateTime.UtcNow.AddHours(-3.5), IsApproved = true };
        db.MentorAvailabilities.AddRange(avail1, avail2);

        var booking1 = new MentorBooking
        {
            Id = 6001,
            UserSubscriptionId = sub1.Id,
            MentorAvailabilityId = avail1.Id,
            Status = MentorStatus.Completed,
            CompensationLevelId = 3, // N3
            FrozenHourlyRate = 200_000m,
            FrozenLevelCoefficient = 1.25m,
            ScheduledCompensationAmount = 375_000m,
            CompensationCurrency = "VND",
            CompensationFormulaVersion = 1
        };
        var booking2 = new MentorBooking
        {
            Id = 6002,
            UserSubscriptionId = sub2.Id,
            MentorAvailabilityId = avail2.Id,
            Status = MentorStatus.Completed,
            CompensationLevelId = 4, // N2
            FrozenHourlyRate = 200_000m,
            FrozenLevelCoefficient = 1.45m,
            ScheduledCompensationAmount = 435_000m,
            CompensationCurrency = "VND",
            CompensationFormulaVersion = 1
        };
        db.MentorBookings.AddRange(booking1, booking2);
        await db.SaveChangesAsync();

        // Approve attendance for both bookings
        claims.Role = Role.Admin;
        claims.UserId = admin.Id;
        Assert.True((await compService.ReviewBookingAttendanceAsync(booking1.Id, new ReviewBookingAttendanceRequest
        {
            Approve = true,
            ApprovedEligibleMinutes = 90,
            Reason = "Đủ 90 phút"
        })).IsSuccess);
        Assert.True((await compService.ReviewBookingAttendanceAsync(booking2.Id, new ReviewBookingAttendanceRequest
        {
            Approve = true,
            ApprovedEligibleMinutes = 90,
            Reason = "Đủ 90 phút"
        })).IsSuccess);

        // Generate monthly payroll for 2026-11
        var genRes = await compService.GenerateMonthlyPayrollAsync(new GenerateMonthlyPayrollRequest
        {
            Year = 2026,
            Month = 11
        });
        Assert.True(genRes.IsSuccess);
        Assert.Equal(2, genRes.Result!.Count);

        var payroll1 = genRes.Result.Single(p => p.MentorUserId == mentor1.Id);
        var payroll2 = genRes.Result.Single(p => p.MentorUserId == mentor2.Id);
        Assert.Equal(375_000L, payroll1.GrossPayableAmountVnd);
        Assert.Equal(435_000L, payroll2.GrossPayableAmountVnd);

        // Negative adjustment that exceeds total earnings is rejected (400 BadRequest)
        var excessiveNegAdj = await compService.AddPayrollAdjustmentAsync(payroll1.Id, new CreatePayrollAdjustmentRequest
        {
            AmountVnd = -400_000L,
            Reason = "Khấu trừ vượt mức"
        });
        Assert.False(excessiveNegAdj.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, excessiveNegAdj.StatusCode);

        // Valid +25,000 VND adjustment on payroll1 -> GrossPayableAmountVnd becomes 400,000 VND
        var validAdj = await compService.AddPayrollAdjustmentAsync(payroll1.Id, new CreatePayrollAdjustmentRequest
        {
            AmountVnd = 25_000L,
            Reason = "Bù phụ cấp buổi tư vấn kỹ thuật",
            OperationKey = "adj:payroll1:bonus"
        });
        Assert.True(validAdj.IsSuccess);

        // Approve both payrolls -> creates payout obligations
        var approve1 = await compService.ApproveMonthlyPayrollAsync(payroll1.Id, new ApproveMonthlyPayrollRequest
        {
            DefaultPayoutMethod = PayoutExecutionMethod.ManualBankTransfer,
            Notes = "Duyệt chi kỳ 11/2026"
        });
        var approve2 = await compService.ApproveMonthlyPayrollAsync(payroll2.Id, new ApproveMonthlyPayrollRequest
        {
            DefaultPayoutMethod = PayoutExecutionMethod.PayOsPayout,
            Notes = "Duyệt chi payOS kỳ 11/2026"
        });
        Assert.True(approve1.IsSuccess);
        Assert.True(approve2.IsSuccess);
        Assert.Equal(400_000L, approve1.Result!.GrossPayableAmountVnd);
        Assert.NotNull(approve1.Result.PayoutObligationId);
        Assert.NotNull(approve2.Result!.PayoutObligationId);

        var ob1Id = approve1.Result.PayoutObligationId!.Value;
        var ob2Id = approve2.Result.PayoutObligationId!.Value;

        // Adjustment after payroll is Approved is rejected (409 Conflict)
        var adjAfterApprove = await compService.AddPayrollAdjustmentAsync(payroll1.Id, new CreatePayrollAdjustmentRequest
        {
            AmountVnd = 10_000L,
            Reason = "Không được sửa bảng lương đã duyệt"
        });
        Assert.False(adjAfterApprove.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, adjAfterApprove.StatusCode);

        // Mentor1 registers bank account -> encrypted at rest, masked in response, PendingVerification
        claims.Role = Role.Mentor;
        claims.UserId = mentor1.Id;
        var bank1v1 = await compService.UpsertMyBankAccountAsync(new UpsertMentorBankAccountRequest
        {
            BankBin = "970436",
            BankShortName = "VCB",
            AccountNumber = "0123456789",
            AccountHolderName = "Akira Tanaka"
        });
        Assert.True(bank1v1.IsSuccess);
        Assert.Equal("****6789", bank1v1.Result!.MaskedAccountNumber);
        Assert.Equal(1, bank1v1.Result.AccountVersion);
        Assert.Equal(MentorBankVerificationStatus.PendingVerification, bank1v1.Result.VerificationStatus);

        var rawBankRow = await db.MentorBankAccounts.FindAsync(bank1v1.Result.Id);
        Assert.NotEqual("0123456789", rawBankRow!.EncryptedAccountNumber);
        Assert.DoesNotContain("0123456789", rawBankRow.EncryptedAccountNumber);

        // Mentor1 updates bank account -> version 1 becomes Superseded, version 2 becomes Current (PendingVerification)
        var bank1v2 = await compService.UpsertMyBankAccountAsync(new UpsertMentorBankAccountRequest
        {
            BankBin = "970436",
            BankShortName = "VCB",
            AccountNumber = "999988887777",
            AccountHolderName = "Akira Tanaka"
        });
        Assert.True(bank1v2.IsSuccess);
        Assert.Equal(2, bank1v2.Result!.AccountVersion);
        Assert.Equal("****7777", bank1v2.Result.MaskedAccountNumber);

        var allBanksMentor1 = await compService.GetMyBankAccountsAsync();
        Assert.Equal(2, allBanksMentor1.Result!.Count);
        Assert.Contains(allBanksMentor1.Result, b => b.AccountVersion == 1 && !b.IsCurrent && b.VerificationStatus == MentorBankVerificationStatus.Superseded);

        // Mentor2 registers bank account
        claims.UserId = mentor2.Id;
        var bank2 = await compService.UpsertMyBankAccountAsync(new UpsertMentorBankAccountRequest
        {
            BankBin = "970422",
            BankShortName = "MB",
            AccountNumber = "1122334455",
            AccountHolderName = "Yuki Ito"
        });
        Assert.True(bank2.IsSuccess);

        // Attempting payout while bank account is PendingVerification -> 409 Conflict
        claims.Role = Role.Admin;
        claims.UserId = admin.Id;
        var unverifiedInit = await compService.InitiateManualPayoutAsync(ob1Id, new InitiateManualPayoutRequest());
        Assert.False(unverifiedInit.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, unverifiedInit.StatusCode);

        // Admin verifies both current bank accounts
        Assert.True((await compService.VerifyMentorBankAccountAsync(bank1v2.Result.Id, new VerifyMentorBankAccountRequest
        {
            Approve = true,
            Notes = "Đã đối chiếu tên chủ TK"
        })).IsSuccess);
        Assert.True((await compService.VerifyMentorBankAccountAsync(bank2.Result!.Id, new VerifyMentorBankAccountRequest
        {
            Approve = true,
            Notes = "Đã đối chiếu tên chủ TK"
        })).IsSuccess);

        // --- OBLIGATION 1: MANUAL BANK TRANSFER ---
        var manualInit = await compService.InitiateManualPayoutAsync(ob1Id, new InitiateManualPayoutRequest
        {
            Notes = "Chuyển khoản VCB kỳ 11/2026"
        });
        Assert.True(manualInit.IsSuccess);
        Assert.Equal(MentorPayoutObligationStatus.Processing, manualInit.Result!.Status);

        // Confirm without EvidenceStorageKey -> 400 BadRequest (never mark Paid without evidence!)
        var confirmNoEvidence = await compService.ConfirmManualPayoutAsync(ob1Id, new ConfirmManualPayoutRequest
        {
            TransferReference = "FT2630899991",
            EvidenceStorageKey = ""
        });
        Assert.False(confirmNoEvidence.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, confirmNoEvidence.StatusCode);

        // Confirm with both TransferReference and EvidenceStorageKey -> Paid!
        var confirmOk = await compService.ConfirmManualPayoutAsync(ob1Id, new ConfirmManualPayoutRequest
        {
            TransferReference = "FT2630899991",
            EvidenceStorageKey = "payout-receipts/2026-11/ob1-ft2630899991.pdf",
            Notes = "Đã ủy nhiệm chi thành công"
        });
        Assert.True(confirmOk.IsSuccess);
        Assert.Equal(MentorPayoutObligationStatus.Paid, confirmOk.Result!.Status);
        Assert.Equal(400_000L, confirmOk.Result.PaidAmountVnd);

        // Replay confirmation with same TransferReference -> idempotent 200 OK
        var confirmReplay = await compService.ConfirmManualPayoutAsync(ob1Id, new ConfirmManualPayoutRequest
        {
            TransferReference = "FT2630899991",
            EvidenceStorageKey = "payout-receipts/2026-11/ob1-ft2630899991.pdf"
        });
        Assert.True(confirmReplay.IsSuccess);
        Assert.Single(confirmReplay.Result!.Attempts);

        // Attempting payOS payout on already-Paid obligation 1 -> 409 Conflict (cross-method double-payout blocked)
        var crossPayOsOnPaid = await compService.ExecutePayOsPayoutAsync(ob1Id, new ExecutePayOsPayoutRequest());
        Assert.False(crossPayOsOnPaid.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, crossPayOsOnPaid.StatusCode);

        // --- OBLIGATION 2: PAYOS PAYOUT (TIMEOUT -> RECONCILE FAILED -> PROCESSING -> RECONCILE COMPLETED) ---
        // 1) Simulate TIMEOUT -> UnknownPendingReconciliation
        var payOsTimeout = await compService.ExecutePayOsPayoutAsync(ob2Id, new ExecutePayOsPayoutRequest
        {
            QaSimulatedOutcome = "TIMEOUT"
        });
        Assert.True(payOsTimeout.IsSuccess);
        Assert.Equal(MentorPayoutObligationStatus.UnknownPendingReconciliation, payOsTimeout.Result!.Status);

        // While UnknownPendingReconciliation, retry and manual confirmation are both blocked (409 Conflict)
        var retryWhileUnknown = await compService.ExecutePayOsPayoutAsync(ob2Id, new ExecutePayOsPayoutRequest
        {
            QaSimulatedOutcome = "COMPLETED"
        });
        Assert.False(retryWhileUnknown.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, retryWhileUnknown.StatusCode);

        var manualWhileUnknown = await compService.ConfirmManualPayoutAsync(ob2Id, new ConfirmManualPayoutRequest
        {
            TransferReference = "FT_SWITCH_BLOCKED",
            EvidenceStorageKey = "receipts/blocked.pdf"
        });
        Assert.False(manualWhileUnknown.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, manualWhileUnknown.StatusCode);

        // 2) Reconcile TIMEOUT as FAILED -> transitions to FailedRetryable
        var reconcileFailed = await compService.ReconcilePayoutObligationAsync(ob2Id, new ReconcilePayoutObligationRequest
        {
            ProviderFinalState = "FAILED",
            Reason = "Cổng payOS xác nhận chưa phát sinh lệnh chi"
        });
        Assert.True(reconcileFailed.IsSuccess);
        Assert.Equal(MentorPayoutObligationStatus.FailedRetryable, reconcileFailed.Result!.Status);

        // 3) Execute payOS Payout with HTTP 200 PROCESSING -> stays Processing, NEVER Paid!
        var payOsProcessing = await compService.ExecutePayOsPayoutAsync(ob2Id, new ExecutePayOsPayoutRequest
        {
            QaSimulatedOutcome = "PROCESSING"
        });
        Assert.True(payOsProcessing.IsSuccess);
        Assert.Equal(MentorPayoutObligationStatus.Processing, payOsProcessing.Result!.Status);
        Assert.NotEqual(MentorPayoutObligationStatus.Paid, payOsProcessing.Result.Status);

        // 4) Reconcile PROCESSING to COMPLETED -> settles Obligation 2, Payroll 2, and Earning 2 to Paid!
        var reconcileCompleted = await compService.ReconcilePayoutObligationAsync(ob2Id, new ReconcilePayoutObligationRequest
        {
            ProviderFinalState = "COMPLETED",
            ProviderTransactionId = "payos_tx_settled_6002",
            Reason = "Đối soát webhook/API payOS hoàn tất"
        });
        Assert.True(reconcileCompleted.IsSuccess);
        Assert.Equal(MentorPayoutObligationStatus.Paid, reconcileCompleted.Result!.Status);
        Assert.Equal(435_000L, reconcileCompleted.Result.PaidAmountVnd);

        // Verify both payrolls and earnings are now Paid
        var finalPayroll1 = await db.MentorPayrollPeriods.FindAsync(payroll1.Id);
        var finalPayroll2 = await db.MentorPayrollPeriods.FindAsync(payroll2.Id);
        Assert.Equal(MentorPayrollStatus.Paid, finalPayroll1!.Status);
        Assert.Equal(MentorPayrollStatus.Paid, finalPayroll2!.Status);

        var allEarnings = await db.MentorEarningEntries.ToListAsync();
        Assert.All(allEarnings, e => Assert.Equal(MentorEarningStatus.Paid, e.Status));

        // Verify PayOsPayoutClient HMAC-SHA256 signature determinism & live safety lock
        var sig1 = PayOsPayoutClient.ComputeSignature(
            new Dictionary<string, string> { ["b"] = "2", ["a"] = "hello" },
            "secret123");
        var sig2 = PayOsPayoutClient.ComputeSignature(
            new Dictionary<string, string> { ["a"] = "hello", ["b"] = "2" },
            "secret123");
        Assert.Equal(sig1, sig2);
        Assert.True(PayOsPayoutClient.VerifySignature(
            new Dictionary<string, string> { ["a"] = "hello", ["b"] = "2" },
            "secret123",
            sig1));
        Assert.NotEqual(sig1, PayOsPayoutClient.ComputeSignature(
            new Dictionary<string, string> { ["a"] = "tampered", ["b"] = "2" },
            "secret123"));
    }

    // =========================================================================
    // 5. ROLE AUTHORIZATION & LIVE PAYOS SAFETY GATE
    // =========================================================================

    [Fact]
    public async Task RoleAuthorization_BlocksLearnerAndCrossMentorAccess_AndBlocksLivePayOsWhenDisabled()
    {
        await using var db = CreateInMemoryDb();
        var (compService, payoutClient, _, claims) = CreateCompensationStack(db, qaSimulatorEnabled: false, livePayoutEnabled: false);

        // Learner is forbidden from all compensation endpoints
        claims.Role = Role.Learner;
        claims.UserId = 99;
        Assert.Equal((int)HttpStatusCode.Forbidden, (await compService.GetMentorHourlyRatesAsync()).StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, (await compService.GetJlptCoefficientsAsync()).StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, (await compService.GetMentorEarningsAsync()).StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, (await compService.GetPayrollPeriodsAsync()).StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, (await compService.GetPayoutObligationsAsync()).StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, (await compService.GetMyCompensationOverviewAsync()).StatusCode);

        // Mentor cannot view another Mentor's data or call Admin write endpoints
        claims.Role = Role.Mentor;
        claims.UserId = 10;
        Assert.Equal((int)HttpStatusCode.Forbidden, (await compService.GetMentorHourlyRatesAsync(mentorUserId: 11)).StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, (await compService.GetMentorEarningsAsync(mentorUserId: 11)).StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, (await compService.GetPayrollPeriodsAsync(mentorUserId: 11)).StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, (await compService.GetPayoutObligationsAsync(mentorUserId: 11)).StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, (await compService.ConfigureJlptCoefficientAsync(new ConfigureJlptCoefficientRequest { JlptLevel = "N3", Coefficient = 1.25m })).StatusCode);

        // Live payOS safety gate: when QaSimulatorEnabled=false and LivePayoutEnabled=false, CreateSinglePayoutAsync throws InvalidOperationException without network call
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            payoutClient.CreateSinglePayoutAsync(new PayOsSinglePayoutCommand(
                ReferenceId: "ref_live_blocked",
                AmountVnd: 300_000,
                Description: "Blocked live call",
                ToBin: "970436",
                ToAccountNumber: "0123456789",
                Categories: ["salary"],
                IdempotencyKey: "idemp_live_blocked")));
        Assert.Contains("LivePayoutEnabled", ex.Message);
    }

    // =========================================================================
    // 6. PAYOS BATCH PAYOUT, CREDIT ESTIMATE, BALANCE & OVERLAP REJECTION
    // =========================================================================

    [Fact]
    public async Task PayOsBatchPayout_AndEffectiveDatedOverlapValidation_WorkAsSpecified()
    {
        await using var db = CreateInMemoryDb();
        var (compService, _, _, claims) = CreateCompensationStack(db, qaSimulatorEnabled: true, livePayoutEnabled: false);

        var admin = new UserAccount { Id = 1, Role = Role.Admin, FirstName = "Admin", LastName = "One", Email = "admin@test.com", PasswordHash = [], PasswordSalt = [] };
        var mentor = new UserAccount { Id = 15, Role = Role.Mentor, FirstName = "Hana", LastName = "Sato", Email = "hana@test.com", PasswordHash = [], PasswordSalt = [] };
        var learner = new UserAccount { Id = 25, Role = Role.Learner, FirstName = "Tuan", LastName = "Le", Email = "tuan@test.com", PasswordHash = [], PasswordSalt = [] };
        db.Users.AddRange(admin, mentor, learner);
        await db.SaveChangesAsync();

        // Bounded rate window [T-20d, T-10d]
        var t0 = DateTime.UtcNow.Date;
        var boundedRate = await compService.ConfigureMentorHourlyRateAsync(new ConfigureMentorHourlyRateRequest
        {
            MentorUserId = mentor.Id,
            HourlyRateVnd = 180_000m,
            EffectiveFromUtc = t0.AddDays(-20),
            EffectiveToUtc = t0.AddDays(-10)
        });
        Assert.True(boundedRate.IsSuccess);

        // Overlapping window [T-15d, T-5d] -> 409 Conflict
        var overlappingRate = await compService.ConfigureMentorHourlyRateAsync(new ConfigureMentorHourlyRateRequest
        {
            MentorUserId = mentor.Id,
            HourlyRateVnd = 190_000m,
            EffectiveFromUtc = t0.AddDays(-15),
            EffectiveToUtc = t0.AddDays(-5)
        });
        Assert.False(overlappingRate.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, overlappingRate.StatusCode);

        // Estimate credit & balance via QA simulator
        var balanceRes = await compService.GetPayOsPayoutBalanceAsync();
        Assert.True(balanceRes.IsSuccess);
        Assert.True(balanceRes.Result!.IsQaSimulated);

        var estimateRes = await compService.EstimatePayOsBatchCreditAsync([]);
        Assert.False(estimateRes.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, estimateRes.StatusCode);

        // Seed two verified obligations and execute batch payOS payout (one COMPLETED, one PROCESSING)
        claims.Role = Role.Mentor;
        claims.UserId = mentor.Id;
        var bankRes = await compService.UpsertMyBankAccountAsync(new UpsertMentorBankAccountRequest
        {
            BankBin = "970436",
            BankShortName = "VCB",
            AccountNumber = "888877776666",
            AccountHolderName = "Hana Sato"
        });
        Assert.True(bankRes.IsSuccess);

        claims.Role = Role.Admin;
        claims.UserId = admin.Id;
        Assert.True((await compService.VerifyMentorBankAccountAsync(bankRes.Result!.Id, new VerifyMentorBankAccountRequest
        {
            Approve = true,
            Notes = "Verified"
        })).IsSuccess);

        var p1 = new MentorPayrollPeriod
        {
            Id = 701,
            MentorUserId = mentor.Id,
            Year = 2026,
            Month = 10,
            SequenceNumber = 1,
            Currency = "VND",
            Status = MentorPayrollStatus.Approved,
            TotalEarningsAmount = 300_000m,
            TotalAdjustmentsAmount = 0m,
            GrossPayableAmount = 300_000m,
            PreparedByAdminId = admin.Id,
            ApprovedByAdminId = admin.Id,
            PreparedAtUtc = DateTime.UtcNow,
            ApprovedAtUtc = DateTime.UtcNow
        };
        var p2 = new MentorPayrollPeriod
        {
            Id = 702,
            MentorUserId = mentor.Id,
            Year = 2026,
            Month = 11,
            SequenceNumber = 1,
            Currency = "VND",
            Status = MentorPayrollStatus.Approved,
            TotalEarningsAmount = 450_000m,
            TotalAdjustmentsAmount = 0m,
            GrossPayableAmount = 450_000m,
            PreparedByAdminId = admin.Id,
            ApprovedByAdminId = admin.Id,
            PreparedAtUtc = DateTime.UtcNow,
            ApprovedAtUtc = DateTime.UtcNow
        };
        db.MentorPayrollPeriods.AddRange(p1, p2);

        var obA = new MentorPayoutObligation
        {
            Id = 801,
            PayrollPeriodId = p1.Id,
            MentorUserId = mentor.Id,
            LockedBankAccountId = bankRes.Result.Id,
            PayableAmount = 300_000m,
            Currency = "VND",
            SelectedMethod = PayoutExecutionMethod.PayOsPayout,
            Status = MentorPayoutObligationStatus.PendingExecution,
            ApprovedByAdminId = admin.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var obB = new MentorPayoutObligation
        {
            Id = 802,
            PayrollPeriodId = p2.Id,
            MentorUserId = mentor.Id,
            LockedBankAccountId = bankRes.Result.Id,
            PayableAmount = 450_000m,
            Currency = "VND",
            SelectedMethod = PayoutExecutionMethod.PayOsPayout,
            Status = MentorPayoutObligationStatus.PendingExecution,
            ApprovedByAdminId = admin.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.MentorPayoutObligations.AddRange(obA, obB);
        await db.SaveChangesAsync();

        var validEstimate = await compService.EstimatePayOsBatchCreditAsync([obA.Id, obB.Id]);
        Assert.True(validEstimate.IsSuccess);
        Assert.Equal(750_000L, validEstimate.Result!.TotalAmountVnd);
        Assert.True(validEstimate.Result.TotalCreditRequiredVnd >= 750_000L);

        var batchExec = await compService.ExecutePayOsBatchPayoutAsync(new ExecutePayOsBatchPayoutRequest
        {
            ObligationIds = [obA.Id, obB.Id],
            Description = "Batch payout QA",
            QaSimulatedOutcomesByObligation = new Dictionary<long, string>
            {
                [obA.Id] = "COMPLETED",
                [obB.Id] = "PROCESSING"
            }
        });
        Assert.True(batchExec.IsSuccess);
        Assert.Equal(2, batchExec.Result!.Obligations.Count);

        var resObA = batchExec.Result.Obligations.Single(o => o.Id == obA.Id);
        var resObB = batchExec.Result.Obligations.Single(o => o.Id == obB.Id);
        Assert.Equal(MentorPayoutObligationStatus.Paid, resObA.Status);
        Assert.Equal(MentorPayoutObligationStatus.Processing, resObB.Status);
    }

    // =========================================================================
    // 7. RESCHEDULE SNAPSHOT PRESERVATION & FINANCIAL CONCURRENCY / IDEMPOTENCY
    // =========================================================================

    [Fact]
    public async Task RescheduleAndFinancialConcurrency_PreservesFrozenSnapshot_AndPreventsDuplicateEarningsOrPayouts()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var db = CreateInMemoryDb(dbName);
        var (compService, _, _, claims) = CreateCompensationStack(db, qaSimulatorEnabled: true, livePayoutEnabled: false);

        var admin = new UserAccount { Id = 1, Role = Role.Admin, FirstName = "Admin", LastName = "One", Email = "admin@test.com", PasswordHash = [], PasswordSalt = [] };
        var mentor = new UserAccount { Id = 18, Role = Role.Mentor, FirstName = "Daiki", LastName = "Mori", Email = "daiki@test.com", PasswordHash = [], PasswordSalt = [] };
        var learner = new UserAccount { Id = 28, Role = Role.Learner, FirstName = "Linh", LastName = "Vo", Email = "linh@test.com", PasswordHash = [], PasswordSalt = [] };
        db.Users.AddRange(admin, mentor, learner);

        var sub = new UserSubscription
        {
            Id = 900,
            UserId = learner.Id,
            MentorId = mentor.Id,
            CertificateLevelId = 4, // N2
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 3,
            PurchasedAt = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(90)
        };
        db.UserSubscriptions.Add(sub);

        var slot1 = new MentorAvailability
        {
            Id = 910,
            MentorId = mentor.Id,
            StartTime = DateTime.UtcNow.AddDays(3),
            EndTime = DateTime.UtcNow.AddDays(3).AddMinutes(90),
            MaxLearners = 1,
            BookedCount = 1,
            IsApproved = true
        };
        var slot2 = new MentorAvailability
        {
            Id = 911,
            MentorId = mentor.Id,
            StartTime = DateTime.UtcNow.AddDays(5),
            EndTime = DateTime.UtcNow.AddDays(5).AddMinutes(90),
            MaxLearners = 1,
            BookedCount = 0,
            IsApproved = true
        };
        db.MentorAvailabilities.AddRange(slot1, slot2);

        var booking = new MentorBooking
        {
            Id = 950,
            UserSubscriptionId = sub.Id,
            MentorAvailabilityId = slot1.Id,
            ScheduledAt = slot1.StartTime,
            Status = MentorStatus.Assigned,
            CompensationLevelId = 4, // N2
            FrozenHourlyRate = 220_000m,
            FrozenLevelCoefficient = 1.45m,
            ScheduledCompensationAmount = 478_500m,
            CompensationCurrency = "VND",
            CompensationFormulaVersion = 1
        };
        db.MentorBookings.Add(booking);
        await db.SaveChangesAsync();

        // Grant N2 qualification, then reschedule -> succeeds and preserves frozen snapshot
        claims.Role = Role.Admin;
        claims.UserId = admin.Id;
        Assert.True((await compService.GrantTeachingQualificationAsync(new GrantMentorTeachingQualificationRequest
        {
            MentorUserId = mentor.Id,
            JlptLevel = "N2"
        })).IsSuccess);

        var uow = new UnitOfWork(db);
        var creditLedger = new MentorCreditLedgerService(uow, claims, NullLogger<MentorCreditLedgerService>.Instance);
        var attendanceMock = new Mock<IMentorAttendanceService>();
        var cancelService = new MentorCancellationService(db, claims, creditLedger, attendanceMock.Object, NullLogger<MentorCancellationService>.Instance);

        claims.Role = Role.Learner;
        claims.UserId = learner.Id;
        var reschedOk = await cancelService.RescheduleBookingAsync(booking.Id, new RescheduleMentorBookingRequest
        {
            NewSlotId = slot2.Id,
            Reason = "Dời sang thứ Sáu"
        });
        Assert.True(reschedOk.IsSuccess);

        var afterResched = await db.MentorBookings.FindAsync(booking.Id);
        Assert.Equal(slot2.Id, afterResched!.MentorAvailabilityId);
        Assert.Equal(4, afterResched.CompensationLevelId);
        Assert.Equal(220_000m, afterResched.FrozenHourlyRate);
        Assert.Equal(1.45m, afterResched.FrozenLevelCoefficient);
        Assert.Equal(478_500m, afterResched.ScheduledCompensationAmount);

        // Concurrent earning approval with same OperationKey across parallel calls -> only 1 earning entry persisted
        claims.Role = Role.Admin;
        claims.UserId = admin.Id;
        var concurrentReviews = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(async _ =>
            {
                await using var isolatedDb = CreateInMemoryDb(dbName);
                var (isolatedComp, _, _, _) = CreateCompensationStack(isolatedDb, qaSimulatorEnabled: true, livePayoutEnabled: false);
                return await isolatedComp.ReviewBookingAttendanceAsync(booking.Id, new ReviewBookingAttendanceRequest
                {
                    Approve = true,
                    ApprovedEligibleMinutes = 90,
                    Reason = "Concurrent review",
                    OperationKey = "concurrent:earning:950"
                });
            }));

        Assert.All(concurrentReviews, r => Assert.True(r.IsSuccess));
        var earningCount = await db.MentorEarningEntries.CountAsync(e => e.MentorBookingId == booking.Id);
        Assert.Equal(1, earningCount);

        // Prepare payroll, bank account, and obligation, then run 5 concurrent payOS payout requests
        claims.Role = Role.Mentor;
        claims.UserId = mentor.Id;
        var bank = await compService.UpsertMyBankAccountAsync(new UpsertMentorBankAccountRequest
        {
            BankBin = "970436",
            BankShortName = "VCB",
            AccountNumber = "555566667777",
            AccountHolderName = "Daiki Mori"
        });
        Assert.True(bank.IsSuccess);

        claims.Role = Role.Admin;
        claims.UserId = admin.Id;
        Assert.True((await compService.VerifyMentorBankAccountAsync(bank.Result!.Id, new VerifyMentorBankAccountRequest
        {
            Approve = true,
            Notes = "Verified"
        })).IsSuccess);

        var genPayroll = await compService.GenerateMonthlyPayrollAsync(new GenerateMonthlyPayrollRequest
        {
            Year = 2026,
            Month = 11,
            MentorUserId = mentor.Id
        });
        Assert.True(genPayroll.IsSuccess);
        var approvePayroll = await compService.ApproveMonthlyPayrollAsync(genPayroll.Result!.Single().Id, new ApproveMonthlyPayrollRequest
        {
            DefaultPayoutMethod = PayoutExecutionMethod.PayOsPayout
        });
        Assert.True(approvePayroll.IsSuccess);
        var obId = approvePayroll.Result!.PayoutObligationId!.Value;

        var concurrentPayouts = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(async _ =>
            {
                await using var isolatedDb = CreateInMemoryDb(dbName);
                var (isolatedComp, _, _, _) = CreateCompensationStack(isolatedDb, qaSimulatorEnabled: true, livePayoutEnabled: false);
                return await isolatedComp.ExecutePayOsPayoutAsync(obId, new ExecutePayOsPayoutRequest
                {
                    IdempotencyKey = "concurrent:payos:ob950",
                    QaSimulatedOutcome = "COMPLETED"
                });
            }));

        Assert.All(concurrentPayouts, r => Assert.True(r.IsSuccess));
        var succeededAttemptCount = await db.MentorPayoutAttempts
            .CountAsync(a => a.PayoutObligationId == obId && a.InternalStatus == MentorPayoutAttemptStatus.Succeeded);
        Assert.Equal(1, succeededAttemptCount);
    }

    // =========================================================================
    // 8. CANONICAL OPENAPI / SWAGGER DOCUMENT GENERATION & VALIDATION
    // =========================================================================

    [Fact]
    public void OpenApiContract_GeneratesValidCanonicalDocument_WithAllMentorCompensationRoutes()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(RikiPath.WebApi.Controllers.AdminMentorCompensationController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "RikiPath API",
                Version = "v1",
                Description = "API RikiPath sử dụng các đường dẫn nghiệp vụ chuẩn, không gắn phiên bản."
            });
            c.DocumentFilter<RikiPath.WebApi.Swagger.CanonicalApiDocumentFilter>();
            c.OperationFilter<RikiPath.WebApi.Swagger.CanonicalApiOperationFilter>();
            c.SchemaFilter<RikiPath.WebApi.Swagger.CanonicalApiSchemaFilter>();
        });

        using var app = builder.Build();
        app.MapControllers();

        var swaggerProvider = app.Services.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>();
        var doc = swaggerProvider.GetSwagger("v1");

        Assert.NotNull(doc);
        Assert.Contains("/api/admin/mentor-compensation/hourly-rates", doc.Paths.Keys);
        Assert.Contains("/api/admin/mentor-compensation/jlpt-coefficients", doc.Paths.Keys);
        Assert.Contains("/api/admin/mentor-compensation/qualifications", doc.Paths.Keys);
        Assert.Contains("/api/admin/mentor-compensation/payrolls/generate", doc.Paths.Keys);
        Assert.Contains("/api/admin/mentor-compensation/payouts", doc.Paths.Keys);
        Assert.Contains("/api/admin/mentor-compensation/payouts/payos/batch", doc.Paths.Keys);
        Assert.Contains("/api/mentor/compensation/overview", doc.Paths.Keys);
        Assert.Contains("/api/mentor/compensation/bank-accounts", doc.Paths.Keys);

        var json = Microsoft.OpenApi.Extensions.OpenApiSerializableExtensions.SerializeAsJson(
            doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0);
        System.IO.File.WriteAllText(@"C:\Temp\rikipath-mentor-compensation-openapi-20261104.json", json);
    }
}
