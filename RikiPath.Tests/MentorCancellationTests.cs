using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Application.Responses.MentorScheduling;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;
using RikiPath.WebApi.Controllers;
using Xunit;

namespace RikiPath.Tests;

// Bộ kiểm thử tích hợp nghiệp vụ Hủy lịch hẹn, Dời lịch & Bồi hoàn tín dụng (Stage 10/13)
public sealed class MentorCancellationTests
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

    private static async Task<(UserAccount mentor, UserAccount learner, MentorAvailability slot, MentorBooking booking, MentorBookingReservation reservation, MentorBookingDayGuard guard, UserSubscription sub)> SeedBookingScenarioAsync(
        AppDbContext db,
        int mentorId = 201,
        int learnerId = 101,
        double startHoursOffset = 30,
        int durationMinutes = 60,
        MentorStatus bookingStatus = MentorStatus.Assigned,
        bool isSlotApproved = true)
    {
        var now = DateTime.UtcNow;
        var rId = Guid.NewGuid();

        var mentor = new UserAccount
        {
            Id = mentorId,
            FirstName = "Kenji",
            LastName = "Yamada",
            Email = $"mentor{mentorId}@rikipath.edu.vn",
            Role = Role.Mentor,
            PasswordHash = [],
            PasswordSalt = []
        };
        db.Users.Add(mentor);

        var learner = new UserAccount
        {
            Id = learnerId,
            FirstName = "An",
            LastName = "Nguyen",
            Email = $"learner{learnerId}@rikipath.edu.vn",
            Role = Role.Learner,
            PasswordHash = [],
            PasswordSalt = []
        };
        db.Users.Add(learner);

        var sub = new UserSubscription
        {
            Id = 500 + learnerId,
            UserId = learnerId,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 10,
            MeetingSessionsUsed = 1,
            PurchasedAt = now.AddDays(-2),
            StartDate = now.AddDays(-2),
            EndDate = now.AddDays(5)
        };
        db.UserSubscriptions.Add(sub);

        var slot = new MentorAvailability
        {
            Id = 700 + mentorId,
            MentorId = mentorId,
            RoomId = rId,
            StartTime = now.AddHours(startHoursOffset),
            EndTime = now.AddHours(startHoursOffset).AddMinutes(durationMinutes),
            IsApproved = isSlotApproved,
            MaxLearners = 1,
            BookedCount = 1
        };
        db.MentorAvailabilities.Add(slot);

        var booking = new MentorBooking
        {
            Id = 900 + learnerId,
            UserSubscriptionId = sub.Id,
            MentorAvailabilityId = slot.Id,
            Status = bookingStatus,
            ScheduledAt = slot.StartTime
        };
        db.MentorBookings.Add(booking);

        var localStartTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(slot.StartTime, localStartTz);
        var localDate = DateOnly.FromDateTime(localStart);

        var reservation = new MentorBookingReservation
        {
            Id = 1000 + learnerId,
            MentorBookingId = booking.Id,
            MentorId = mentorId,
            StartUtc = slot.StartTime,
            EndUtc = slot.EndTime,
            LocalBusinessDate = localDate,
            RestProtectedUntilUtc = slot.EndTime.AddMinutes(15),
            CreatedAtUtc = now.AddDays(-1),
            PolicyVersion = 1
        };
        db.MentorBookingReservations.Add(reservation);

        var guard = new MentorBookingDayGuard
        {
            MentorId = mentorId,
            LocalBusinessDate = localDate,
            ActiveBookingCount = 1
        };
        db.MentorBookingDayGuards.Add(guard);

        await db.SaveChangesAsync();
        return (mentor, learner, slot, booking, reservation, guard, sub);
    }

    [Fact]
    public async Task Learner_early_cancellation_refunds_credit_and_releases_slot_and_guard()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, slot, booking, _, _, _) = await SeedBookingScenarioAsync(db, startHoursOffset: 30);

        var oldRoomId = slot.RoomId;
        var claims = MockClaims(Role.Learner, learner.Id);

        var mockCredit = new Mock<IMentorCreditLedgerService>();
        mockCredit.Setup(x => x.RefundCreditAsync(learner.Id, booking.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditRefundResult
            {
                IsSuccess = true,
                RestoredBalance = 1,
                OperationKey = $"refund:booking:{booking.Id}"
            });

        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.CancelBookingAsync(booking.Id, new CancelMentorBookingRequest
        {
            Reason = "Bận việc đột xuất trước 24h"
        });

        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.True(result.Result.IsRefunded);
        Assert.False(result.Result.IsLateCancellation);
        Assert.Equal(MentorStatus.Cancelled, result.Result.Status);
        Assert.Equal("Learner", result.Result.CancelledBy);

        // Kiểm tra database cập nhật
        var updatedBooking = await db.MentorBookings.FindAsync(booking.Id);
        Assert.NotNull(updatedBooking);
        Assert.Equal(MentorStatus.Cancelled, updatedBooking.Status);
        Assert.Equal("Learner", updatedBooking.CancelledByRole);
        Assert.Equal(learner.Id, updatedBooking.CancelledByUserId);
        Assert.NotNull(updatedBooking.CancelledAtUtc);

        var updatedSlot = await db.MentorAvailabilities.FindAsync(slot.Id);
        Assert.NotNull(updatedSlot);
        Assert.Equal(0, updatedSlot.BookedCount);
        Assert.NotEqual(oldRoomId, updatedSlot.RoomId); // RoomId được xoay mới

        var updatedReservation = await db.MentorBookingReservations.FirstOrDefaultAsync(r => r.MentorBookingId == booking.Id);
        Assert.NotNull(updatedReservation);
        Assert.NotNull(updatedReservation.ReleasedAtUtc);

        var updatedGuard = await db.MentorBookingDayGuards.FirstOrDefaultAsync(g => g.MentorId == mentor.Id);
        Assert.NotNull(updatedGuard);
        Assert.Equal(0, updatedGuard.ActiveBookingCount);

        // Kiểm tra Outbox event
        var outbox = await db.MentorOutboxEvents.FirstOrDefaultAsync(e => e.AggregateId == booking.Id.ToString());
        Assert.NotNull(outbox);
        Assert.Equal("BookingCancelled", outbox.EventType);

        // Xác nhận gọi hoàn tín dụng
        mockCredit.Verify(x => x.RefundCreditAsync(learner.Id, booking.Id, It.Is<string>(k => k.StartsWith("refund:booking:")), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Learner_late_cancellation_releases_slot_without_credit_refund()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, slot, booking, _, _, _) = await SeedBookingScenarioAsync(db, startHoursOffset: 5); // Dưới 24h

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.CancelBookingAsync(booking.Id, new CancelMentorBookingRequest
        {
            Reason = "Bận việc sát giờ học"
        });

        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.False(result.Result.IsRefunded);
        Assert.True(result.Result.IsLateCancellation);

        var updatedBooking = await db.MentorBookings.FindAsync(booking.Id);
        Assert.NotNull(updatedBooking);
        Assert.Equal(MentorStatus.Cancelled, updatedBooking.Status);

        var updatedSlot = await db.MentorAvailabilities.FindAsync(slot.Id);
        Assert.NotNull(updatedSlot);
        Assert.Equal(0, updatedSlot.BookedCount); // Slot vẫn được giải phóng

        // Tuyệt đối không gọi hoàn tín dụng
        mockCredit.Verify(x => x.RefundCreditAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Outbox event ghi nhận BookingLateCancelled
        var outbox = await db.MentorOutboxEvents.FirstOrDefaultAsync(e => e.AggregateId == booking.Id.ToString());
        Assert.NotNull(outbox);
        Assert.Equal("BookingLateCancelled", outbox.EventType);
    }

    [Fact]
    public async Task Learner_late_cancellation_with_exception_request_flags_for_admin_review()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _, _, _) = await SeedBookingScenarioAsync(db, startHoursOffset: 3);

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.CancelBookingAsync(booking.Id, new CancelMentorBookingRequest
        {
            Reason = "Hủy sát giờ do cấp cứu y tế",
            RequestException = true,
            ExceptionReason = "Giấy chứng nhận cấp cứu từ bệnh viện"
        });

        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.True(result.Result.ExceptionRequested);
        Assert.Null(result.Result.ExceptionApproved);

        var updatedBooking = await db.MentorBookings.FindAsync(booking.Id);
        Assert.NotNull(updatedBooking);
        Assert.True(updatedBooking.ExceptionRequested);
        Assert.Equal("Giấy chứng nhận cấp cứu từ bệnh viện", updatedBooking.ExceptionReason);
        Assert.Null(updatedBooking.ExceptionApproved);
    }

    [Fact]
    public async Task Mentor_cancellation_before_session_refunds_learner_and_extends_validity()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, slot, booking, _, _, sub) = await SeedBookingScenarioAsync(db, startHoursOffset: 12);

        var claims = MockClaims(Role.Mentor, mentor.Id);

        var mockCredit = new Mock<IMentorCreditLedgerService>();
        mockCredit.Setup(x => x.RefundCreditAsync(learner.Id, booking.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditRefundResult
            {
                IsSuccess = true,
                RestoredBalance = 1,
                OperationKey = $"refund:booking:{booking.Id}"
            });

        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.CancelBookingAsync(booking.Id, new CancelMentorBookingRequest
        {
            Reason = "Mentor có lịch công tác khẩn cấp"
        });

        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.True(result.Result.IsRefunded);
        Assert.Equal("Mentor", result.Result.CancelledBy);

        var updatedSlot = await db.MentorAvailabilities.FindAsync(slot.Id);
        Assert.NotNull(updatedSlot);
        Assert.NotNull(updatedSlot.CancelRequestedAt); // Mentor hủy đánh dấu slot không cho nhận book lại

        var updatedSub = await db.UserSubscriptions.FindAsync(sub.Id);
        Assert.NotNull(updatedSub);
        Assert.True(updatedSub.EndDate >= DateTime.UtcNow.AddDays(6)); // Gia hạn ít nhất 7 ngày

        // Outbox event ghi nhận BookingCancelledByMentor
        var outbox = await db.MentorOutboxEvents.FirstOrDefaultAsync(e => e.AggregateId == booking.Id.ToString());
        Assert.NotNull(outbox);
        Assert.Equal("BookingCancelledByMentor", outbox.EventType);
    }

    [Fact]
    public async Task Mentor_cancellation_after_start_time_rejected_with_conflict()
    {
        await using var db = CreateInMemoryDb();
        // Slot đã bắt đầu 10 phút trước
        var (mentor, _, _, booking, _, _, _) = await SeedBookingScenarioAsync(db, startHoursOffset: -0.16);

        var claims = MockClaims(Role.Mentor, mentor.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.CancelBookingAsync(booking.Id, new CancelMentorBookingRequest
        {
            Reason = "Mentor hủy trễ"
        });

        Assert.Equal((int)HttpStatusCode.Conflict, result.StatusCode);
        Assert.Contains("sau khi đã đến giờ bắt đầu", result.ErrorMessage);
    }

    [Fact]
    public async Task Admin_cancellation_force_cancels_and_refunds_and_extends()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _, _, sub) = await SeedBookingScenarioAsync(db, startHoursOffset: 2);

        var claims = MockClaims(Role.Admin, 1);

        var mockCredit = new Mock<IMentorCreditLedgerService>();
        mockCredit.Setup(x => x.RefundCreditAsync(learner.Id, booking.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditRefundResult
            {
                IsSuccess = true,
                RestoredBalance = 1,
                OperationKey = $"refund:booking:{booking.Id}"
            });

        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.CancelBookingAsync(booking.Id, new CancelMentorBookingRequest
        {
            Reason = "Admin can thiệp hỗ trợ kỹ thuật"
        });

        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.True(result.Result.IsRefunded);
        Assert.Equal("Admin", result.Result.CancelledBy);

        var updatedSub = await db.UserSubscriptions.FindAsync(sub.Id);
        Assert.NotNull(updatedSub);
        Assert.True(updatedSub.EndDate >= DateTime.UtcNow.AddDays(6));
    }

    [Fact]
    public async Task Cancelling_completed_booking_rejected_with_conflict()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _, _, _) = await SeedBookingScenarioAsync(db, bookingStatus: MentorStatus.Completed);

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.CancelBookingAsync(booking.Id, new CancelMentorBookingRequest());

        Assert.Equal((int)HttpStatusCode.Conflict, result.StatusCode);
        Assert.Contains("đã hoàn thành", result.ErrorMessage);
    }

    [Fact]
    public async Task Cancelling_ongoing_booking_while_online_in_room_rejected_with_conflict()
    {
        await using var db = CreateInMemoryDb();
        // Slot đang diễn ra (bắt đầu cách đây 5 phút)
        var (_, learner, slot, booking, _, _, _) = await SeedBookingScenarioAsync(db, startHoursOffset: -0.08);

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        mockAttendance.Setup(x => x.IsUserOnlineInRoom(slot.RoomId, learner.Id)).Returns(true);

        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.CancelBookingAsync(booking.Id, new CancelMentorBookingRequest());

        Assert.Equal((int)HttpStatusCode.Conflict, result.StatusCode);
        Assert.Contains("đang có mặt trong phòng họp", result.ErrorMessage);
    }

    [Fact]
    public async Task Repeated_cancellation_is_idempotent_without_duplicate_refund()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _, _, _) = await SeedBookingScenarioAsync(db, bookingStatus: MentorStatus.Cancelled);

        // Giả lập booking đã có metadata hủy trước đó
        booking.CancelledAtUtc = DateTime.UtcNow.AddHours(-1);
        booking.CancelledByRole = "Learner";
        booking.CancellationReason = "Hủy trước đó";
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.CancelBookingAsync(booking.Id, new CancelMentorBookingRequest());

        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.False(result.Result.IsRefunded);
        Assert.Contains("idempotent", result.Result.Message);

        mockCredit.Verify(x => x.RefundCreditAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unauthorized_learner_or_mentor_cannot_cancel_others_booking()
    {
        await using var db = CreateInMemoryDb();
        var (_, _, _, booking, _, _, _) = await SeedBookingScenarioAsync(db);

        // Learner khác (ID 999)
        var alienClaims = MockClaims(Role.Learner, 999);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, alienClaims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.CancelBookingAsync(booking.Id, new CancelMentorBookingRequest());

        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Contains("không có quyền", result.ErrorMessage);
    }

    [Fact]
    public async Task Learner_reschedule_to_valid_slot_of_same_mentor_transfers_reservation_atomically()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, oldSlot, booking, _, _, _) = await SeedBookingScenarioAsync(db, startHoursOffset: 48);

        // Tạo slot mới của cùng Mentor vào 72h sau
        var now = DateTime.UtcNow;
        var newSlot = new MentorAvailability
        {
            Id = 888,
            MentorId = mentor.Id,
            RoomId = Guid.NewGuid(),
            StartTime = now.AddHours(72),
            EndTime = now.AddHours(73),
            IsApproved = true,
            MaxLearners = 1,
            BookedCount = 0
        };
        db.MentorAvailabilities.Add(newSlot);
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.RescheduleBookingAsync(booking.Id, new RescheduleMentorBookingRequest
        {
            NewSlotId = newSlot.Id
        });

        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.Equal(oldSlot.Id, result.Result.PreviousSlotId);
        Assert.Equal(newSlot.Id, result.Result.NewSlotId);

        // Kiểm tra database cập nhật
        var updatedBooking = await db.MentorBookings.FindAsync(booking.Id);
        Assert.NotNull(updatedBooking);
        Assert.Equal(newSlot.Id, updatedBooking.MentorAvailabilityId);
        Assert.Equal(MentorStatus.Assigned, updatedBooking.Status);

        var updatedOldSlot = await db.MentorAvailabilities.FindAsync(oldSlot.Id);
        Assert.NotNull(updatedOldSlot);
        Assert.Equal(0, updatedOldSlot.BookedCount);

        var updatedNewSlot = await db.MentorAvailabilities.FindAsync(newSlot.Id);
        Assert.NotNull(updatedNewSlot);
        Assert.Equal(1, updatedNewSlot.BookedCount);

        // Kiểm tra reservation được chuyển nguyên tử sang slot mới (tuân thủ unique index IX_MentorBookingReservations_MentorBookingId)
        var transferredReservation = await db.MentorBookingReservations.FirstOrDefaultAsync(r => r.MentorBookingId == booking.Id);
        Assert.NotNull(transferredReservation);
        Assert.Equal(newSlot.Id, transferredReservation.MentorAvailabilityId);
        Assert.Equal(newSlot.StartTime, transferredReservation.StartUtc);
        Assert.Equal(newSlot.EndTime, transferredReservation.EndUtc);
        Assert.Null(transferredReservation.ReleasedAtUtc);

        // Kiểm tra Outbox event
        var outbox = await db.MentorOutboxEvents.FirstOrDefaultAsync(e => e.AggregateId == booking.Id.ToString() && e.EventType == "BookingRescheduled");
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task Reschedule_rejected_when_under_24_hours_from_start()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, _, _, _) = await SeedBookingScenarioAsync(db, startHoursOffset: 12); // < 24h

        var newSlot = new MentorAvailability
        {
            Id = 888,
            MentorId = mentor.Id,
            RoomId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow.AddHours(72),
            EndTime = DateTime.UtcNow.AddHours(73),
            IsApproved = true,
            MaxLearners = 1,
            BookedCount = 0
        };
        db.MentorAvailabilities.Add(newSlot);
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.RescheduleBookingAsync(booking.Id, new RescheduleMentorBookingRequest
        {
            NewSlotId = newSlot.Id
        });

        Assert.Equal((int)HttpStatusCode.Conflict, result.StatusCode);
        Assert.Contains("ít nhất 24 tiếng", result.ErrorMessage);
    }

    [Fact]
    public async Task Reschedule_rejected_when_new_slot_belongs_to_different_mentor()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _, _, _) = await SeedBookingScenarioAsync(db, startHoursOffset: 48);

        // Mentor khác (ID 999) và slot của họ
        var otherMentor = new UserAccount
        {
            Id = 999,
            FirstName = "Other",
            LastName = "Mentor",
            Email = "othermentor999@rikipath.edu.vn",
            Role = Role.Mentor,
            PasswordHash = [],
            PasswordSalt = []
        };
        db.Users.Add(otherMentor);

        var newSlot = new MentorAvailability
        {
            Id = 888,
            MentorId = 999,
            RoomId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow.AddHours(72),
            EndTime = DateTime.UtcNow.AddHours(73),
            IsApproved = true,
            MaxLearners = 1,
            BookedCount = 0
        };
        db.MentorAvailabilities.Add(newSlot);
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.RescheduleBookingAsync(booking.Id, new RescheduleMentorBookingRequest
        {
            NewSlotId = newSlot.Id
        });

        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("cùng một cố vấn", result.ErrorMessage);
    }

    [Fact]
    public async Task Reschedule_rejected_when_new_slot_is_fully_booked()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, _, _, _) = await SeedBookingScenarioAsync(db, startHoursOffset: 48);

        var newSlot = new MentorAvailability
        {
            Id = 888,
            MentorId = mentor.Id,
            RoomId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow.AddHours(72),
            EndTime = DateTime.UtcNow.AddHours(73),
            IsApproved = true,
            MaxLearners = 1,
            BookedCount = 1 // Đã đầy
        };
        db.MentorAvailabilities.Add(newSlot);
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.RescheduleBookingAsync(booking.Id, new RescheduleMentorBookingRequest
        {
            NewSlotId = newSlot.Id
        });

        Assert.Equal((int)HttpStatusCode.Conflict, result.StatusCode);
        Assert.Contains("đã được học viên khác đặt kín", result.ErrorMessage);
    }

    [Fact]
    public async Task Check_cancellation_eligibility_returns_accurate_policy_tiers()
    {
        await using var db = CreateInMemoryDb();
        // 1. Nhánh sớm > 24h
        var (_, learner, _, bookingEarly, _, _, _) = await SeedBookingScenarioAsync(db, startHoursOffset: 48);
        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var resEarly = await service.CheckCancellationEligibilityAsync(bookingEarly.Id);
        Assert.Equal((int)HttpStatusCode.OK, resEarly.StatusCode);
        Assert.NotNull(resEarly.Result);
        Assert.True(resEarly.Result.CanCancel);
        Assert.True(resEarly.Result.CanReschedule);
        Assert.True(resEarly.Result.EligibleForFullRefund);
        Assert.Equal("Early_FullRefund", resEarly.Result.Tier);

        // 2. Nhánh muộn < 24h
        var (_, learner2, _, bookingLate, _, _, _) = await SeedBookingScenarioAsync(db, mentorId: 301, learnerId: 102, startHoursOffset: 10);
        var claims2 = MockClaims(Role.Learner, learner2.Id);
        var service2 = new MentorCancellationService(db, claims2, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var resLate = await service2.CheckCancellationEligibilityAsync(bookingLate.Id);
        Assert.Equal((int)HttpStatusCode.OK, resLate.StatusCode);
        Assert.NotNull(resLate.Result);
        Assert.True(resLate.Result.CanCancel);
        Assert.False(resLate.Result.CanReschedule);
        Assert.False(resLate.Result.EligibleForFullRefund);
        Assert.Equal("Late_NoRefund", resLate.Result.Tier);

        // 3. Nhánh đã bắt đầu
        var (_, learner3, _, bookingStarted, _, _, _) = await SeedBookingScenarioAsync(db, mentorId: 302, learnerId: 103, startHoursOffset: -1);
        var claims3 = MockClaims(Role.Learner, learner3.Id);
        var service3 = new MentorCancellationService(db, claims3, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var resStarted = await service3.CheckCancellationEligibilityAsync(bookingStarted.Id);
        Assert.Equal((int)HttpStatusCode.OK, resStarted.StatusCode);
        Assert.NotNull(resStarted.Result);
        Assert.False(resStarted.Result.CanCancel);
        Assert.False(resStarted.Result.CanReschedule);
        Assert.Equal("Session_Started", resStarted.Result.Tier);
    }

    [Fact]
    public async Task Admin_review_exception_request_approves_and_refunds_and_extends()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _, _, sub) = await SeedBookingScenarioAsync(db, startHoursOffset: 5);

        // Giả lập booking đã yêu cầu ngoại lệ
        booking.ExceptionRequested = true;
        booking.ExceptionReason = "Lý do bất khả kháng có xác nhận";
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Admin, 1);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        mockCredit.Setup(x => x.RefundCreditAsync(learner.Id, booking.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditRefundResult
            {
                IsSuccess = true,
                RestoredBalance = 1,
                OperationKey = $"refund:exception:{booking.Id}"
            });

        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.ReviewExceptionRequestAsync(booking.Id, new ReviewCancellationExceptionRequest
        {
            ApproveRefund = true,
            ExtendDays = 14,
            AdminNotes = "Hồ sơ y tế hợp lệ"
        });

        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.True(result.Result.IsRefunded);
        Assert.True(result.Result.ExceptionApproved);

        var updatedBooking = await db.MentorBookings.FindAsync(booking.Id);
        Assert.NotNull(updatedBooking);
        Assert.True(updatedBooking.ExceptionApproved);
        Assert.Contains("Duyệt hoàn vé", updatedBooking.CancellationReason);

        var updatedSub = await db.UserSubscriptions.FindAsync(sub.Id);
        Assert.NotNull(updatedSub);
        Assert.True(updatedSub.EndDate >= DateTime.UtcNow.AddDays(13));

        mockCredit.Verify(x => x.RefundCreditAsync(learner.Id, booking.Id, It.Is<string>(k => k.StartsWith("refund:exception:")), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_review_exception_request_rejects_without_refund()
    {
        await using var db = CreateInMemoryDb();
        var (_, _, _, booking, _, _, _) = await SeedBookingScenarioAsync(db, startHoursOffset: 5);

        booking.ExceptionRequested = true;
        booking.ExceptionReason = "Lý do không chính đáng";
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Admin, 1);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.ReviewExceptionRequestAsync(booking.Id, new ReviewCancellationExceptionRequest
        {
            ApproveRefund = false,
            AdminNotes = "Không cung cấp đủ bằng chứng"
        });

        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.False(result.Result.IsRefunded);
        Assert.False(result.Result.ExceptionApproved);

        var updatedBooking = await db.MentorBookings.FindAsync(booking.Id);
        Assert.NotNull(updatedBooking);
        Assert.False(updatedBooking.ExceptionApproved);
        Assert.Contains("Từ chối", updatedBooking.CancellationReason);

        mockCredit.Verify(x => x.RefundCreditAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Non_admin_cannot_review_cancellation_exception()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _, _, _) = await SeedBookingScenarioAsync(db);

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.ReviewExceptionRequestAsync(booking.Id, new ReviewCancellationExceptionRequest());

        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Contains("quản trị viên", result.ErrorMessage);
    }

    [Fact]
    public async Task Review_exception_returns_bad_request_if_no_exception_requested()
    {
        await using var db = CreateInMemoryDb();
        var (_, _, _, booking, _, _, _) = await SeedBookingScenarioAsync(db);
        // ExceptionRequested mặc định là false

        var claims = MockClaims(Role.Admin, 1);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service = new MentorCancellationService(db, claims, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var result = await service.ReviewExceptionRequestAsync(booking.Id, new ReviewCancellationExceptionRequest());

        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("không có yêu cầu ngoại lệ", result.ErrorMessage);
    }

    [Fact]
    public async Task MentorCancellationController_endpoints_delegate_to_service_and_return_expected_status_codes()
    {
        var mockService = new Mock<IMentorCancellationService>();

        mockService.Setup(x => x.CancelBookingAsync(100, It.IsAny<CancelMentorBookingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<MentorCancellationResponse>.Success(new MentorCancellationResponse { BookingId = 100 }, HttpStatusCode.OK));

        mockService.Setup(x => x.RescheduleBookingAsync(100, It.IsAny<RescheduleMentorBookingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<MentorRescheduleResponse>.Success(new MentorRescheduleResponse { BookingId = 100 }, HttpStatusCode.OK));

        mockService.Setup(x => x.CheckCancellationEligibilityAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<CancellationPolicyEligibilityResponse>.Success(new CancellationPolicyEligibilityResponse { BookingId = 100 }, HttpStatusCode.OK));

        mockService.Setup(x => x.ReviewExceptionRequestAsync(100, It.IsAny<ReviewCancellationExceptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<MentorCancellationResponse>.Success(new MentorCancellationResponse { BookingId = 100 }, HttpStatusCode.OK));

        var controller = new MentorCancellationController(mockService.Object);

        // 1. Cancel
        var cancelAction = await controller.CancelBooking(100, new CancelMentorBookingRequest(), CancellationToken.None);
        var cancelResult = Assert.IsType<ObjectResult>(cancelAction);
        Assert.Equal(200, cancelResult.StatusCode);

        // 2. Reschedule
        var rescheduleAction = await controller.RescheduleBooking(100, new RescheduleMentorBookingRequest(), CancellationToken.None);
        var rescheduleResult = Assert.IsType<ObjectResult>(rescheduleAction);
        Assert.Equal(200, rescheduleResult.StatusCode);

        // 3. Eligibility
        var eligibilityAction = await controller.CheckCancellationEligibility(100, CancellationToken.None);
        var eligibilityResult = Assert.IsType<ObjectResult>(eligibilityAction);
        Assert.Equal(200, eligibilityResult.StatusCode);

        // 4. Review
        var reviewAction = await controller.ReviewCancellationException(100, new ReviewCancellationExceptionRequest(), CancellationToken.None);
        var reviewResult = Assert.IsType<ObjectResult>(reviewAction);
        Assert.Equal(200, reviewResult.StatusCode);
    }

    [Fact]
    public void MentorBooking_cancellation_metadata_properties_exist_and_persist()
    {
        var now = DateTime.UtcNow;
        var booking = new MentorBooking
        {
            Id = 555,
            CancellationReason = "Học viên hủy trước 24h",
            CancelledAtUtc = now,
            CancelledByUserId = 101,
            CancelledByRole = "Learner",
            ExceptionRequested = true,
            ExceptionReason = "Lý do đột xuất",
            ExceptionApproved = true
        };

        Assert.Equal("Học viên hủy trước 24h", booking.CancellationReason);
        Assert.Equal(now, booking.CancelledAtUtc);
        Assert.Equal(101, booking.CancelledByUserId);
        Assert.Equal("Learner", booking.CancelledByRole);
        Assert.True(booking.ExceptionRequested);
        Assert.Equal("Lý do đột xuất", booking.ExceptionReason);
        Assert.True(booking.ExceptionApproved);
    }
    // 23. Boundary Test: Kiểm tra chính xác ngưỡng 24h đối với quyền lợi hoàn vé (>= 24h vs < 24h)
    [Fact]
    public async Task Check_cancellation_eligibility_exact_boundary_conditions()
    {
        await using var db = CreateInMemoryDb();
        // Nhánh đúng ngưỡng 24h (24.0 giờ trước buổi hẹn)
        var (_, learner1, _, bookingBoundary, _, _, _) = await SeedBookingScenarioAsync(db, mentorId: 401, learnerId: 201, startHoursOffset: 24.05);
        var claims1 = MockClaims(Role.Learner, learner1.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        var service1 = new MentorCancellationService(db, claims1, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var resBoundary = await service1.CheckCancellationEligibilityAsync(bookingBoundary.Id);
        Assert.Equal((int)HttpStatusCode.OK, resBoundary.StatusCode);
        Assert.NotNull(resBoundary.Result);
        Assert.True(resBoundary.Result.CanCancel);
        Assert.True(resBoundary.Result.CanReschedule);
        Assert.True(resBoundary.Result.EligibleForFullRefund);
        Assert.Equal("Early_FullRefund", resBoundary.Result.Tier);

        // Nhánh dưới ngưỡng 24h một khoảng ngắn (23.9 giờ trước buổi hẹn)
        var (_, learner2, _, bookingSubBoundary, _, _, _) = await SeedBookingScenarioAsync(db, mentorId: 402, learnerId: 202, startHoursOffset: 23.9);
        var claims2 = MockClaims(Role.Learner, learner2.Id);
        var service2 = new MentorCancellationService(db, claims2, mockCredit.Object, mockAttendance.Object, NullLogger<MentorCancellationService>.Instance);

        var resSubBoundary = await service2.CheckCancellationEligibilityAsync(bookingSubBoundary.Id);
        Assert.Equal((int)HttpStatusCode.OK, resSubBoundary.StatusCode);
        Assert.NotNull(resSubBoundary.Result);
        Assert.True(resSubBoundary.Result.CanCancel);
        Assert.False(resSubBoundary.Result.CanReschedule);
        Assert.False(resSubBoundary.Result.EligibleForFullRefund);
        Assert.Equal("Late_NoRefund", resSubBoundary.Result.Tier);
    }
}
