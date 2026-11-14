using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RikiPath.Domain.Entities;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Migrations;
using RikiPath.Infrastructure.Repositories;

namespace RikiPath.Tests;

public sealed class CoursePostgresFactAttribute : FactAttribute
{
    public CoursePostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")))
            Skip = "Set RIKIPATH_COURSE_TEST_DB to an isolated PostgreSQL test database.";
    }
}

public sealed class StudyCoursePostgresTests
{
    [CoursePostgresFact]
    public async Task Migration_row_locks_and_unique_retake_constraints_work_on_Postgres()
    {
        var schema = "course_qa_" + Guid.NewGuid().ToString("N");
        var connectionString = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")) { SearchPath = schema }.ConnectionString;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var setup = new NpgsqlCommand($"CREATE SCHEMA {schema}; CREATE TABLE {schema}.\"Users\" (\"Id\" integer PRIMARY KEY); CREATE TABLE {schema}.\"CertificateLevels\" (\"Id\" integer PRIMARY KEY); INSERT INTO {schema}.\"Users\" VALUES (1),(2); INSERT INTO {schema}.\"CertificateLevels\" VALUES (1);", connection))
            await setup.ExecuteNonQueryAsync();
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
            var migration = new AddStudyCoursesAndRetakePolicy();
            var statements = db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.Model);
            foreach (var sql in statements)
            {
                Assert.DoesNotContain("DROP COLUMN", sql.CommandText, StringComparison.OrdinalIgnoreCase);
                await using var command = new NpgsqlCommand(sql.CommandText, connection);
                await command.ExecuteNonQueryAsync();
            }
            var plan = new StudyCoursePlan { Name = "QA", CertificateLevelId = 1, DurationMonths = 3, SessionsPerWeek = 3, SessionMinutes = 60, TotalSessions = 10, Price = 100 };
            db.Add(plan); await db.SaveChangesAsync();
            StudyCourse Course(int? parent = null) => new() { PlanId = plan.Id, LearnerId = 1, MentorId = 2, PurchaseKey = Guid.NewGuid(), CertificateLevelId = 1, Name = "QA", RetakeOfId = parent, DurationMonths = 3, SessionsPerWeek = 3, SessionMinutes = 60, TotalSessions = 10 };
            var original = Course(); db.Add(original); await db.SaveChangesAsync();
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                var repo = new StudyCourseRepository(db);
                Assert.Equal(original.Id, (await repo.LockAsync(original.Id, default))!.Id);
                await repo.LockPurchaseAsync(1, original.PurchaseKey, default);
                await repo.LockScheduleAsync(2, 1, default);
                await transaction.CommitAsync();
            }
            var retake = Course(original.Id); db.Add(retake); await db.SaveChangesAsync();
            db.Add(Course(original.Id));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
            db.ChangeTracker.Clear();
            db.Add(new CourseRetakeRequest { CourseId = original.Id, EvidenceUrl = "private://qa", Status = "Pending", ExamDate = DateTime.UtcNow, ResultPublishedAt = DateTime.UtcNow }); await db.SaveChangesAsync();
            db.Add(new CourseRetakeRequest { CourseId = original.Id, EvidenceUrl = "private://qa2", Status = "Pending", ExamDate = DateTime.UtcNow, ResultPublishedAt = DateTime.UtcNow });
            duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", connection);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
