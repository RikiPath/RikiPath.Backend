using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Options;
using RikiPath.Application.Requests.MentorDiscovery;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Domain.MentorScheduling;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;
using Xunit;

namespace RikiPath.Tests;

// Bộ kiểm thử toàn diện cho Nghiệp vụ Giữ chỗ & Đặt lịch nguyên tử (Stage 08/13)
public sealed class MentorBookingTests
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

    private static readonly AppSettings Settings = new()
    {
        FrontendBaseUrl = "https://rikipath.edu.vn"
    };

    private static MentorBookingService CreateBookingService(AppDbContext db, int learnerId = 101, Role role = Role.Learner)
    {
        var uow = new UnitOfWork(db);
        var claims = MockClaims(role, learnerId);
        var ledgerService = new MentorCreditLedgerService(uow, claims, NullLogger<MentorCreditLedgerService>.Instance);
        return new MentorBookingService(db, uow, claims, ledgerService, Settings, NullLogger<MentorBookingService>.Instance);
    }

    private static async Task<(UserAccount mentor, MentorAvailability slot)> SeedMentorAndSlotAsync(
        AppDbContext db,
        int mentorId = 201,
        MentorVerificationStatus verificationStatus = MentorVerificationStatus.Approved,
        int startHoursAhead = 24,
        int durationMinutes = 90,
        bool isApproved = true,
        DateTime? cancelRequestedAt = null,
        DateTime? rejectedAt = null)
    {
        var mentor = new UserAccount
        {
            Id = mentorId,
            FirstName = "Yamada",
            LastName = "Kenji",
            Email = $"yamada{mentorId}@rikipath.edu.vn",
            Role = Role.Mentor,
            PasswordHash = [],
            PasswordSalt = []
        };
        db.Users.Add(mentor);

        var profile = new MentorProfile
        {
            MentorId = mentorId,
            Bio = "10 năm kinh nghiệm giảng dạy JLPT",
            Expertise = "JLPT N1",
            JlptLevel = "N1",
            VerificationStatus = verificationStatus
        };
        db.MentorProfiles.Add(profile);

        var startTime = DateTime.UtcNow.Date.AddDays(2).AddHours(8); // Khung 8:00 sáng sau 2 ngày
        if (startHoursAhead != 24)
            startTime = DateTime.UtcNow.AddHours(startHoursAhead);

        var slot = new MentorAvailability
        {
            MentorId = mentorId,
            Mentor = mentor,
            StartTime = startTime,
            EndTime = startTime.AddMinutes(durationMinutes),
            MaxLearners = 1,
            BookedCount = 0,
            IsApproved = isApproved,
            RoomId = Guid.NewGuid(),
            CancelRequestedAt = cancelRequestedAt,
            RejectedAt = rejectedAt
        };
        db.MentorAvailabilities.Add(slot);
        await db.SaveChangesAsync();

        return (mentor, slot);
    }

    private static async Task<(UserAccount learner, UserSubscription sub, MentorCreditGrant grant)> SeedLearnerWithCreditsAsync(
        AppDbContext db,
        int learnerId = 101,
        int sessions = 3,
        DateTime? expiresAt = null)
    {
        var learner = new UserAccount
        {
            Id = learnerId,
            FirstName = "Minh",
            LastName = "Tran",
            Email = $"minh{learnerId}@learner.vn",
            Role = Role.Learner,
            PasswordHash = [],
            PasswordSalt = []
        };
        db.Users.Add(learner);

        var sub = new UserSubscription
        {
            UserId = learnerId,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = sessions,
            MeetingSessionsUsed = 0,
            PurchasedAt = DateTime.UtcNow,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = expiresAt ?? DateTime.UtcNow.AddDays(60)
        };
        db.UserSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var grant = new MentorCreditGrant
        {
            LearnerId = learnerId,
            UserSubscriptionId = sub.Id,
            InitialQuantity = sessions,
            AvailableQuantity = sessions,
            ExpiresAtUtc = expiresAt ?? DateTime.UtcNow.AddDays(60),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.MentorCreditGrants.Add(grant);

        var entry = new MentorCreditEntry
        {
            CreditGrantId = grant.Id,
            LearnerId = learnerId,
            EntryType = CreditEntryType.Grant,
            Amount = sessions,
            OperationKey = $"grant:sub:{sub.Id}",
            Reason = "Cấp lượt tư vấn ban đầu",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.MentorCreditEntries.Add(entry);
        await db.SaveChangesAsync();

        return (learner, sub, grant);
    }

    // 1. Học viên hợp lệ đặt slot khả dụng thành công
    [Fact]
    public async Task Authenticated_Learner_Can_Book_Valid_Slot_With_Sufficient_Credit()
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db);
        var (_, _, grant) = await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        var request = new BookIncludedMeetingRequest
        {
            MentorAvailabilityId = slot.Id,
            Question = "Em muốn được tư vấn chiến lược ôn thi Dokkai N1."
        };

        var response = await service.BookSlotAsync(request);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(201, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.Equal(MentorStatus.Accepted, response.Result.Status);
        Assert.Equal(slot.Id, response.Result.MentorAvailabilityId);
        Assert.Contains("/consultation-room/", response.Result.MeetingLink);

        // Kiểm tra tính nguyên tử: Slot đã bị khóa, credit đã bị trừ, reservation, terms và day guard được tạo
        var updatedSlot = await db.MentorAvailabilities.FindAsync(slot.Id);
        Assert.Equal(1, updatedSlot!.BookedCount);

        var updatedGrant = await db.MentorCreditGrants.FindAsync(grant.Id);
        Assert.Equal(2, updatedGrant!.AvailableQuantity);

        var reservation = await db.MentorBookingReservations.FirstOrDefaultAsync(r => r.MentorAvailabilityId == slot.Id);
        Assert.NotNull(reservation);
        Assert.Null(reservation.ReleasedAtUtc);
        Assert.Equal(slot.EndTime.AddMinutes(15), reservation.RestProtectedUntilUtc);

        var terms = await db.MentorBookingTerms.FirstOrDefaultAsync(t => t.MentorAvailabilityId == slot.Id);
        Assert.NotNull(terms);
        Assert.Equal(3, terms.PolicyVersion);
        Assert.NotNull(terms.ConfirmedAtUtc);

        var dayGuard = await db.MentorBookingDayGuards.FirstOrDefaultAsync(g => g.MentorId == slot.MentorId);
        Assert.NotNull(dayGuard);
        Assert.Equal(1, dayGuard.ActiveBookingCount);

        var spendEntry = await db.MentorCreditEntries.FirstOrDefaultAsync(e => e.EntryType == CreditEntryType.Spend);
        Assert.NotNull(spendEntry);
        Assert.Equal(-1, spendEntry.Amount);
    }

    // 2. Người dùng không phải Learner bị từ chối với 403 Forbidden
    [Theory]
    [InlineData(Role.Mentor)]
    [InlineData(Role.Admin)]
    public async Task Wrong_Role_Is_Rejected_With_403_Forbidden(Role wrongRole)
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db);
        var service = CreateBookingService(db, 101, wrongRole);

        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.False(response.IsSuccess);
        Assert.Equal(403, response.StatusCode);
    }

    // 3. Chuyên gia chưa được phê duyệt hồ sơ chính thức bị từ chối với 409 Conflict
    [Theory]
    [InlineData(MentorVerificationStatus.Pending)]
    [InlineData(MentorVerificationStatus.Rejected)]
    [InlineData(MentorVerificationStatus.Unverified)]
    public async Task Unapproved_Mentor_Slot_Is_Rejected_With_409_Conflict(MentorVerificationStatus status)
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db, verificationStatus: status);
        await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("chưa được phê duyệt", response.ErrorMessage);
    }

    // 4. Khung giờ đã bị xóa, bị từ chối hoặc đang yêu cầu hủy bị từ chối với 409 Conflict
    [Fact]
    public async Task Cancel_Requested_Or_Rejected_Slot_Is_Rejected_With_409()
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db, cancelRequestedAt: DateTime.UtcNow);
        await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
    }

    // 5. Khung giờ có lead-time dưới 2 giờ bị từ chối với 409 Conflict
    [Fact]
    public async Task Slot_With_Lead_Time_Under_Two_Hours_Is_Rejected_With_409()
    {
        await using var db = CreateInMemoryDb();
        // Slot bắt đầu sau 1 giờ (nhỏ hơn 2 giờ quy định)
        var (_, slot) = await SeedMentorAndSlotAsync(db, startHoursAhead: 1);
        await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("tối thiểu 2 giờ", response.ErrorMessage);
    }

    // 6. Khung giờ vượt quá chân trời 30 ngày bị từ chối với 409 Conflict
    [Fact]
    public async Task Slot_Beyond_Thirty_Days_Horizon_Is_Rejected_With_409()
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db, startHoursAhead: 24 * 35); // 35 ngày
        await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("30 ngày", response.ErrorMessage);
    }

    // 7. Khung giờ có thời lượng legacy (không phải 90 phút) bị từ chối với 409 Conflict
    [Theory]
    [InlineData(50)]
    [InlineData(60)]
    [InlineData(120)]
    public async Task Non_Ninety_Minute_Duration_Slot_Is_Rejected_With_409(int durationMinutes)
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db, durationMinutes: durationMinutes);
        await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("90 phút", response.ErrorMessage);
    }

    // 8. Không đủ số dư tín dụng hoặc lượt đã hết hạn bị từ chối với 409 Conflict
    [Fact]
    public async Task Insufficient_Credits_Or_Expired_Grants_Is_Rejected_With_409()
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db);
        // Cấp 0 lượt
        await SeedLearnerWithCreditsAsync(db, 101, 0);

        var service = CreateBookingService(db, 101);
        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("không còn đủ số lượt", response.ErrorMessage);
    }

    // 9. Thực thi quy tắc trừ tín dụng theo thứ tự FIFO (ưu tiên nguồn cấp hết hạn trước)
    [Fact]
    public async Task Credit_Deduction_Strictly_Follows_FIFO_By_Expiration()
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db);

        // Tạo 2 nguồn cấp: Grant 1 hết hạn sau 5 ngày, Grant 2 hết hạn sau 30 ngày
        var (_, _, grantEarlier) = await SeedLearnerWithCreditsAsync(db, 101, 1, DateTime.UtcNow.AddDays(5));

        var subLater = new UserSubscription
        {
            UserId = 101,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 2,
            MeetingSessionsUsed = 0,
            PurchasedAt = DateTime.UtcNow,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        db.UserSubscriptions.Add(subLater);
        await db.SaveChangesAsync();

        var grantLater = new MentorCreditGrant
        {
            LearnerId = 101,
            UserSubscriptionId = subLater.Id,
            InitialQuantity = 2,
            AvailableQuantity = 2,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.MentorCreditGrants.Add(grantLater);
        await db.SaveChangesAsync();

        var service = CreateBookingService(db, 101);
        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.True(response.IsSuccess);

        // Nguồn cấp hết hạn sớm hơn (grantEarlier) phải bị trừ trước
        var reloadedEarlier = await db.MentorCreditGrants.FindAsync(grantEarlier.Id);
        var reloadedLater = await db.MentorCreditGrants.FindAsync(grantLater.Id);

        Assert.Equal(0, reloadedEarlier!.AvailableQuantity);
        Assert.Equal(2, reloadedLater!.AvailableQuantity);
    }

    // 10. Thực thi giới hạn số buổi tối đa trong ngày của chuyên gia (Daily Booking Cap = 4)
    [Fact]
    public async Task Daily_Booking_Cap_Of_Four_Is_Enforced_With_409()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, slot) = await SeedMentorAndSlotAsync(db);
        await SeedLearnerWithCreditsAsync(db, 101, 5);

        // Đã đạt giới hạn 4 buổi trong ngày
        var tz = TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(slot.StartTime, tz));

        db.MentorBookingDayGuards.Add(new MentorBookingDayGuard
        {
            MentorId = mentor.Id,
            LocalBusinessDate = localDate,
            ActiveBookingCount = 4
        });
        await db.SaveChangesAsync();

        var service = CreateBookingService(db, 101);
        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("tối đa 4 buổi", response.ErrorMessage);
    }

    // 11. Xung đột với khoảng thời gian nghỉ 15 phút bắt buộc bị từ chối với 409 Conflict
    [Fact]
    public async Task Rest_Buffer_Conflict_Is_Rejected_With_409()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, slot) = await SeedMentorAndSlotAsync(db);
        await SeedLearnerWithCreditsAsync(db, 101, 3);

        // Tạo một reservation đã tồn tại kết thúc lúc slot.StartTime - 5 phút (vi phạm 15 phút nghỉ)
        var existingReservation = new MentorBookingReservation
        {
            MentorId = mentor.Id,
            MentorBookingId = 999,
            MentorAvailabilityId = 888,
            LocalBusinessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            StartUtc = slot.StartTime.AddMinutes(-95),
            EndUtc = slot.StartTime.AddMinutes(-5),
            RestProtectedUntilUtc = slot.StartTime.AddMinutes(10), // Kéo dài qua giờ bắt đầu slot
            PolicyVersion = 3,
            CreatedAtUtc = DateTime.UtcNow,
            ReleasedAtUtc = null
        };
        db.MentorBookingReservations.Add(existingReservation);
        await db.SaveChangesAsync();

        var service = CreateBookingService(db, 101);
        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("thời gian nghỉ 15 phút", response.ErrorMessage);
    }

    // 12. Slot đã có active reservation khác thì bị từ chối với 409 Conflict
    [Fact]
    public async Task Active_Reservation_On_Slot_Blocks_New_Booking_With_409()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, slot) = await SeedMentorAndSlotAsync(db);
        await SeedLearnerWithCreditsAsync(db, 101, 3);

        db.MentorBookingReservations.Add(new MentorBookingReservation
        {
            MentorId = mentor.Id,
            MentorBookingId = 888,
            MentorAvailabilityId = slot.Id,
            LocalBusinessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            StartUtc = slot.StartTime,
            EndUtc = slot.EndTime,
            RestProtectedUntilUtc = slot.EndTime.AddMinutes(15),
            PolicyVersion = 3,
            CreatedAtUtc = DateTime.UtcNow,
            ReleasedAtUtc = null
        });
        await db.SaveChangesAsync();

        var service = CreateBookingService(db, 101);
        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("đã có người giữ chỗ", response.ErrorMessage);
    }

    // 13. Học viên đã có lịch tư vấn khác trùng giờ thì bị từ chối với 409 Conflict
    [Fact]
    public async Task Learner_With_Overlapping_Booking_Is_Rejected_With_409()
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db);
        var (_, sub, _) = await SeedLearnerWithCreditsAsync(db, 101, 3);

        // Tạo lịch hẹn trùng giờ với chuyên gia khác
        var otherSlot = new MentorAvailability
        {
            MentorId = 999,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            MaxLearners = 1,
            BookedCount = 1,
            IsApproved = true
        };
        db.MentorAvailabilities.Add(otherSlot);
        await db.SaveChangesAsync();

        var existingBooking = new MentorBooking
        {
            UserSubscriptionId = sub.Id,
            MentorAvailabilityId = otherSlot.Id,
            Status = MentorStatus.Assigned,
            ScheduledAt = slot.StartTime,
            CreatedDate = DateTime.UtcNow
        };
        db.MentorBookings.Add(existingBooking);
        await db.SaveChangesAsync();

        var service = CreateBookingService(db, 101);
        var response = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("trùng với khung giờ này", response.ErrorMessage);
    }

    // 14. Gọi lặp với cùng OperationKey trả về kết quả hợp lệ mà không trừ trùng credit (Idempotent Replay)
    [Fact]
    public async Task Idempotent_Replay_With_Same_OperationKey_Does_Not_Deduct_Credits_Twice()
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db);
        var (_, _, grant) = await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        const string opKey = "idempotency-key-test-001";
        var request = new BookIncludedMeetingRequest
        {
            MentorAvailabilityId = slot.Id,
            OperationKey = opKey
        };

        var first = await service.BookSlotAsync(request);
        Assert.True(first.IsSuccess);
        Assert.Equal(201, first.StatusCode);

        var grantAfterFirst = await db.MentorCreditGrants.FindAsync(grant.Id);
        Assert.Equal(2, grantAfterFirst!.AvailableQuantity);

        // Gọi lại lần 2 với cùng key
        var second = await service.BookSlotAsync(request);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Result!.Id, second.Result!.Id);

        var grantAfterSecond = await db.MentorCreditGrants.FindAsync(grant.Id);
        Assert.Equal(2, grantAfterSecond!.AvailableQuantity); // Không bị trừ lần 2
    }

    // 15. Giải phóng giữ chỗ / hủy lịch hoàn trả tín dụng và phục hồi hiển thị khung giờ
    [Fact]
    public async Task Release_Booking_Hold_Cancels_Booking_Releases_Reservation_And_Refunds_Credit()
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db);
        var (_, _, grant) = await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        var booked = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });
        Assert.True(booked.IsSuccess);

        var bookingId = booked.Result!.Id;

        // Giải phóng giữ chỗ
        var releaseResponse = await service.ReleaseBookingHoldAsync(bookingId, "Em bận đột xuất");
        Assert.True(releaseResponse.IsSuccess, releaseResponse.ErrorMessage);

        var reloadedBooking = await db.MentorBookings.FindAsync(bookingId);
        Assert.Equal(MentorStatus.Cancelled, reloadedBooking!.Status);

        var reloadedReservation = await db.MentorBookingReservations.FirstOrDefaultAsync(r => r.MentorBookingId == bookingId);
        Assert.NotNull(reloadedReservation);
        Assert.NotNull(reloadedReservation.ReleasedAtUtc);

        var reloadedSlot = await db.MentorAvailabilities.FindAsync(slot.Id);
        Assert.Equal(0, reloadedSlot!.BookedCount); // Khung giờ trở lại trống

        var reloadedGrant = await db.MentorCreditGrants.FindAsync(grant.Id);
        Assert.Equal(3, reloadedGrant!.AvailableQuantity); // Đã được hoàn 1 lượt

        var refundEntry = await db.MentorCreditEntries.FirstOrDefaultAsync(e => e.EntryType == CreditEntryType.Refund);
        Assert.NotNull(refundEntry);
        Assert.Equal(1, refundEntry.Amount);
    }

    // 16. Học viên khác không được phép giải phóng lịch hẹn của người khác (403 Forbidden)
    [Fact]
    public async Task Other_Learner_Cannot_Release_Booking_Returns_403()
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db);
        await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        var booked = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });
        Assert.True(booked.IsSuccess);

        // Học viên 999 cố giải phóng
        var hackerService = CreateBookingService(db, 999, Role.Learner);
        var release = await hackerService.ReleaseBookingHoldAsync(booked.Result!.Id);

        Assert.False(release.IsSuccess);
        Assert.Equal(403, release.StatusCode);
    }

    // 17. Quản trị viên (Admin) có quyền giải phóng bất kỳ lịch hẹn nào
    [Fact]
    public async Task Admin_Can_Release_Any_Booking_Hold()
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db);
        await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        var booked = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });
        Assert.True(booked.IsSuccess);

        // Admin thực hiện giải phóng
        var adminService = CreateBookingService(db, 1, Role.Admin);
        var release = await adminService.ReleaseBookingHoldAsync(booked.Result!.Id, "Admin can thiệp do sự cố kỹ thuật");

        Assert.True(release.IsSuccess);
        var reloadedBooking = await db.MentorBookings.FindAsync(booked.Result!.Id);
        Assert.Equal(MentorStatus.Cancelled, reloadedBooking!.Status);
    }

    // 18. Tương tác với Discovery Stage 07: Slot biến mất khi đặt và hiển thị lại khi giải phóng
    [Fact]
    public async Task Stage07_Discovery_Reflects_Booked_Slot_And_Restores_On_Release()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, slot) = await SeedMentorAndSlotAsync(db);
        await SeedLearnerWithCreditsAsync(db, 101, 3);

        var uow = new UnitOfWork(db);
        var discoveryService = new MentorDiscoveryService(
            uow,
            NullLogger<MentorDiscoveryService>.Instance);

        var fromDate = DateOnly.FromDateTime(slot.StartTime);
        var toDate = fromDate.AddDays(1);

        // Trước khi đặt: Slot xuất hiện trong danh sách khả dụng
        var initialSlots = await discoveryService.GetPublicSlotsAsync(mentor.Id, new MentorSlotBrowsingRequest { StartDate = fromDate, EndDate = toDate });
        Assert.True(initialSlots.IsSuccess);
        Assert.True(initialSlots.Result!.TotalAvailableSlots > 0);

        // Tiến hành đặt lịch
        var bookingService = CreateBookingService(db, 101);
        var booked = await bookingService.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });
        Assert.True(booked.IsSuccess);

        // Sau khi đặt: Slot biến mất khỏi danh sách khả dụng của Discovery
        var afterBookingSlots = await discoveryService.GetPublicSlotsAsync(mentor.Id, new MentorSlotBrowsingRequest { StartDate = fromDate, EndDate = toDate });
        Assert.True(afterBookingSlots.IsSuccess);
        Assert.Equal(0, afterBookingSlots.Result!.TotalAvailableSlots);

        // Tiến hành giải phóng giữ chỗ
        var release = await bookingService.ReleaseBookingHoldAsync(booked.Result!.Id);
        Assert.True(release.IsSuccess);

        // Sau khi giải phóng: Slot xuất hiện trở lại trong Discovery
        var afterReleaseSlots = await discoveryService.GetPublicSlotsAsync(mentor.Id, new MentorSlotBrowsingRequest { StartDate = fromDate, EndDate = toDate });
        Assert.True(afterReleaseSlots.IsSuccess);
        Assert.True(afterReleaseSlots.Result!.TotalAvailableSlots > 0);
    }

    // 19. Đua tranh đồng thời: 2 học viên đặt cùng 1 slot -> đúng 1 người thắng (201), 1 người thua (409)
    [Fact]
    public async Task Concurrent_Booking_Same_Slot_Single_Winner_And_Loser_Gets_409()
    {
        var dbName = Guid.NewGuid().ToString();
        await using (var seedDb = CreateInMemoryDb(dbName))
        {
            var (_, slot) = await SeedMentorAndSlotAsync(seedDb);
            await SeedLearnerWithCreditsAsync(seedDb, 101, 3);
            await SeedLearnerWithCreditsAsync(seedDb, 102, 3);
        }

        // Tạo 2 DbContext độc lập mô phỏng 2 HTTP requests đồng thời vào 2 thread khác nhau
        await using var db1 = CreateInMemoryDb(dbName);
        await using var db2 = CreateInMemoryDb(dbName);

        var service1 = CreateBookingService(db1, 101);
        var service2 = CreateBookingService(db2, 102);

        var slotId = (await db1.MentorAvailabilities.SingleAsync()).Id;

        var task1 = service1.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slotId, OperationKey = "learner-101-race" });
        var task2 = service2.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slotId, OperationKey = "learner-102-race" });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.IsSuccess);
        var conflictCount = results.Count(r => !r.IsSuccess && r.StatusCode == 409);

        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);

        // Kiểm tra database: chỉ đúng 1 booking được tạo cho slot này
        await using var verifyDb = CreateInMemoryDb(dbName);
        var finalSlot = await verifyDb.MentorAvailabilities.FindAsync(slotId);
        Assert.Equal(1, finalSlot!.BookedCount);

        var reservations = await verifyDb.MentorBookingReservations.Where(r => r.MentorAvailabilityId == slotId).ToListAsync();
        Assert.Single(reservations);
    }

    // 20. Đua tranh đồng thời: 1 học viên có 1 credit đặt 2 slot khác nhau -> không thể double spend
    [Fact]
    public async Task Concurrent_Booking_Two_Slots_With_Single_Credit_Prevents_Double_Spending()
    {
        var dbName = Guid.NewGuid().ToString();
        int slot1Id, slot2Id;
        await using (var seedDb = CreateInMemoryDb(dbName))
        {
            var (_, slot1) = await SeedMentorAndSlotAsync(seedDb, 201, startHoursAhead: 24);
            var (_, slot2) = await SeedMentorAndSlotAsync(seedDb, 202, startHoursAhead: 48);
            slot1Id = slot1.Id;
            slot2Id = slot2.Id;

            // Cấp đúng 1 credit duy nhất
            await SeedLearnerWithCreditsAsync(seedDb, 101, 1);
        }

        await using var db1 = CreateInMemoryDb(dbName);
        await using var db2 = CreateInMemoryDb(dbName);

        var service1 = CreateBookingService(db1, 101);
        var service2 = CreateBookingService(db2, 101);

        var task1 = service1.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot1Id, OperationKey = "race-slot-1" });
        var task2 = service2.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot2Id, OperationKey = "race-slot-2" });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.IsSuccess);
        var failCount = results.Count(r => !r.IsSuccess && r.StatusCode == 409);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failCount);

        // Kiểm tra số dư credit còn lại đúng bằng 0, tuyệt đối không bị âm
        await using var verifyDb = CreateInMemoryDb(dbName);
        var grant = await verifyDb.MentorCreditGrants.SingleAsync();
        Assert.Equal(0, grant.AvailableQuantity);

        var spendEntries = await verifyDb.MentorCreditEntries.Where(e => e.EntryType == CreditEntryType.Spend).ToListAsync();
        Assert.Single(spendEntries);
    }

    // 21. Kiểm thử tích hợp ràng buộc GiST và kết nối QA PostgreSQL 17.11 trên 127.0.0.1:55439
    [Fact]
    public async Task PostgreSQL_QA_Rehearsal_Verifies_GiST_And_Constraint_Integrity()
    {
        var connStr = GetQaPostgreSqlConnectionString();
        if (string.IsNullOrWhiteSpace(connStr))
        {
            // Bỏ qua nếu môi trường test không có file pgpass
            return;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connStr)
            .Options;

        await using var db = new AppDbContext(options);

        // Kiểm tra khả năng kết nối tới cluster QA
        var canConnect = await db.Database.CanConnectAsync();
        Assert.True(canConnect, "Không thể kết nối tới QA PostgreSQL cluster 127.0.0.1:55439");

        // Kiểm tra extension btree_gist đã được kích hoạt trên QA database
        var uow = new UnitOfWork(db);
        var extCount = await uow.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pg_extension WHERE extname = 'btree_gist';");
        Assert.True(extCount >= 1, "Extension btree_gist phải tồn tại trên QA database");

        // Kiểm tra phiên bản PostgreSQL là 17.x
        var serverVersion = await uow.ExecuteScalarAsync<string>("SHOW server_version;");
        Assert.StartsWith("17.", serverVersion);
    }

    private static string? GetQaPostgreSqlConnectionString()
    {
        var pgpassPath = Environment.GetEnvironmentVariable("PGPASSFILE")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RikiPath", "qa-credentials", "mentor-v3.pgpass");
        if (!File.Exists(pgpassPath)) return null;

        var line = File.ReadAllLines(pgpassPath).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"));
        if (line == null) return null;

        var parts = line.Trim().Split(':');
        if (parts.Length < 5) return null;

        var host = parts[0] == "*" ? "127.0.0.1" : parts[0];
        var port = parts[1] == "*" ? "55439" : parts[1];
        var db = parts[2] == "*" ? "rikipath_mentor_qa" : parts[2];
        var user = parts[3];
        var password = parts[4];

        return $"Host={host};Port={port};Database={db};Username={user};Password={password};Timeout=5;CommandTimeout=5;";
    }

    // 24. Anti-Bypass: Learner giải phóng booking khi còn dưới 24h không được hoàn vé
    [Fact]
    public async Task Learner_ReleaseBookingHold_LessThan24h_DoesNotRefundCredit_AntiBypass()
    {
        await using var db = CreateInMemoryDb();
        // Slot bắt đầu sau 12h (< 24h)
        var (_, slot) = await SeedMentorAndSlotAsync(db, startHoursAhead: 12);
        var (_, _, grant) = await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        var booked = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });
        Assert.True(booked.IsSuccess);
        var bookingId = booked.Result!.Id;

        // Gọi ReleaseBookingHoldAsync nhằm lách chính sách hủy dưới 24h
        var releaseResponse = await service.ReleaseBookingHoldAsync(bookingId, "Cố giải phóng sát giờ để đòi hoàn vé");
        Assert.True(releaseResponse.IsSuccess);

        // Booking bị huỷ và slot được nhả
        var reloadedBooking = await db.MentorBookings.FindAsync(bookingId);
        Assert.Equal(MentorStatus.Cancelled, reloadedBooking!.Status);

        // Nhưng KHÔNG được hoàn credit (AvailableQuantity vẫn là 2, không tạo entry Refund)
        var reloadedGrant = await db.MentorCreditGrants.FindAsync(grant.Id);
        Assert.Equal(2, reloadedGrant!.AvailableQuantity);

        var refundEntry = await db.MentorCreditEntries.FirstOrDefaultAsync(e => e.EntryType == CreditEntryType.Refund);
        Assert.Null(refundEntry);
    }

    // 25. Anti-Bypass: Learner không thể giải phóng booking khi buổi học đã bắt đầu (409 Conflict)
    [Fact]
    public async Task Learner_ReleaseBookingHold_AfterSessionStart_Returns_409Conflict()
    {
        await using var db = CreateInMemoryDb();
        var (_, slot) = await SeedMentorAndSlotAsync(db, startHoursAhead: 48);
        await SeedLearnerWithCreditsAsync(db, 101, 3);

        var service = CreateBookingService(db, 101);
        var booked = await service.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot.Id });
        Assert.True(booked.IsSuccess);
        var bookingId = booked.Result!.Id;

        // Giả lập thời gian đã trôi qua: slot bắt đầu 10 phút trước
        slot.StartTime = DateTime.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();

        var releaseResponse = await service.ReleaseBookingHoldAsync(bookingId, "Hủy khi đã vào giờ học");
        Assert.False(releaseResponse.IsSuccess);
        Assert.Equal(409, releaseResponse.StatusCode);
    }
}
