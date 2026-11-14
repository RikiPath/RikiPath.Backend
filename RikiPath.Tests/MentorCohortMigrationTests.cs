using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Migrations;

namespace RikiPath.Tests;

public sealed class MentorCohortMigrationTests
{
    [Fact] public void Migration_does_not_drop_existing_columns_or_rewrite_seed_data()
    {
        var operations = new MentorCohortsAndExamDrafts().UpOperations;
        Assert.DoesNotContain(operations, x => x is DropColumnOperation or DropTableOperation or UpdateDataOperation or DeleteDataOperation);
        Assert.Contains(operations.OfType<SqlOperation>(), x => x.Sql.Contains("\"MentorCalendarConnections\" ENABLE ROW LEVEL SECURITY"));
    }

    [CoursePostgresFact]
    public async Task Migration_applies_without_changing_existing_plans_or_mock_test_data()
    {
        var schema = "cohort_migration_" + Guid.NewGuid().ToString("N");
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")) { SearchPath = schema }.ConnectionString;
        await using var connection = new NpgsqlConnection(cs); await connection.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE SCHEMA {schema}", connection)) await command.ExecuteNonQueryAsync();
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE "Users" ("Id" integer PRIMARY KEY);
                CREATE TABLE "CertificateLevels" ("Id" integer PRIMARY KEY);
                CREATE TABLE "MentorAvailabilities" ("Id" integer PRIMARY KEY);
                CREATE TABLE "MentorAttendanceRecords" ("Id" integer PRIMARY KEY, "MentorBookingId" integer NOT NULL, "RoomId" uuid NOT NULL, "UserId" integer NOT NULL);
                CREATE TABLE "MockTests" ("Id" integer PRIMARY KEY, "IsPlacementAssessment" boolean NOT NULL);
                INSERT INTO "MockTests" VALUES (1, true);
                INSERT INTO "CertificateLevels" VALUES (1);
                """);
            foreach (var migration in new Migration[] { new AddStudyCoursesAndRetakePolicy(), new MentorCohortsAndExamDrafts() })
                foreach (var sql in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.Model))
                    await db.Database.ExecuteSqlRawAsync(sql.CommandText);
            await using var verify = new NpgsqlCommand("SELECT \"IsPlacementAssessment\" FROM \"MockTests\" WHERE \"Id\"=1", connection);
            Assert.True((bool)(await verify.ExecuteScalarAsync())!);
            await using var rls = new NpgsqlCommand("SELECT relrowsecurity FROM pg_class WHERE oid='\"MentorCalendarConnections\"'::regclass", connection);
            Assert.True((bool)(await rls.ExecuteScalarAsync())!);
        }
        finally
        {
            await using var command = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", connection); await command.ExecuteNonQueryAsync();
        }
    }
}
