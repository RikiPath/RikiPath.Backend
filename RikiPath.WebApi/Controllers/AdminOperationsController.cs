using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

/// <summary>API quản trị tài khoản người dùng.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminOperationsController(IUnitOfWork uow, IClaimService claimService) : ControllerBase
{
    /// <summary>Lấy danh sách tài khoản, có thể lọc theo role và tùy chọn bao gồm tài khoản đã vô hiệu hóa.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] Role? role,
        [FromQuery] bool includeInactive = false,
        bool includeDisabled = false,
        CancellationToken ct = default)
    {
        var query = uow.UserAccounts.Query().AsNoTracking();
        if (!includeInactive && !includeDisabled)
            query = query.Where(x => !x.IsDeleted);
        if (role is not null)
            query = query.Where(x => x.Role == role);

        var users = await query.OrderBy(x => x.Id).ToListAsync(ct);
        return Ok(users.Select(UserDto).ToList());
    }

    /// <summary>Lấy thông tin một tài khoản theo ID.</summary>
    [HttpGet("users/{id:int}")]
    public async Task<IActionResult> GetUser(int id, CancellationToken ct)
    {
        var user = await uow.UserAccounts.GetByIdAsync(id, ct);
        return user is null ? NotFound(new { errorMessage = "Không tìm thấy người dùng." }) : Ok(UserDto(user));
    }

    /// <summary>Tạo tài khoản người dùng và tạo password hash/salt.</summary>
    /// <remarks>Không cho phép tạo thêm tài khoản Admin vì hệ thống chỉ có một Admin.</remarks>
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequest r, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 6)
            return BadRequest(new { errorMessage = "Email và mật khẩu (ít nhất 6 ký tự) là bắt buộc." });
        if (!Enum.IsDefined(r.Role))
            return BadRequest(new { error = "Vai trò không hợp lệ.", errorMessage = "Vai trò không hợp lệ." });
        if (r.Role == Role.Admin)
            return BadRequest(new { error = "Không thể tạo thêm tài khoản Admin.", errorMessage = "Không thể tạo thêm tài khoản Admin." });

        var email = r.Email.Trim().ToLowerInvariant();
        if (await uow.UserAccounts.AnyAsync(x => x.Email != null && x.Email.ToLower() == email, ct))
            return Conflict(new { error = "Email đã được sử dụng.", errorMessage = "Email đã được sử dụng." });

        var actor = claimService.GetUserClaim();
        using var hmac = new HMACSHA512();
        var (firstName, lastName) = UserNameHelper.SplitFullName(r.FullName);
        var now = DateTime.UtcNow;
        var user = new UserAccount
        {
            Email = email,
            PasswordSalt = hmac.Key,
            PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(r.Password)),
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = r.PhoneNumber?.Trim(),
            Role = r.Role,
            IsEmailVerified = r.IsEmailVerified,
            CreatedDate = now,
        };

        await uow.ExecuteInTransactionAsync(async token =>
        {
            await uow.UserAccounts.AddAsync(user, token);
            await uow.SaveChangesAsync(token);

            if (user.Role == Role.Mentor)
            {
                await uow.MentorProfiles.AddAsync(new MentorProfile
                {
                    MentorId = user.Id,
                    VerificationStatus = MentorVerificationStatus.Unverified,
                    CreatedDate = now,
                }, token);
            }

            await uow.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = actor.Id,
                EntityType = "UserAccount",
                EntityId = user.Id,
                Action = "Created",
                AfterJson = JsonSerializer.Serialize(new { user.Id, user.Email, Role = user.Role.ToString(), IsActive = true }),
                CreatedAtUtc = now,
            }, token);
            await uow.SaveChangesAsync(token);
        }, ct);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, UserDto(user));
    }

    /// <summary>Cập nhật thông tin hồ sơ, role hoặc trạng thái hoạt động của tài khoản.</summary>
    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] AdminUpdateUserRequest r, CancellationToken ct)
    {
        var user = await uow.UserAccounts.GetByIdAsync(id, ct);
        if (user is null) return NotFound(new { errorMessage = "Không tìm thấy người dùng." });
        if (!Enum.IsDefined(r.Role))
            return BadRequest(new { error = "Vai trò không hợp lệ.", errorMessage = "Vai trò không hợp lệ." });

        var actor = claimService.GetUserClaim();
        if (user.Id == actor.Id && !r.IsActive)
            return BadRequest(new { error = "Không thể tự vô hiệu hóa tài khoản Admin đang đăng nhập.", errorMessage = "Không thể tự vô hiệu hóa tài khoản Admin đang đăng nhập." });
        if (user.Role == Role.Admin && (r.Role != Role.Admin || !r.IsActive))
            return BadRequest(new { error = "Không thể hạ quyền hoặc vô hiệu hóa tài khoản Admin duy nhất.", errorMessage = "Không thể hạ quyền hoặc vô hiệu hóa tài khoản Admin duy nhất." });
        if (user.Role != Role.Admin && r.Role == Role.Admin)
            return BadRequest(new { error = "Không thể cấp thêm quyền Admin.", errorMessage = "Không thể cấp thêm quyền Admin." });

        var deactivating = !user.IsDeleted && !r.IsActive;
        var demotingMentor = user.Role == Role.Mentor && r.Role != Role.Mentor;
        if ((deactivating || demotingMentor) && await HasActiveMentorCommitmentsAsync(user.Id, ct))
        {
            return Conflict(new
            {
                error = "Giáo viên đang phụ trách lớp học hoặc có khung giờ đã duyệt có học viên đặt. Hãy điều phối lại lớp/lịch trước khi khóa hoặc đổi vai trò.",
                errorMessage = "Giáo viên đang phụ trách lớp học hoặc có khung giờ đã duyệt có học viên đặt. Hãy điều phối lại lớp/lịch trước khi khóa hoặc đổi vai trò.",
            });
        }

        var beforeJson = JsonSerializer.Serialize(new
        {
            user.Id,
            user.Email,
            FullName = UserNameHelper.FormatFullName(user.FirstName, user.LastName),
            user.PhoneNumber,
            Role = user.Role.ToString(),
            IsActive = !user.IsDeleted,
        });

        var (firstName, lastName) = UserNameHelper.SplitFullName(r.FullName);
        var now = DateTime.UtcNow;
        var roleChanged = user.Role != Role.Admin && user.Role != r.Role;
        user.FirstName = firstName;
        user.LastName = lastName;
        user.PhoneNumber = r.PhoneNumber?.Trim();
        if (user.Role != Role.Admin) user.Role = r.Role;
        user.IsDeleted = !r.IsActive;
        if (roleChanged || !r.IsActive)
            user.TokenVersion++;
        user.ModifiedDate = now;

        var afterJson = JsonSerializer.Serialize(new
        {
            user.Id,
            user.Email,
            FullName = UserNameHelper.FormatFullName(user.FirstName, user.LastName),
            user.PhoneNumber,
            Role = user.Role.ToString(),
            IsActive = !user.IsDeleted,
        });

        await uow.ExecuteInTransactionAsync(async token =>
        {
            uow.UserAccounts.Update(user);
            await uow.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = actor.Id,
                EntityType = "UserAccount",
                EntityId = user.Id,
                Action = deactivating ? "Deactivated" : "Updated",
                BeforeJson = beforeJson,
                AfterJson = afterJson,
                CreatedAtUtc = now,
            }, token);
            await uow.SaveChangesAsync(token);
        }, ct);

        return Ok(UserDto(user));
    }

    /// <summary>Vô hiệu hóa tài khoản bằng soft delete.</summary>
    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeactivateUser(int id, CancellationToken ct)
    {
        var user = await uow.UserAccounts.GetByIdAsync(id, ct);
        if (user is null) return NotFound(new { errorMessage = "Không tìm thấy người dùng." });

        var actor = claimService.GetUserClaim();
        if (user.Id == actor.Id || user.Role == Role.Admin)
            return BadRequest(new { error = "Không thể vô hiệu hóa tài khoản Admin duy nhất.", errorMessage = "Không thể vô hiệu hóa tài khoản Admin duy nhất." });

        if (user.Role == Role.Mentor && await HasActiveMentorCommitmentsAsync(user.Id, ct))
        {
            return Conflict(new
            {
                error = "Giáo viên đang phụ trách lớp học hoặc có khung giờ đã duyệt có học viên đặt. Hãy điều phối lại lớp/lịch trước khi khóa.",
                errorMessage = "Giáo viên đang phụ trách lớp học hoặc có khung giờ đã duyệt có học viên đặt. Hãy điều phối lại lớp/lịch trước khi khóa.",
            });
        }

        var now = DateTime.UtcNow;
        user.IsDeleted = true;
        user.TokenVersion++;
        user.ModifiedDate = now;

        await uow.ExecuteInTransactionAsync(async token =>
        {
            uow.UserAccounts.Update(user);
            await uow.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = actor.Id,
                EntityType = "UserAccount",
                EntityId = user.Id,
                Action = "Deactivated",
                BeforeJson = JsonSerializer.Serialize(new { user.Id, user.Email, Role = user.Role.ToString(), IsActive = true }),
                AfterJson = JsonSerializer.Serialize(new { user.Id, user.Email, Role = user.Role.ToString(), IsActive = false }),
                CreatedAtUtc = now,
            }, token);
            await uow.SaveChangesAsync(token);
        }, ct);

        return NoContent();
    }

    private async Task<bool> HasActiveMentorCommitmentsAsync(int mentorId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var hasCohorts = await uow.StudyCohorts.Query()
            .AnyAsync(c => c.MentorId == mentorId && (c.Status == "Published" || c.Status == "Draft") && c.EndsAt > now, ct);
        if (hasCohorts) return true;

        return await uow.MentorAvailabilities.Query()
            .AnyAsync(a => a.MentorId == mentorId && !a.IsDeleted && a.IsApproved && a.BookedCount > 0 && a.StartTime > now, ct);
    }

    private static object UserDto(UserAccount u) => new
    {
        u.Id,
        u.Email,
        FullName = UserNameHelper.FormatFullName(u.FirstName, u.LastName),
        u.PhoneNumber,
        Role = u.Role.ToString(),
        u.IsEmailVerified,
        u.IsPhoneVerified,
        IsActive = !u.IsDeleted,
        u.CreatedDate,
        LastActivityAt = u.ModifiedDate ?? u.CreatedDate,
    };
}

public sealed class AdminCreateUserRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public Role Role { get; set; } = Role.Learner;
    public bool IsEmailVerified { get; set; }
}

public sealed class AdminUpdateUserRequest
{
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public Role Role { get; set; }
    public bool IsActive { get; set; }
}
