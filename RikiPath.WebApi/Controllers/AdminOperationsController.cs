using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

/// <summary>API quản trị tài khoản người dùng.</summary>
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
    public async Task<IActionResult> GetUsers([FromQuery] Role? role, [FromQuery] bool includeInactive = false, bool includeDisabled = false, CancellationToken ct = default)
    {
        var users = await uow.UserAccounts.GetAllAsync(ct);
        var result = users.Where(x => (includeInactive || includeDisabled || !x.IsDeleted) && (role is null || x.Role == role))
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
            return BadRequest(new { errorMessage = "Email và mật khẩu (ít nhất 6 ký tự) là bắt buộc." });
        if (!Enum.IsDefined(r.Role)) return BadRequest(new { error = "Role không hợp lệ." });
        if (r.Role == Role.Admin) return BadRequest(new { error = "Không thể tạo thêm tài khoản Admin." });
        var email = r.Email.Trim().ToLowerInvariant();
        if (await uow.UserAccounts.AnyAsync(x => x.Email != null && x.Email.ToLower() == email, ct))
            return Conflict(new { error = "Email đã được sử dụng." });
        using var hmac = new HMACSHA512();
        var user = new UserAccount
        {
            Email = email,
            PasswordSalt = hmac.Key,
            PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(r.Password)),
            FirstName = r.FirstName,
            LastName = r.LastName,
            PhoneNumber = r.PhoneNumber,
            Role = r.Role,
            IsEmailVerified = r.IsEmailVerified,
            CreatedDate = DateTime.UtcNow
        };
        await uow.UserAccounts.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, UserDto(user));
    }

    /// <summary>Cập nhật thông tin hồ sơ, role hoặc trạng thái hoạt động của tài khoản.</summary>
    /// <remarks>Không cho phép tạo thêm Admin hoặc hạ quyền Admin duy nhất.</remarks>
    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] AdminUpdateUserRequest r, CancellationToken ct)
    {
        var user = await uow.UserAccounts.GetByIdAsync(id, ct);
        if (user is null) return NotFound();
        if (!Enum.IsDefined(r.Role)) return BadRequest(new { error = "Role không hợp lệ." });
        if (user.Role == Role.Admin && (r.Role != Role.Admin || !r.IsActive)) return BadRequest(new { error = "Không thể hạ quyền Admin duy nhất." });
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

    private static object UserDto(UserAccount u) => new { u.Id, u.Email, u.FirstName, u.LastName, u.PhoneNumber, Role = u.Role.ToString(), u.IsEmailVerified, u.IsPhoneVerified, IsActive = !u.IsDeleted, u.CreatedDate };
}

public sealed class AdminCreateUserRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress]
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
