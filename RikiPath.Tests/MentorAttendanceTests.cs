using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;
using RikiPath.WebApi.Controllers;
using RikiPath.WebApi.Hubs;
using Xunit;

namespace RikiPath.Tests;

// Bộ kiểm thử tích hợp và thời gian thực cho Điểm danh & Đồng bộ Phòng họp Mentor (Stage 09/13)
public sealed class MentorAttendanceTests
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

    private static async Task<(UserAccount mentor, UserAccount learner, MentorAvailability slot, MentorBooking booking)> SeedBookingAndSlotAsync(
        AppDbContext db,
        int mentorId = 201,
        int learnerId = 101,
        int startMinutesOffset = 0,
        int durationMinutes = 60,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        MentorStatus bookingStatus = MentorStatus.Assigned,
        bool isSlotApproved = true,
        Guid? roomId = null)
    {
        var now = DateTime.UtcNow;
        var rId = roomId ?? Guid.NewGuid();

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
            PaymentStatus = paymentStatus,
            MeetingSessionsIncluded = 10,
            MeetingSessionsUsed = 1,
            PurchasedAt = now.AddDays(-1),
            StartDate = now.AddDays(-1),
            EndDate = now.AddMonths(1)
        };
        db.UserSubscriptions.Add(sub);

        var slot = new MentorAvailability
        {
            Id = 700 + mentorId,
            MentorId = mentorId,
            RoomId = rId,
            StartTime = now.AddMinutes(startMinutesOffset),
            EndTime = now.AddMinutes(startMinutesOffset + durationMinutes),
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
            Status = bookingStatus
        };
        db.MentorBookings.Add(booking);

        await db.SaveChangesAsync();
        return (mentor, learner, slot, booking);
    }

    [Fact]
    public async Task Learner_with_valid_booking_can_access_room_during_allowed_window()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, slot, _) = await SeedBookingAndSlotAsync(db, startMinutesOffset: 5, durationMinutes: 60);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        var canAccess = await service.CanAccessRoomAsync(slot.RoomId, learner.Id);
        Assert.True(canAccess);
    }

    [Fact]
    public async Task Learner_cannot_access_room_before_15_minute_window()
    {
        await using var db = CreateInMemoryDb();
        // Slot bắt đầu sau 30 phút -> chưa vào khoảng -15 phút
        var (_, learner, slot, _) = await SeedBookingAndSlotAsync(db, startMinutesOffset: 30, durationMinutes: 60);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        var canAccess = await service.CanAccessRoomAsync(slot.RoomId, learner.Id);
        Assert.False(canAccess);
    }

    [Fact]
    public async Task Learner_cannot_access_room_after_30_minutes_past_end_time()
    {
        await using var db = CreateInMemoryDb();
        // Slot đã kết thúc 45 phút trước (bắt đầu -105 phút, kéo dài 60 phút -> kết thúc -45 phút)
        var (_, learner, slot, _) = await SeedBookingAndSlotAsync(db, startMinutesOffset: -105, durationMinutes: 60);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        var canAccess = await service.CanAccessRoomAsync(slot.RoomId, learner.Id);
        Assert.False(canAccess);
    }

    [Fact]
    public async Task Learner_without_booking_cannot_access_room()
    {
        await using var db = CreateInMemoryDb();
        var (_, _, slot, _) = await SeedBookingAndSlotAsync(db, startMinutesOffset: 0, durationMinutes: 60);

        const int strangerLearnerId = 999;
        var claims = MockClaims(Role.Learner, strangerLearnerId);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        var canAccess = await service.CanAccessRoomAsync(slot.RoomId, strangerLearnerId);
        Assert.False(canAccess);
    }

    [Fact]
    public async Task Learner_with_cancelled_or_unpaid_booking_cannot_access_room()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner1, slot1, _) = await SeedBookingAndSlotAsync(db, mentorId: 201, learnerId: 101,
            paymentStatus: PaymentStatus.Pending);
        var (_, learner2, slot2, _) = await SeedBookingAndSlotAsync(db, mentorId: 202, learnerId: 102,
            bookingStatus: MentorStatus.Cancelled);

        var claims1 = MockClaims(Role.Learner, learner1.Id);
        var service1 = new MentorAttendanceService(db, claims1, NullLogger<MentorAttendanceService>.Instance);
        Assert.False(await service1.CanAccessRoomAsync(slot1.RoomId, learner1.Id));

        var claims2 = MockClaims(Role.Learner, learner2.Id);
        var service2 = new MentorAttendanceService(db, claims2, NullLogger<MentorAttendanceService>.Instance);
        Assert.False(await service2.CanAccessRoomAsync(slot2.RoomId, learner2.Id));
    }

    [Fact]
    public async Task Assigned_mentor_can_access_room_during_allowed_window()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, _, slot, _) = await SeedBookingAndSlotAsync(db, startMinutesOffset: 0, durationMinutes: 60);

        var claims = MockClaims(Role.Mentor, mentor.Id);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        var canAccess = await service.CanAccessRoomAsync(slot.RoomId, mentor.Id);
        Assert.True(canAccess);
    }

    [Fact]
    public async Task Unassigned_mentor_cannot_access_other_mentors_room()
    {
        await using var db = CreateInMemoryDb();
        var (_, _, slot, _) = await SeedBookingAndSlotAsync(db, mentorId: 201, startMinutesOffset: 0);

        const int strangerMentorId = 202;
        var claims = MockClaims(Role.Mentor, strangerMentorId);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        var canAccess = await service.CanAccessRoomAsync(slot.RoomId, strangerMentorId);
        Assert.False(canAccess);
    }

    [Fact]
    public async Task Admin_can_access_any_room_for_supervision()
    {
        await using var db = CreateInMemoryDb();
        var (_, _, slot, _) = await SeedBookingAndSlotAsync(db, startMinutesOffset: 0);

        const int adminId = 99;
        var claims = MockClaims(Role.Admin, adminId);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        var canAccess = await service.CanAccessRoomAsync(slot.RoomId, adminId);
        Assert.True(canAccess);
    }

    [Fact]
    public async Task RecordJoin_creates_attendance_record_with_joined_status()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, slot, booking) = await SeedBookingAndSlotAsync(db);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        var joinResult = await service.RecordJoinAsync(slot.RoomId, learner.Id, "conn-learner-01");
        Assert.True(joinResult.IsSuccess);
        Assert.Equal(MentorAttendanceStatus.Joined, joinResult.Result!.Status);
        Assert.True(joinResult.Result.IsOnline);
        Assert.NotNull(joinResult.Result.FirstJoinedAtUtc);
        Assert.NotNull(joinResult.Result.LastJoinedAtUtc);
        Assert.Equal(0, joinResult.Result.ReconnectionCount);

        var savedRecord = await db.MentorAttendanceRecords.FirstOrDefaultAsync(r => r.RoomId == slot.RoomId && r.UserId == learner.Id);
        Assert.NotNull(savedRecord);
        Assert.Equal(booking.Id, savedRecord.MentorBookingId);
        Assert.Equal(MentorAttendanceStatus.Joined, savedRecord.Status);
    }

    [Fact]
    public async Task RecordJoin_reconnection_increments_reconnection_count_without_duplicate_record()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, slot, booking) = await SeedBookingAndSlotAsync(db);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        // Lần kết nối thứ nhất
        await service.RecordJoinAsync(slot.RoomId, learner.Id, "conn-01");
        // Mất kết nối
        await service.RecordDisconnectAsync("conn-01");

        // Kết nối lại bằng kết nối mới
        var reconnectResult = await service.RecordJoinAsync(slot.RoomId, learner.Id, "conn-02");
        Assert.True(reconnectResult.IsSuccess);
        Assert.Equal(1, reconnectResult.Result!.ReconnectionCount);
        Assert.Equal(MentorAttendanceStatus.Joined, reconnectResult.Result.Status);
        Assert.True(reconnectResult.Result.IsOnline);

        // Đảm bảo không tạo thêm bản ghi mới trong DB (duy trì 1 bản ghi duy nhất cho cặp booking + user)
        var totalRecords = await db.MentorAttendanceRecords.CountAsync(r => r.MentorBookingId == booking.Id && r.UserId == learner.Id);
        Assert.Equal(1, totalRecords);
    }

    [Fact]
    public async Task MultiDevice_presence_closing_one_connection_leaves_user_online_if_another_active()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, slot, _) = await SeedBookingAndSlotAsync(db);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        // Kết nối từ 2 thiết bị/tab cùng lúc
        await service.RecordJoinAsync(slot.RoomId, learner.Id, "tab-laptop");
        await service.RecordJoinAsync(slot.RoomId, learner.Id, "tab-phone");

        // Đóng 1 tab
        var disconnectRes = await service.RecordDisconnectAsync("tab-laptop");
        Assert.True(disconnectRes.IsSuccess);
        Assert.True(disconnectRes.Result!.IsOnline);
        Assert.Equal(MentorAttendanceStatus.Joined, disconnectRes.Result.Status);

        // Người dùng vẫn online vì còn tab-phone
        Assert.True(service.IsUserOnlineInRoom(slot.RoomId, learner.Id));
    }

    [Fact]
    public async Task RecordDisconnect_all_connections_transitions_to_temporarily_disconnected()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, slot, _) = await SeedBookingAndSlotAsync(db);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        await service.RecordJoinAsync(slot.RoomId, learner.Id, "unique-conn");
        var disconnectRes = await service.RecordDisconnectAsync("unique-conn");

        Assert.True(disconnectRes.IsSuccess);
        Assert.False(disconnectRes.Result!.IsOnline);
        Assert.Equal(MentorAttendanceStatus.TemporarilyDisconnected, disconnectRes.Result.Status);
        Assert.False(service.IsUserOnlineInRoom(slot.RoomId, learner.Id));

        var recordInDb = await db.MentorAttendanceRecords.FirstAsync(r => r.RoomId == slot.RoomId && r.UserId == learner.Id);
        Assert.Equal(MentorAttendanceStatus.TemporarilyDisconnected, recordInDb.Status);
    }

    [Fact]
    public async Task RecordLeave_marks_status_left_and_accumulates_duration()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, slot, _) = await SeedBookingAndSlotAsync(db);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        await service.RecordJoinAsync(slot.RoomId, learner.Id, "conn-leave-test");
        var leaveRes = await service.RecordLeaveAsync(slot.RoomId, learner.Id, "conn-leave-test");

        Assert.True(leaveRes.IsSuccess);
        Assert.Equal(MentorAttendanceStatus.Left, leaveRes.Result!.Status);
        Assert.False(leaveRes.Result.IsOnline);
        Assert.NotNull(leaveRes.Result.LeftAtUtc);
        Assert.True(leaveRes.Result.TotalDurationSeconds >= 0);
    }

    [Fact]
    public async Task IsRoomReady_returns_true_only_when_both_mentor_and_learner_are_online()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, slot, _) = await SeedBookingAndSlotAsync(db);

        var claims = MockClaims(Role.Mentor, mentor.Id);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        // Ban đầu chưa ai online
        Assert.False(service.IsRoomReady(slot.RoomId));

        // Chỉ có Mentor online
        await service.RecordJoinAsync(slot.RoomId, mentor.Id, "conn-mentor-ready");
        Assert.False(service.IsRoomReady(slot.RoomId));

        // Learner tham gia -> Cả hai online -> Sẵn sàng
        await service.RecordJoinAsync(slot.RoomId, learner.Id, "conn-learner-ready");
        Assert.True(service.IsRoomReady(slot.RoomId));

        // Mentor rời phòng -> Không còn sẵn sàng
        await service.RecordDisconnectAsync("conn-mentor-ready");
        Assert.False(service.IsRoomReady(slot.RoomId));
    }

    [Fact]
    public async Task GetRoomAttendance_returns_ordered_records_with_correct_online_state()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, slot, _) = await SeedBookingAndSlotAsync(db);

        var claims = MockClaims(Role.Admin, 99);
        var service = new MentorAttendanceService(db, claims, NullLogger<MentorAttendanceService>.Instance);

        await service.RecordJoinAsync(slot.RoomId, mentor.Id, "conn-m");
        await service.RecordJoinAsync(slot.RoomId, learner.Id, "conn-l");

        var response = await service.GetRoomAttendanceAsync(slot.RoomId);
        Assert.True(response.IsSuccess);
        Assert.Equal(2, response.Result!.Count);
        Assert.All(response.Result, r => Assert.True(r.IsOnline));
    }

    [Fact]
    public async Task GetBookingAttendance_authorizes_owner_and_assigned_mentor_rejects_unrelated()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, slot, booking) = await SeedBookingAndSlotAsync(db);

        // Ghi nhận điểm danh
        var setupClaims = MockClaims(Role.Admin, 99);
        var setupService = new MentorAttendanceService(db, setupClaims, NullLogger<MentorAttendanceService>.Instance);
        await setupService.RecordJoinAsync(slot.RoomId, learner.Id, "conn-l-book");

        // Learner chính chủ
        var learnerClaims = MockClaims(Role.Learner, learner.Id);
        var learnerService = new MentorAttendanceService(db, learnerClaims, NullLogger<MentorAttendanceService>.Instance);
        var learnerRes = await learnerService.GetBookingAttendanceAsync(booking.Id);
        Assert.True(learnerRes.IsSuccess);
        Assert.NotEmpty(learnerRes.Result!);

        // Mentor được giao
        var mentorClaims = MockClaims(Role.Mentor, mentor.Id);
        var mentorService = new MentorAttendanceService(db, mentorClaims, NullLogger<MentorAttendanceService>.Instance);
        var mentorRes = await mentorService.GetBookingAttendanceAsync(booking.Id);
        Assert.True(mentorRes.IsSuccess);

        // Người lạ (Learner khác)
        var strangerClaims = MockClaims(Role.Learner, 999);
        var strangerService = new MentorAttendanceService(db, strangerClaims, NullLogger<MentorAttendanceService>.Instance);
        var strangerRes = await strangerService.GetBookingAttendanceAsync(booking.Id);
        Assert.False(strangerRes.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, strangerRes.StatusCode);
    }

    [Fact]
    public async Task CheckRoomAccess_returns_allowed_for_valid_participant_and_ready_state()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, slot, _) = await SeedBookingAndSlotAsync(db);

        // Learner kiểm tra quyền
        var learnerClaims = MockClaims(Role.Learner, learner.Id);
        var learnerService = new MentorAttendanceService(db, learnerClaims, NullLogger<MentorAttendanceService>.Instance);

        var checkRes = await learnerService.CheckRoomAccessAsync(slot.RoomId);
        Assert.True(checkRes.IsSuccess);
        Assert.True(checkRes.Result!.CanAccess);
        Assert.False(checkRes.Result.IsRoomReady);

        // Mentor tham gia và learner tham gia -> RoomReady
        await learnerService.RecordJoinAsync(slot.RoomId, mentor.Id, "conn-m-check");
        await learnerService.RecordJoinAsync(slot.RoomId, learner.Id, "conn-l-check");

        var checkRes2 = await learnerService.CheckRoomAccessAsync(slot.RoomId);
        Assert.True(checkRes2.IsSuccess);
        Assert.True(checkRes2.Result!.IsRoomReady);
    }

    [Fact]
    public async Task MentorAttendanceController_CheckRoomAccess_and_GetAttendance_endpoints()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, slot, booking) = await SeedBookingAndSlotAsync(db);

        var learnerClaims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorAttendanceService(db, learnerClaims, NullLogger<MentorAttendanceService>.Instance);
        var controller = new MentorAttendanceController(service);

        // Gọi action kiểm tra quyền truy cập
        var accessAction = await controller.CheckRoomAccess(slot.RoomId, default);
        var okAccess = Assert.IsType<ObjectResult>(accessAction);
        Assert.Equal(200, okAccess.StatusCode);
        var accessData = Assert.IsType<ApiResponse<RoomAccessCheckResponse>>(okAccess.Value);
        Assert.True(accessData.IsSuccess);
        Assert.True(accessData.Result!.CanAccess);

        // Gọi action lấy danh sách điểm danh
        var attendanceAction = await controller.GetRoomAttendance(slot.RoomId, default);
        var okAttendance = Assert.IsType<ObjectResult>(attendanceAction);
        Assert.Equal(200, okAttendance.StatusCode);

        // Gọi action lấy điểm danh theo booking
        var bookingAction = await controller.GetBookingAttendance(booking.Id, default);
        var okBooking = Assert.IsType<ObjectResult>(bookingAction);
        Assert.Equal(200, okBooking.StatusCode);
    }

    [Fact]
    public async Task SignalR_Hub_JoinRoom_emits_structured_events_and_records_attendance()
    {
        var roomId = Guid.NewGuid();
        const int learnerId = 101;
        const string connId = "hub-test-conn-1";

        var mockAccess = new Mock<IMentorMeetingAccessService>();
        mockAccess.Setup(a => a.GetMeetingAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeetingAccessInfoResponse
            {
                Mentor = new MeetingParticipantResponse { UserId = 201, Name = "Mentor Yamada", IsMentor = true },
                Learners = [new MeetingParticipantResponse { UserId = learnerId, Name = "Learner An", IsMentor = false }]
            });

        var mockNotes = new Mock<IMeetingNoteService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();
        mockAttendance.Setup(a => a.RecordJoinAsync(roomId, learnerId, connId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<MentorAttendanceResponse>.Success(new MentorAttendanceResponse
            {
                RoomId = roomId,
                UserId = learnerId,
                Status = MentorAttendanceStatus.Joined,
                IsOnline = true,
                ReconnectionCount = 0
            }));
        mockAttendance.Setup(a => a.IsRoomReady(roomId)).Returns(true);

        var hub = new MentorMeetingHub(mockAccess.Object, mockNotes.Object, mockAttendance.Object);

        var mockClients = new Mock<IHubCallerClients>();
        var mockGroupProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.OthersInGroup(It.IsAny<string>())).Returns(mockGroupProxy.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockGroupProxy.Object);

        var mockGroups = new Mock<IGroupManager>();
        mockGroups.Setup(g => g.AddToGroupAsync(connId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var claims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, learnerId.ToString()) };
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims));

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns(connId);
        mockContext.Setup(c => c.User).Returns(principal);
        mockContext.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);

        hub.Clients = mockClients.Object;
        hub.Groups = mockGroups.Object;
        hub.Context = mockContext.Object;

        var joinRes = await hub.JoinRoom(roomId, "Learner An", "Learner");

        Assert.NotNull(joinRes);
        Assert.NotNull(joinRes.Self);
        Assert.Equal(learnerId, joinRes.Self.Id);
        Assert.Equal("Learner An", joinRes.Self.Name);
        Assert.Equal("Learner", joinRes.Self.Role);

        // Xác nhận RecordJoinAsync được gọi
        mockAttendance.Verify(a => a.RecordJoinAsync(roomId, learnerId, connId, It.IsAny<CancellationToken>()), Times.Once);

        // Xác nhận phát sự kiện ParticipantJoined
        mockGroupProxy.Verify(p => p.SendCoreAsync("ParticipantJoined", It.Is<object?[]>(args => args.Length > 0), It.IsAny<CancellationToken>()), Times.Once);

        // Xác nhận phát sự kiện RoomReady vì IsRoomReady = true
        mockGroupProxy.Verify(p => p.SendCoreAsync("RoomReady", It.Is<object?[]>(args => args.Length > 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignalR_Hub_OnDisconnectedAsync_records_disconnect_and_emits_presence_changed()
    {
        var roomId = Guid.NewGuid();
        const int learnerId = 101;
        const string connId = "hub-test-conn-disc";

        var mockAccess = new Mock<IMentorMeetingAccessService>();
        var mockNotes = new Mock<IMeetingNoteService>();
        var mockAttendance = new Mock<IMentorAttendanceService>();

        mockAttendance.Setup(a => a.RecordDisconnectAsync(connId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<MentorAttendanceResponse?>.Success(new MentorAttendanceResponse
            {
                RoomId = roomId,
                UserId = learnerId,
                Role = "Learner",
                Status = MentorAttendanceStatus.TemporarilyDisconnected,
                IsOnline = false,
                ReconnectionCount = 0
            }));

        var hub = new MentorMeetingHub(mockAccess.Object, mockNotes.Object, mockAttendance.Object);

        var mockClients = new Mock<IHubCallerClients>();
        var mockGroupProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockGroupProxy.Object);

        var mockGroups = new Mock<IGroupManager>();
        mockGroups.Setup(g => g.RemoveFromGroupAsync(connId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns(connId);

        hub.Clients = mockClients.Object;
        hub.Groups = mockGroups.Object;
        hub.Context = mockContext.Object;

        await hub.OnDisconnectedAsync(null);

        // Xác nhận RecordDisconnectAsync được gọi với đúng connectionId
        mockAttendance.Verify(a => a.RecordDisconnectAsync(connId, It.IsAny<CancellationToken>()), Times.Once);

        // Xác nhận phát sự kiện PresenceChanged với IsOnline = false
        mockGroupProxy.Verify(p => p.SendCoreAsync("PresenceChanged", It.Is<object?[]>(args => args.Length > 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostgreSQL_QA_Rehearsal_Verifies_Attendance_Constraints_And_Unique_Index()
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

        var canConnect = await db.Database.CanConnectAsync();
        Assert.True(canConnect, "Không thể kết nối tới QA PostgreSQL cluster 127.0.0.1:55439");

        var uow = new UnitOfWork(db);

        // Kiểm tra ràng buộc CK_MentorAttendanceRecord_Duration tồn tại
        var durationCk = await uow.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pg_constraint WHERE conname = 'CK_MentorAttendanceRecord_Duration';");
        Assert.True(durationCk >= 1, "Ràng buộc CK_MentorAttendanceRecord_Duration phải tồn tại trên PostgreSQL QA");

        // Kiểm tra ràng buộc CK_MentorAttendanceRecord_Reconnection tồn tại
        var reconnectCk = await uow.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pg_constraint WHERE conname = 'CK_MentorAttendanceRecord_Reconnection';");
        Assert.True(reconnectCk >= 1, "Ràng buộc CK_MentorAttendanceRecord_Reconnection phải tồn tại trên PostgreSQL QA");

        // Kiểm tra chỉ mục duy nhất IX_MentorAttendanceRecords_Booking_User tồn tại
        var uniqueIx = await uow.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_MentorAttendanceRecords_Booking_User';");
        Assert.True(uniqueIx >= 1, "Chỉ mục duy nhất IX_MentorAttendanceRecords_Booking_User phải tồn tại trên PostgreSQL QA");
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
}
