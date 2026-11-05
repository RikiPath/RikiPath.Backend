using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorDiscovery;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using Xunit;

namespace RikiPath.Tests;

public sealed class MentorDiscoveryTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static UserAccount CreateMentor(int id, string firstName, string lastName, MentorVerificationStatus status, string? jlpt = "N1", string? expertise = "JLPT Luyện đề", int yoe = 5)
    {
        var user = new UserAccount
        {
            Id = id,
            Email = $"mentor{id}@rikipath.local",
            FirstName = firstName,
            LastName = lastName,
            Role = Role.Mentor,
            AvatarUrl = $"https://example.com/avatar{id}.png",
            PasswordHash = [],
            PasswordSalt = []
        };

        user.MentorProfile = new MentorProfile
        {
            MentorId = id,
            Bio = $"Tiểu sử giảng viên {firstName} {lastName}",
            Expertise = expertise,
            JlptLevel = jlpt,
            YearsOfExperience = yoe,
            Workplace = "Riki Nihongo Academy",
            VerificationDocumentUrl = "https://example.com/private-cert.pdf",
            RejectionReason = status == MentorVerificationStatus.Rejected ? "Chứng chỉ không hợp lệ" : null,
            ReviewNotes = "Ghi chú nội bộ admin",
            VerificationStatus = status,
            ReviewedByAdminName = status == MentorVerificationStatus.Approved ? "Admin System" : null,
            ReviewedAt = status == MentorVerificationStatus.Approved ? DateTime.UtcNow.AddDays(-10) : null,
            CreatedDate = DateTime.UtcNow.AddDays(-30)
        };

        return user;
    }

    private static MentorAvailability CreateSlot(
        int id, int mentorId, DateTime startUtc, DateTime endUtc,
        bool isApproved = true, bool isDeleted = false, int bookedCount = 0, int maxLearners = 1,
        DateTime? rejectedAt = null, int? policyVersion = 3)
    {
        return new MentorAvailability
        {
            Id = id,
            MentorId = mentorId,
            StartTime = startUtc,
            EndTime = endUtc,
            IsApproved = isApproved,
            IsDeleted = isDeleted,
            BookedCount = bookedCount,
            MaxLearners = maxLearners,
            RejectedAt = rejectedAt,
            PolicyVersion = policyVersion,
            GenerationKey = $"gen-{mentorId}-{id}",
            CreatedDate = DateTime.UtcNow.AddDays(-1)
        };
    }

    // 1. Approved Mentor xuất hiện trong danh sách tìm kiếm
    [Fact]
    public async Task SearchMentors_WhenApproved_AppearsInDiscoveryList()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(1, "Yamada", "Taro", MentorVerificationStatus.Approved));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.SearchMentorsAsync(new MentorDiscoveryFilterRequest());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Result!.Items);
        Assert.Equal(1, result.Result.Items[0].MentorId);
        Assert.Equal("Yamada Taro", result.Result.Items[0].Name);
        Assert.True(result.Result.Items[0].IsBookable);
    }

    // 2. Unverified Mentor bị loại khỏi danh sách tìm kiếm
    [Fact]
    public async Task SearchMentors_WhenUnverified_ExcludedFromDiscovery()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(2, "Nguyen", "Van A", MentorVerificationStatus.Unverified));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.SearchMentorsAsync(new MentorDiscoveryFilterRequest());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Result!.Items);
    }

    // 3. Rejected Mentor bị loại khỏi danh sách tìm kiếm
    [Fact]
    public async Task SearchMentors_WhenRejected_ExcludedFromDiscovery()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(3, "Le", "Thi B", MentorVerificationStatus.Rejected));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.SearchMentorsAsync(new MentorDiscoveryFilterRequest());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Result!.Items);
    }

    // 4. Mentor bị thu hồi Approved biến mất khỏi kết quả tìm kiếm
    [Fact]
    public async Task SearchMentors_WhenApprovedRevoked_DisappearsFromDiscovery()
    {
        await using var db = CreateInMemoryDb();
        var mentor = CreateMentor(4, "Tran", "Van C", MentorVerificationStatus.Approved);
        db.Users.Add(mentor);
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var initialResult = await service.SearchMentorsAsync(new MentorDiscoveryFilterRequest());
        Assert.Single(initialResult.Result!.Items);

        // Thu hồi trạng thái Approved sang ChangesRequested hoặc Rejected
        mentor.MentorProfile!.VerificationStatus = MentorVerificationStatus.ChangesRequested;
        await db.SaveChangesAsync();

        var afterRevocationResult = await service.SearchMentorsAsync(new MentorDiscoveryFilterRequest());
        Assert.Empty(afterRevocationResult.Result!.Items);
    }

    // 5. Public profile không để lộ thông tin xác minh nhạy cảm
    [Fact]
    public async Task GetMentorProfile_PublicView_DoesNotLeakPrivateVerificationFields()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(5, "Suzuki", "Ichiro", MentorVerificationStatus.Approved));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.GetMentorProfileAsync(5);

        Assert.True(result.IsSuccess);
        Assert.Equal("Suzuki Ichiro", result.Result!.Name);
        Assert.True(result.Result.IsBookable);

        // Kiểm tra qua reflection rằng không có các trường nội bộ/nhạy cảm
        var type = typeof(RikiPath.Application.Responses.MentorProfile.PublicMentorProfileResponse);
        Assert.Null(type.GetProperty("VerificationDocumentUrl"));
        Assert.Null(type.GetProperty("RejectionReason"));
        Assert.Null(type.GetProperty("ReviewNotes"));
        Assert.Null(type.GetProperty("ReviewedByAdminName"));
        Assert.Null(type.GetProperty("ReviewedAt"));
    }

    // 6. Tìm kiếm theo tên (khớp từng phần FirstName, LastName hoặc FullName)
    [Fact]
    public async Task SearchMentors_ByName_ReturnsMatchingMentors()
    {
        await using var db = CreateInMemoryDb();
        db.Users.AddRange(
            CreateMentor(6, "Kenji", "Sato", MentorVerificationStatus.Approved),
            CreateMentor(7, "Akiko", "Tanaka", MentorVerificationStatus.Approved),
            CreateMentor(8, "Ken", "Watanabe", MentorVerificationStatus.Approved));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);

        var result = await service.SearchMentorsAsync(new MentorDiscoveryFilterRequest { SearchTerm = "Ken" });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Result!.Items.Count);
        Assert.Contains(result.Result.Items, m => m.MentorId == 6);
        Assert.Contains(result.Result.Items, m => m.MentorId == 8);
        Assert.DoesNotContain(result.Result.Items, m => m.MentorId == 7);
    }

    // 7. Lọc theo trình độ JLPT
    [Fact]
    public async Task SearchMentors_FilterByJlpt_ReturnsMatchingMentors()
    {
        await using var db = CreateInMemoryDb();
        db.Users.AddRange(
            CreateMentor(9, "Sensei", "N1", MentorVerificationStatus.Approved, jlpt: "N1"),
            CreateMentor(10, "Sensei", "N2", MentorVerificationStatus.Approved, jlpt: "N2"));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.SearchMentorsAsync(new MentorDiscoveryFilterRequest { JlptLevel = "N1" });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Result!.Items);
        Assert.Equal(9, result.Result.Items[0].MentorId);
    }

    // 8. Lọc theo chuyên môn (Expertise)
    [Fact]
    public async Task SearchMentors_FilterByExpertise_ReturnsMatchingMentors()
    {
        await using var db = CreateInMemoryDb();
        db.Users.AddRange(
            CreateMentor(11, "Kaiwa", "Master", MentorVerificationStatus.Approved, expertise: "Kaiwa Giao tiếp thương mại"),
            CreateMentor(12, "Grammar", "Master", MentorVerificationStatus.Approved, expertise: "Ngữ pháp Dokkai"));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.SearchMentorsAsync(new MentorDiscoveryFilterRequest { Expertise = "Kaiwa" });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Result!.Items);
        Assert.Equal(11, result.Result.Items[0].MentorId);
    }

    // 9. Giới hạn phân trang (clamp pageSize tối đa 50, page tối thiểu 1)
    [Fact]
    public async Task SearchMentors_PaginationBoundaries_ClampsPageAndPageSize()
    {
        await using var db = CreateInMemoryDb();
        for (int i = 1; i <= 60; i++)
        {
            db.Users.Add(CreateMentor(100 + i, $"Mentor{i}", "Test", MentorVerificationStatus.Approved));
        }
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);

        // Yêu cầu pageSize = 200 vượt ngưỡng 50 -> clamp về 50
        var result = await service.SearchMentorsAsync(new MentorDiscoveryFilterRequest { Page = -5, PageSize = 200 });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Result!.Page);
        Assert.Equal(50, result.Result.PageSize);
        Assert.Equal(60, result.Result.TotalCount);
        Assert.Equal(50, result.Result.Items.Count);
    }

    // 10. Thứ tự sắp xếp ổn định (Deterministic sorting)
    [Fact]
    public async Task SearchMentors_DeterministicSorting_OrdersCorrectly()
    {
        await using var db = CreateInMemoryDb();
        db.Users.AddRange(
            CreateMentor(21, "Binh", "Nguyen", MentorVerificationStatus.Approved, yoe: 2),
            CreateMentor(22, "An", "Tran", MentorVerificationStatus.Approved, yoe: 10),
            CreateMentor(23, "Cuong", "Le", MentorVerificationStatus.Approved, yoe: 6));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);

        // Mặc định sắp xếp theo số năm kinh nghiệm giảm dần
        var defaultResult = await service.SearchMentorsAsync(new MentorDiscoveryFilterRequest());
        Assert.Equal(new[] { 22, 23, 21 }, defaultResult.Result!.Items.Select(x => x.MentorId));

        // Sắp xếp theo tên tăng dần
        var nameResult = await service.SearchMentorsAsync(new MentorDiscoveryFilterRequest { SortBy = "name_asc" });
        Assert.Equal(new[] { 22, 21, 23 }, nameResult.Result!.Items.Select(x => x.MentorId));
    }

    // 11. Duyệt khung giờ khả dụng trong tương lai (90 phút chuẩn)
    [Fact]
    public async Task GetPublicSlots_FutureAvailableSlots_ReturnedSuccessfully()
    {
        await using var db = CreateInMemoryDb();
        var mentor = CreateMentor(30, "Sensei", "Valid", MentorVerificationStatus.Approved);
        db.Users.Add(mentor);

        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(9); // Ngày mai 09:00 UTC
        var end = start.AddMinutes(90);
        db.MentorAvailabilities.Add(CreateSlot(301, 30, start, end));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.GetPublicSlotsAsync(30, new MentorSlotBrowsingRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Result!.TotalAvailableSlots);
        Assert.Single(result.Result.DailySlots);
        Assert.Equal(301, result.Result.DailySlots[0].Slots[0].SlotId);
        Assert.Equal(90, result.Result.DailySlots[0].Slots[0].DurationMinutes);
    }

    // 12. Khung giờ bị vô hiệu hóa (IsDeleted == true hoặc IsApproved == false) bị loại
    [Fact]
    public async Task GetPublicSlots_DisabledSlots_Excluded()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(31, "Sensei", "Slots", MentorVerificationStatus.Approved));

        var start = DateTime.UtcNow.AddDays(2).Date.AddHours(14);
        db.MentorAvailabilities.AddRange(
            CreateSlot(311, 31, start, start.AddMinutes(90), isDeleted: true),
            CreateSlot(312, 31, start.AddHours(2), start.AddHours(2).AddMinutes(90), isApproved: false),
            CreateSlot(313, 31, start.AddHours(4), start.AddHours(4).AddMinutes(90), rejectedAt: DateTime.UtcNow));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.GetPublicSlotsAsync(31, new MentorSlotBrowsingRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Result!.TotalAvailableSlots);
    }

    // 13. Khung giờ đã có người đặt (BookedCount > 0) bị loại
    [Fact]
    public async Task GetPublicSlots_BookedSlots_Excluded()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(32, "Sensei", "Booked", MentorVerificationStatus.Approved));

        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        db.MentorAvailabilities.Add(CreateSlot(321, 32, start, start.AddMinutes(90), bookedCount: 1));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.GetPublicSlotsAsync(32, new MentorSlotBrowsingRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Result!.TotalAvailableSlots);
    }

    // 14. Khung giờ đang có Active Reservation Hold bị loại khỏi danh sách duyệt
    [Fact]
    public async Task GetPublicSlots_ActiveReservationHold_ExcludesSlot()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(33, "Sensei", "Reserve", MentorVerificationStatus.Approved));

        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(15);
        var end = start.AddMinutes(90);
        db.MentorAvailabilities.Add(CreateSlot(331, 33, start, end, bookedCount: 0));

        // Active reservation: ReleasedAtUtc == null và EndUtc còn trong tương lai
        db.MentorBookingReservations.Add(new MentorBookingReservation
        {
            Id = 1,
            MentorId = 33,
            MentorBookingId = 999,
            MentorAvailabilityId = 331,
            LocalBusinessDate = DateOnly.FromDateTime(start),
            StartUtc = start,
            EndUtc = end,
            RestProtectedUntilUtc = end.AddMinutes(15),
            PolicyVersion = 3,
            CreatedAtUtc = DateTime.UtcNow,
            ReleasedAtUtc = null
        });
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.GetPublicSlotsAsync(33, new MentorSlotBrowsingRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Result!.TotalAvailableSlots);
    }

    // 15. Reservation đã được nhả (ReleasedAtUtc != null) phục hồi hiển thị khung giờ
    [Fact]
    public async Task GetPublicSlots_ReleasedReservation_RestoresSlotVisibility()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(34, "Sensei", "Released", MentorVerificationStatus.Approved));

        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(16);
        var end = start.AddMinutes(90);
        db.MentorAvailabilities.Add(CreateSlot(341, 34, start, end, bookedCount: 0));

        // Released reservation: ReleasedAtUtc có giá trị
        db.MentorBookingReservations.Add(new MentorBookingReservation
        {
            Id = 2,
            MentorId = 34,
            MentorBookingId = 998,
            MentorAvailabilityId = 341,
            LocalBusinessDate = DateOnly.FromDateTime(start),
            StartUtc = start,
            EndUtc = end,
            RestProtectedUntilUtc = end.AddMinutes(15),
            PolicyVersion = 3,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            ReleasedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        });
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.GetPublicSlotsAsync(34, new MentorSlotBrowsingRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Result!.TotalAvailableSlots);
        Assert.Equal(341, result.Result.DailySlots[0].Slots[0].SlotId);
    }

    // 16. Khung giờ đã qua trong quá khứ bị loại
    [Fact]
    public async Task GetPublicSlots_PastExpiredSlots_Excluded()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(35, "Sensei", "Past", MentorVerificationStatus.Approved));

        var pastStart = DateTime.UtcNow.AddHours(-3);
        db.MentorAvailabilities.Add(CreateSlot(351, 35, pastStart, pastStart.AddMinutes(90)));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.GetPublicSlotsAsync(35, new MentorSlotBrowsingRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Result!.TotalAvailableSlots);
    }

    // 17. Ranh giới Lead-time tối thiểu 2 giờ được áp dụng chặt chẽ
    [Fact]
    public async Task GetPublicSlots_LeadTimeBoundary_ExcludesSlotsStartingWithinTwoHours()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(36, "Sensei", "LeadTime", MentorVerificationStatus.Approved));

        var now = DateTime.UtcNow;
        var slotTooSoon = now.AddMinutes(90); // 1.5 giờ tới -> vi phạm lead-time 2 giờ
        var slotValid = now.AddHours(3);      // 3 giờ tới -> hợp lệ

        db.MentorAvailabilities.AddRange(
            CreateSlot(361, 36, slotTooSoon, slotTooSoon.AddMinutes(90)),
            CreateSlot(362, 36, slotValid, slotValid.AddMinutes(90)));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.GetPublicSlotsAsync(36, new MentorSlotBrowsingRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Result!.TotalAvailableSlots);
        Assert.Equal(362, result.Result.DailySlots[0].Slots[0].SlotId);
    }

    // 18. Ranh giới Horizon tối đa 30 ngày được kiểm tra và chặn
    [Fact]
    public async Task GetPublicSlots_HorizonBoundary_RejectsRangesExceedingThirtyDays()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(37, "Sensei", "Horizon", MentorVerificationStatus.Approved));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);

        var request = new MentorSlotBrowsingRequest
        {
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(35)) // 35 ngày > 30 ngày
        };

        var result = await service.GetPublicSlotsAsync(37, request);

        Assert.False(result.IsSuccess);
        Assert.Contains("30 ngày", result.ErrorMessage);
    }

    // 19. Khung giờ lịch sử 50/60 phút không được trả về như khung giờ 90 phút chuẩn
    [Fact]
    public async Task GetPublicSlots_LegacyFiftyOrSixtyMinuteSlots_ExcludedFromNinetyMinuteBrowsing()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(38, "Sensei", "Legacy", MentorVerificationStatus.Approved));

        var start = DateTime.UtcNow.AddDays(3).Date.AddHours(10);
        db.MentorAvailabilities.AddRange(
            CreateSlot(381, 38, start, start.AddMinutes(50)), // Legacy 50 phút
            CreateSlot(382, 38, start.AddHours(2), start.AddHours(2).AddMinutes(60)), // Legacy 60 phút
            CreateSlot(383, 38, start.AddHours(4), start.AddHours(4).AddMinutes(90))); // Chuẩn 90 phút
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.GetPublicSlotsAsync(38, new MentorSlotBrowsingRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Result!.TotalAvailableSlots);
        Assert.Equal(383, result.Result.DailySlots[0].Slots[0].SlotId);
        Assert.Equal(90, result.Result.DailySlots[0].Slots[0].DurationMinutes);
    }

    // 20. Direct slotId access kiểm tra nghiêm ngặt điều kiện công khai và trạng thái Approved
    [Fact]
    public async Task GetPublicSlotById_DirectAccess_EnforcesVerificationGate()
    {
        await using var db = CreateInMemoryDb();
        var unapprovedMentor = CreateMentor(39, "Sensei", "Unapproved", MentorVerificationStatus.Pending);
        db.Users.Add(unapprovedMentor);

        var start = DateTime.UtcNow.AddDays(2).Date.AddHours(9);
        db.MentorAvailabilities.Add(CreateSlot(391, 39, start, start.AddMinutes(90)));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);

        // Truy cập trực tiếp qua SlotId của mentor chưa Approved -> phải trả về 404 NotFound
        var result = await service.GetPublicSlotByIdAsync(391);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    // 21. Xử lý chuyển đổi múi giờ và ranh giới ngày chính xác
    [Fact]
    public async Task GetPublicSlots_TimezoneConversion_GroupsByCorrectLocalDate()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(40, "Sensei", "Timezone", MentorVerificationStatus.Approved));

        // 23:30 UTC ngày 2026-11-05 tương đương 06:30 sáng ngày 2026-11-06 tại múi giờ Asia/Ho_Chi_Minh (+7)
        var utcSlotStart = new DateTime(2026, 11, 5, 23, 30, 0, DateTimeKind.Utc);
        var utcSlotEnd = utcSlotStart.AddMinutes(90);
        db.MentorAvailabilities.Add(CreateSlot(401, 40, utcSlotStart, utcSlotEnd));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var request = new MentorSlotBrowsingRequest
        {
            StartDate = new DateOnly(2026, 11, 6),
            EndDate = new DateOnly(2026, 11, 6),
            TimeZoneId = "Asia/Ho_Chi_Minh"
        };

        var result = await service.GetPublicSlotsAsync(40, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Result!.TotalAvailableSlots);
        Assert.Equal(new DateOnly(2026, 11, 6), result.Result.DailySlots[0].Date);
        Assert.Equal(new TimeOnly(6, 30), result.Result.DailySlots[0].Slots[0].LocalStart);
        Assert.Equal(new TimeOnly(8, 0), result.Result.DailySlots[0].Slots[0].LocalEnd);
        Assert.Equal("Morning", result.Result.DailySlots[0].Slots[0].DaypartCode);
    }

    // 22. Public API contract regression: Định dạng dữ liệu danh sách ngày khả dụng
    [Fact]
    public async Task GetAvailableDates_ReturnsDistinctSortedDates()
    {
        await using var db = CreateInMemoryDb();
        db.Users.Add(CreateMentor(41, "Sensei", "Dates", MentorVerificationStatus.Approved));

        var day1 = DateTime.UtcNow.AddDays(2).Date.AddHours(9);
        var day2 = DateTime.UtcNow.AddDays(3).Date.AddHours(14);
        db.MentorAvailabilities.AddRange(
            CreateSlot(411, 41, day1, day1.AddMinutes(90)),
            CreateSlot(412, 41, day1.AddHours(2), day1.AddHours(2).AddMinutes(90)),
            CreateSlot(413, 41, day2, day2.AddMinutes(90)));
        await db.SaveChangesAsync();

        var service = new MentorDiscoveryService(new UnitOfWork(db), NullLogger<MentorDiscoveryService>.Instance);
        var result = await service.GetAvailableDatesAsync(41, new MentorSlotBrowsingRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Result!.Count);
        Assert.True(result.Result[0] < result.Result[1]);
    }
}
