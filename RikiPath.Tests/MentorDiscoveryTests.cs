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
}
