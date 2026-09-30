using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

/// <summary>API quản trị người dùng, gói đăng ký, tính năng, lịch Mentor và thống kê toàn hệ thống.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminOperationsController(IUnitOfWork uow) : ControllerBase
{
    /// <summary>Lấy danh sách tài khoản, có thể lọc theo role và tùy chọn bao gồm tài khoản đã vô hiệu hóa.</summary>
    /// <param name="role">Role cần lọc; bỏ trống để lấy mọi role.</param>
    /// <param name="includeInactive">True để bao gồm tài khoản đã vô hiệu hóa.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] Role? role, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var users = await uow.UserAccounts.GetAllAsync(ct);
        var result = users.Where(x => (includeInactive || !x.IsDeleted) && (role is null || x.Role == role))
            .OrderBy(x => x.Id).Select(UserDto).ToList();
        return Ok(result);
    }

    /// <summary>Lấy thông tin một tài khoản theo ID.</summary>
    /// <param name="id">ID tài khoản.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpGet("users/{id:int}")]
    public async Task<IActionResult> GetUser(int id, CancellationToken ct)
    {
        var user = await uow.UserAccounts.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(UserDto(user));
    }

    /// <summary>Tạo tài khoản người dùng và tạo password hash/salt.</summary>
    /// <remarks>Không cho phép tạo thêm tài khoản Admin vì hệ thống chỉ có một Admin.</remarks>
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequest r, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 6)
            return BadRequest(new { error = "Email và mật khẩu (ít nhất 6 ký tự) là bắt buộc." });
        if (!Enum.IsDefined(r.Role)) return BadRequest(new { error = "Role không hợp lệ." });
        if (r.Role == Role.Admin) return BadRequest(new { error = "Không thể tạo thêm tài khoản Admin." });
        var email = r.Email.Trim().ToLowerInvariant();
        if (await uow.UserAccounts.AnyAsync(x => x.Email != null && x.Email.ToLower() == email, ct))
            return Conflict(new { error = "Email đã được sử dụng." });
        using var hmac = new HMACSHA512();
        var user = new UserAccount
        {
            Email = email, PasswordSalt = hmac.Key, PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(r.Password)),
            FirstName = r.FirstName, LastName = r.LastName, PhoneNumber = r.PhoneNumber, Role = r.Role,
            IsEmailVerified = r.IsEmailVerified, CreatedDate = DateTime.UtcNow
        };
        await uow.UserAccounts.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetUsers), UserDto(user));
    }

    /// <summary>Cập nhật thông tin hồ sơ, role hoặc trạng thái hoạt động của tài khoản.</summary>
    /// <remarks>Không cho phép tạo thêm Admin hoặc hạ quyền Admin duy nhất.</remarks>
    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] AdminUpdateUserRequest r, CancellationToken ct)
    {
        var user = await uow.UserAccounts.GetByIdAsync(id, ct);
        if (user is null || user.IsDeleted) return NotFound();
        if (!Enum.IsDefined(r.Role)) return BadRequest(new { error = "Role không hợp lệ." });
        if (user.Role == Role.Admin && r.Role != Role.Admin) return BadRequest(new { error = "Không thể hạ quyền Admin duy nhất." });
        if (user.Role != Role.Admin && r.Role == Role.Admin) return BadRequest(new { error = "Không thể cấp thêm role Admin." });
        user.FirstName = r.FirstName; user.LastName = r.LastName; user.PhoneNumber = r.PhoneNumber;
        if (user.Role != Role.Admin) user.Role = r.Role;
        user.IsDeleted = !r.IsActive; user.ModifiedDate = DateTime.UtcNow;
        uow.UserAccounts.Update(user); await uow.SaveChangesAsync(ct);
        return Ok(UserDto(user));
    }

    /// <summary>Vô hiệu hóa tài khoản bằng soft delete.</summary>
    /// <remarks>Không cho phép vô hiệu hóa Admin duy nhất.</remarks>
    /// <param name="id">ID tài khoản cần vô hiệu hóa.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeactivateUser(int id, CancellationToken ct)
    {
        var user = await uow.UserAccounts.GetByIdAsync(id, ct);
        if (user is null) return NotFound();
        if (user.Role == Role.Admin) return BadRequest(new { error = "Không thể vô hiệu hóa Admin duy nhất." });
        user.IsDeleted = true; user.ModifiedDate = DateTime.UtcNow;
        uow.UserAccounts.Update(user); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Lấy danh sách gói đăng ký đang hoạt động cùng các Feature được gán.</summary>
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var plans = await uow.SubscriptionPlans.Query().Include(x => x.Features).Where(x => !x.IsDeleted).OrderBy(x => x.SortOrder).ToListAsync(ct);
        return Ok(plans.Select(PlanDto));
    }

    /// <summary>Lấy chi tiết một gói đăng ký cùng danh sách Feature.</summary>
    /// <param name="id">ID gói đăng ký.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpGet("plans/{id:int}")]
    public async Task<IActionResult> GetPlan(int id, CancellationToken ct)
    {
        var plan = await uow.SubscriptionPlans.Query().Include(x => x.Features).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        return plan is null ? NotFound() : Ok(PlanDto(plan));
    }

    /// <summary>Tạo gói đăng ký và gán các Feature hiện có.</summary>
    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] SaveSubscriptionPlanRequest r, CancellationToken ct)
    {
        var plan = new SubscriptionPlan();
        var error = await ApplyPlanAsync(plan, r, ct);
        if (error is not null) return BadRequest(new { error });
        await uow.SubscriptionPlans.AddAsync(plan, ct); await uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetPlans), new { id = plan.Id }, PlanDto(plan));
    }

    /// <summary>Cập nhật thông tin và danh sách Feature của gói đăng ký.</summary>
    [HttpPut("plans/{id:int}")]
    public async Task<IActionResult> UpdatePlan(int id, [FromBody] SaveSubscriptionPlanRequest r, CancellationToken ct)
    {
        var plan = await uow.SubscriptionPlans.Query().Include(x => x.Features).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (plan is null) return NotFound();
        var error = await ApplyPlanAsync(plan, r, ct);
        if (error is not null) return BadRequest(new { error });
        plan.ModifiedDate = DateTime.UtcNow; uow.SubscriptionPlans.Update(plan); await uow.SaveChangesAsync(ct);
        return Ok(PlanDto(plan));
    }

    /// <summary>Vô hiệu hóa gói đăng ký bằng soft delete.</summary>
    /// <param name="id">ID gói đăng ký cần xóa.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpDelete("plans/{id:int}")]
    public async Task<IActionResult> DeletePlan(int id, CancellationToken ct)
    {
        var plan = await uow.SubscriptionPlans.GetByIdAsync(id, ct);
        if (plan is null || plan.IsDeleted) return NotFound();
        plan.IsDeleted = true; plan.IsActive = false; plan.ModifiedDate = DateTime.UtcNow;
        uow.SubscriptionPlans.Update(plan); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Lấy danh sách Feature chưa bị xóa.</summary>
    [HttpGet("features")]
    public async Task<IActionResult> GetFeatures(CancellationToken ct)
    {
        var result = (await uow.Features.GetAllAsync(ct)).Where(x => !x.IsDeleted).OrderBy(x => x.Name).Select(FeatureDto);
        return Ok(result);
    }

    /// <summary>Lấy chi tiết một Feature theo ID.</summary>
    /// <param name="id">ID Feature.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpGet("features/{id:int}")]
    public async Task<IActionResult> GetFeature(int id, CancellationToken ct)
    {
        var feature = await uow.Features.GetByIdAsync(id, ct);
        return feature is null || feature.IsDeleted ? NotFound() : Ok(FeatureDto(feature));
    }

    /// <summary>Tạo Feature mới để có thể gán vào Subscription Plan.</summary>
    [HttpPost("features")]
    public async Task<IActionResult> CreateFeature([FromBody] SaveFeatureRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest(new { error = "Tên Feature là bắt buộc." });
        var feature = new Feature { Name = r.Name.Trim(), Description = r.Description?.Trim() };
        await uow.Features.AddAsync(feature, ct); await uow.SaveChangesAsync(ct); return CreatedAtAction(nameof(GetFeatures), new { id = feature.Id }, FeatureDto(feature));
    }

    /// <summary>Cập nhật tên và mô tả của Feature.</summary>
    /// <param name="id">ID Feature cần cập nhật.</param>
    /// <param name="r">Tên và mô tả mới của Feature.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpPut("features/{id:int}")]
    public async Task<IActionResult> UpdateFeature(int id, [FromBody] SaveFeatureRequest r, CancellationToken ct)
    {
        var feature = await uow.Features.GetByIdAsync(id, ct);
        if (feature is null || feature.IsDeleted) return NotFound();
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest(new { error = "Tên Feature là bắt buộc." });
        feature.Name = r.Name.Trim(); feature.Description = r.Description?.Trim(); feature.ModifiedDate = DateTime.UtcNow;
        uow.Features.Update(feature); await uow.SaveChangesAsync(ct); return Ok(FeatureDto(feature));
    }

    /// <summary>Vô hiệu hóa Feature bằng soft delete.</summary>
    /// <param name="id">ID Feature cần xóa.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpDelete("features/{id:int}")]
    public async Task<IActionResult> DeleteFeature(int id, CancellationToken ct)
    {
        var feature = await uow.Features.GetByIdAsync(id, ct);
        if (feature is null || feature.IsDeleted) return NotFound();
        feature.IsDeleted = true; feature.ModifiedDate = DateTime.UtcNow;
        uow.Features.Update(feature); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Lấy danh sách Mentor booking, có thể lọc theo trạng thái.</summary>
    /// <param name="status">Trạng thái booking cần lọc; bỏ trống để lấy tất cả trạng thái.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpGet("mentor-bookings")]
    public async Task<IActionResult> GetMentorBookings([FromQuery] ConsultationStatus? status, CancellationToken ct)
    {
        var items = await uow.MentorBookings.Query().Include(x => x.UserSubscription).Include(x => x.MentorAvailability).ToListAsync(ct);
        var result = items.Where(x => !x.IsDeleted && (status is null || x.Status == status))
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new { x.Id, x.UserSubscriptionId, LearnerId = x.UserSubscription.UserId, x.MentorAvailabilityId, MentorId = x.MentorAvailability?.MentorId, x.Status, x.Question, x.ScheduledAt, x.MeetingLink, x.CompletedAt });
        return Ok(result);
    }

    /// <summary>Lấy các khung giờ của Mentor, có thể lọc theo ID Mentor.</summary>
    /// <param name="mentorId">ID Mentor cần lọc; bỏ trống để lấy lịch của mọi Mentor.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpGet("mentor-availabilities")]
    public async Task<IActionResult> GetMentorAvailabilities([FromQuery] int? mentorId, CancellationToken ct)
    {
        var rows = (await uow.MentorAvailabilities.GetAllAsync(ct)).Where(x => !x.IsDeleted && (mentorId is null || x.MentorId == mentorId)).OrderBy(x => x.StartTime);
        return Ok(rows.Select(x => new { x.Id, x.MentorId, x.StartTime, x.EndTime, x.IsBooked }));
    }

    /// <summary>Tạo khung giờ rảnh cho một tài khoản có role Mentor.</summary>
    [HttpPost("mentor-availabilities")]
    public async Task<IActionResult> CreateMentorAvailability([FromBody] SaveMentorAvailabilityRequest r, CancellationToken ct)
    {
        if (r.EndTime <= r.StartTime) return BadRequest(new { error = "EndTime phải sau StartTime." });
        var mentor = await uow.UserAccounts.GetByIdAsync(r.MentorId, ct);
        if (mentor is null || mentor.IsDeleted || mentor.Role != Role.Mentor) return BadRequest(new { error = "Mentor không tồn tại hoặc đang bị khóa." });
        var row = new MentorAvailability { MentorId = r.MentorId, StartTime = r.StartTime, EndTime = r.EndTime };
        await uow.MentorAvailabilities.AddAsync(row, ct); await uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetMentorAvailabilities), new { mentorId = row.MentorId }, new { row.Id, row.MentorId, row.StartTime, row.EndTime, row.IsBooked });
    }

    /// <summary>Đổi thời gian của khung giờ chưa được đặt.</summary>
    /// <param name="id">ID khung giờ.</param>
    /// <param name="r">Thời gian bắt đầu và kết thúc mới.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpPut("mentor-availabilities/{id:int}")]
    public async Task<IActionResult> UpdateMentorAvailability(int id, [FromBody] UpdateMentorAvailabilityRequest r, CancellationToken ct)
    {
        var row = await uow.MentorAvailabilities.GetByIdAsync(id, ct);
        if (row is null || row.IsDeleted) return NotFound();
        if (row.IsBooked) return Conflict(new { error = "Không thể đổi khung giờ đã được đặt." });
        if (r.EndTime <= r.StartTime) return BadRequest(new { error = "EndTime phải sau StartTime." });
        row.StartTime = r.StartTime; row.EndTime = r.EndTime; row.ModifiedDate = DateTime.UtcNow;
        uow.MentorAvailabilities.Update(row); await uow.SaveChangesAsync(ct);
        return Ok(new { row.Id, row.MentorId, row.StartTime, row.EndTime, row.IsBooked });
    }

    /// <summary>Xóa mềm khung giờ chưa được đặt.</summary>
    /// <param name="id">ID khung giờ.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpDelete("mentor-availabilities/{id:int}")]
    public async Task<IActionResult> DeleteMentorAvailability(int id, CancellationToken ct)
    {
        var row = await uow.MentorAvailabilities.GetByIdAsync(id, ct);
        if (row is null || row.IsDeleted) return NotFound();
        if (row.IsBooked) return Conflict(new { error = "Không thể xóa khung giờ đã được đặt." });
        row.IsDeleted = true; row.ModifiedDate = DateTime.UtcNow; uow.MentorAvailabilities.Update(row); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Cập nhật trạng thái booking, thời gian hẹn, link họp hoặc khung giờ Mentor.</summary>
    /// <remarks>Khi booking bị hủy, khung giờ đã đặt sẽ được giải phóng.</remarks>
    /// <param name="id">ID booking.</param>
    /// <param name="r">Trạng thái, thời gian hẹn, link họp và khung giờ mới nếu được chỉ định.</param>
    /// <param name="ct">Token hủy request.</param>
    [HttpPut("mentor-bookings/{id:int}")]
    public async Task<IActionResult> UpdateMentorBooking(int id, [FromBody] UpdateMentorBookingRequest r, CancellationToken ct)
    {
        var booking = await uow.MentorBookings.GetByIdAsync(id, ct);
        if (booking is null || booking.IsDeleted) return NotFound();
        if (r.MentorAvailabilityId is int slotId)
        {
            var slot = await uow.MentorAvailabilities.GetByIdAsync(slotId, ct);
            if (slot is null || slot.IsDeleted || (slot.IsBooked && slotId != booking.MentorAvailabilityId))
                return BadRequest(new { error = "Khung giờ không tồn tại hoặc đã được đặt." });
            if (booking.MentorAvailabilityId is int previousId && previousId != slotId)
            {
                var previous = await uow.MentorAvailabilities.GetByIdAsync(previousId, ct);
                if (previous is not null) { previous.IsBooked = false; uow.MentorAvailabilities.Update(previous); }
            }
            slot.IsBooked = true; uow.MentorAvailabilities.Update(slot); booking.MentorAvailabilityId = slotId;
        }
        if (r.Status == ConsultationStatus.Cancelled && booking.MentorAvailabilityId is int cancelledSlotId)
        {
            var cancelledSlot = await uow.MentorAvailabilities.GetByIdAsync(cancelledSlotId, ct);
            if (cancelledSlot is not null) { cancelledSlot.IsBooked = false; uow.MentorAvailabilities.Update(cancelledSlot); }
            booking.MentorAvailabilityId = null;
        }
        booking.Status = r.Status; booking.ScheduledAt = r.ScheduledAt; booking.MeetingLink = r.MeetingLink;
        booking.ModifiedDate = DateTime.UtcNow; uow.MentorBookings.Update(booking); await uow.SaveChangesAsync(ct);
        return Ok(new { booking.Id, booking.UserSubscriptionId, booking.MentorAvailabilityId, booking.Status, booking.ScheduledAt, booking.MeetingLink });
    }

    /// <summary>Lấy số liệu tổng quan về người dùng, subscriptions, booking, nội dung và hàng chờ kiểm duyệt.</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var users = (await uow.UserAccounts.GetAllAsync(ct)).Where(x => !x.IsDeleted).ToList();
        var lessons = await uow.Lessons.GetAllAsync(ct); var kanjis = await uow.Kanjis.GetAllAsync(ct);
        var vocab = await uow.Vocabularies.GetAllAsync(ct); var grammar = await uow.GrammarPatterns.GetAllAsync(ct);
        var tests = await uow.MockTests.GetAllAsync(ct); var exercises = await uow.PracticeExercises.GetAllAsync(ct);
        var subs = await uow.UserSubscriptions.GetAllAsync(ct); var bookings = await uow.MentorBookings.GetAllAsync(ct);
        return Ok(new
        {
            Users = users.Count,
            Learners = users.Count(x => x.Role == Role.Learner),
            ContentAuthors = users.Count(x => x.Role == Role.ContentAuthor),
            Mentors = users.Count(x => x.Role == Role.Mentor),
            Subscriptions = subs.Count(x => !x.IsDeleted),
            ActiveSubscriptions = subs.Count(x => !x.IsDeleted && x.PaymentStatus == PaymentStatus.Paid && x.StartDate <= DateTime.UtcNow && x.EndDate >= DateTime.UtcNow),
            PaidSubscriptionAmount = subs.Where(x => !x.IsDeleted && x.PaymentStatus == PaymentStatus.Paid).Sum(x => x.AmountPaid),
            MentorBookings = bookings.Count(x => !x.IsDeleted),
            MentorBookingsByStatus = Enum.GetValues<ConsultationStatus>().ToDictionary(x => x.ToString(), x => bookings.Count(b => !b.IsDeleted && b.Status == x)),
            Content = new { Lessons = Count(lessons), Kanjis = Count(kanjis), Vocabularies = Count(vocab), GrammarPatterns = Count(grammar), MockTests = Count(tests), PracticeExercises = Count(exercises) },
            PendingReview = new { Lessons = lessons.Count(x => !x.IsDeleted && x.Status == ContentStatus.PendingReview), Kanjis = kanjis.Count(x => !x.IsDeleted && x.Status == ContentStatus.PendingReview), Vocabularies = vocab.Count(x => !x.IsDeleted && x.Status == ContentStatus.PendingReview), GrammarPatterns = grammar.Count(x => !x.IsDeleted && x.Status == ContentStatus.PendingReview), MockTests = tests.Count(x => !x.IsDeleted && x.Status == ContentStatus.PendingReview), PracticeExercises = exercises.Count(x => !x.IsDeleted && x.Status == ContentStatus.PendingReview) }
        });
    }

    private async Task<string?> ApplyPlanAsync(SubscriptionPlan plan, SaveSubscriptionPlanRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.DurationDays < 0 || r.Price < 0 || r.AiGradingQuota < 0) return "Thông tin gói không hợp lệ.";
        var available = (await uow.Features.GetAllAsync(ct)).Where(x => !x.IsDeleted).ToList();
        var requested = r.FeatureIds.Distinct().ToHashSet();
        if (requested.Except(available.Select(x => x.Id)).Any()) return "Có Feature không tồn tại.";
        plan.Name = r.Name.Trim(); plan.Description = r.Description?.Trim(); plan.DurationDays = r.DurationDays; plan.Price = r.Price;
        plan.AiGradingQuota = r.AiGradingQuota; plan.IsPopular = r.IsPopular; plan.IsTrial = r.IsTrial; plan.IsActive = r.IsActive; plan.SortOrder = r.SortOrder;
        plan.Features = available.Where(x => requested.Contains(x.Id)).ToList();
        return null;
    }

    private static int Count<T>(IReadOnlyList<T> rows) where T : Base => rows.Count(x => !x.IsDeleted);
    private static object UserDto(UserAccount u) => new { u.Id, u.Email, u.FirstName, u.LastName, u.PhoneNumber, u.Role, u.IsEmailVerified, u.IsPhoneVerified, IsActive = !u.IsDeleted, u.CreatedDate };
    private static object PlanDto(SubscriptionPlan p) => new { p.Id, p.Name, p.Description, p.DurationDays, p.Price, p.AiGradingQuota, p.IsPopular, p.IsTrial, p.IsActive, p.SortOrder, Features = p.Features.Select(FeatureDto) };
    private static object FeatureDto(Feature f) => new { f.Id, f.Name, f.Description };
}

public sealed class AdminCreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public Role Role { get; set; } = Role.Learner;
    public bool IsEmailVerified { get; set; }
}

public sealed class AdminUpdateUserRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public Role Role { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SaveSubscriptionPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationDays { get; set; }
    public decimal Price { get; set; }
    public int AiGradingQuota { get; set; }
    public bool IsPopular { get; set; }
    public bool IsTrial { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public List<int> FeatureIds { get; set; } = [];
}

public sealed class SaveFeatureRequest { public string Name { get; set; } = string.Empty; public string? Description { get; set; } }
public sealed class UpdateMentorBookingRequest
{
    public int? MentorAvailabilityId { get; set; }
    public ConsultationStatus Status { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? MeetingLink { get; set; }
}
public sealed class SaveMentorAvailabilityRequest { public int MentorId { get; set; } public DateTime StartTime { get; set; } public DateTime EndTime { get; set; } }
public sealed class UpdateMentorAvailabilityRequest { public DateTime StartTime { get; set; } public DateTime EndTime { get; set; } }
