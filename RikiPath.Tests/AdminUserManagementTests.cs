using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.WebApi.Controllers;

namespace RikiPath.Tests;

public sealed class AdminUserManagementTests
{
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AdminOperationsController Controller(AppDbContext db) => new(new UnitOfWork(db));
    private static UserAccount User(int id, Role role = Role.Learner, bool deleted = false) => new()
    { Id = id, Email = $"user{id}@example.test", Role = role, IsDeleted = deleted, PasswordHash = [], PasswordSalt = [] };

    [Fact]
    public void Endpoints_require_Admin_role()
        => Assert.Equal("Admin", typeof(AdminOperationsController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Roles);

    [Fact]
    public async Task List_includes_disabled_when_FE_requests_it_without_exposing_passwords()
    {
        await using var db = Context();
        db.Users.AddRange(User(1), User(2, deleted: true));
        await db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await Controller(db).GetUsers(null, includeDisabled: true));
        var json = JsonSerializer.Serialize(result.Value);
        Assert.Equal(2, JsonDocument.Parse(json).RootElement.GetArrayLength());
        Assert.DoesNotContain("Password", json);
        var active = Assert.IsType<OkObjectResult>(await Controller(db).GetUsers(null));
        Assert.Equal(1, JsonDocument.Parse(JsonSerializer.Serialize(active.Value)).RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Create_hashes_password_and_rejects_duplicate_email()
    {
        await using var db = Context();
        var controller = Controller(db);
        var request = new AdminCreateUserRequest { Email = "Learner@Example.test", Password = "Isolated-test-123", Role = Role.Learner };
        var result = Assert.IsType<CreatedAtActionResult>(await controller.CreateUser(request, default));
        var user = await db.Users.SingleAsync();
        using var hmac = new HMACSHA512(user.PasswordSalt);
        Assert.Equal(hmac.ComputeHash(Encoding.UTF8.GetBytes(request.Password)), user.PasswordHash);
        Assert.Equal(nameof(AdminOperationsController.GetUser), result.ActionName);
        Assert.IsType<ConflictObjectResult>(await controller.CreateUser(request, default));
    }

    [Fact]
    public async Task Admin_cannot_be_created_disabled_or_demoted()
    {
        await using var db = Context();
        db.Users.Add(User(1, Role.Admin));
        await db.SaveChangesAsync();
        var controller = Controller(db);
        Assert.IsType<BadRequestObjectResult>(await controller.CreateUser(new AdminCreateUserRequest
        { Email = "admin@example.test", Password = "Isolated-test-123", Role = Role.Admin }, default));
        Assert.IsType<BadRequestObjectResult>(await controller.DeactivateUser(1, default));
        Assert.IsType<BadRequestObjectResult>(await controller.UpdateUser(1, new AdminUpdateUserRequest
        { Role = Role.Admin, IsActive = false }, default));
        Assert.IsType<BadRequestObjectResult>(await controller.UpdateUser(1, new AdminUpdateUserRequest
        { Role = Role.Learner, IsActive = true }, default));
        Assert.False((await db.Users.SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Disabled_account_can_be_reactivated_and_updated()
    {
        await using var db = Context();
        db.Users.Add(User(2));
        await db.SaveChangesAsync();
        var controller = Controller(db);
        Assert.IsType<NoContentResult>(await controller.DeactivateUser(2, default));
        Assert.True((await db.Users.SingleAsync()).IsDeleted);
        Assert.IsType<OkObjectResult>(await controller.UpdateUser(2, new AdminUpdateUserRequest
        { FirstName = "Updated", Role = Role.Mentor, IsActive = true }, default));
        var user = await db.Users.SingleAsync();
        Assert.False(user.IsDeleted);
        Assert.Equal(Role.Mentor, user.Role);
        Assert.Equal("Updated", user.FirstName);
    }
}
