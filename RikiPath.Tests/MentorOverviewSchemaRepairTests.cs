using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Migrations;

namespace RikiPath.Tests;

public sealed class MentorOverviewSchemaRepairTests
{
    [CoursePostgresFact]
    public async Task Repair_is_repeatable_and_preserves_existing_rows_and_newer_columns()
    {
        var schema = "overview_qa_" + Guid.NewGuid().ToString("N");
        var connectionString = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")) { SearchPath = schema }.ConnectionString;
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        try
        {
            await using (var setup = new NpgsqlCommand($"CREATE SCHEMA {schema}; CREATE TABLE {schema}.\"MentorBookings\" (\"Id\" integer PRIMARY KEY, \"Status\" text, \"AttendedAt\" timestamptz); INSERT INTO {schema}.\"MentorBookings\" (\"Id\",\"Status\") VALUES(1,'Assigned'); CREATE TABLE {schema}.\"MentorAvailabilities\" (\"Id\" integer PRIMARY KEY); CREATE TABLE {schema}.\"UserSubscriptions\" (\"Id\" integer PRIMARY KEY); CREATE TABLE {schema}.\"Notifications\" (\"Id\" integer PRIMARY KEY, \"Message\" text); INSERT INTO {schema}.\"Notifications\" VALUES(1,'Keep existing message');", connection)) await setup.ExecuteNonQueryAsync();
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
            var statements = db.GetService<IMigrationsSqlGenerator>().Generate(new RepairMentorOverviewSchema().UpOperations, db.Model);
            for (int attempt = 0; attempt < 2; attempt++) foreach (var sql in statements)
            {
                await using var command = new NpgsqlCommand(sql.CommandText, connection); await command.ExecuteNonQueryAsync();
            }
            await using var verify = new NpgsqlCommand("SELECT b.\"Status\", b.\"AttendedAt\", b.\"ExceptionRequested\", n.\"Message\" FROM \"MentorBookings\" b CROSS JOIN \"Notifications\" n", connection);
            await using var reader = await verify.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
            Assert.Equal("Assigned", reader.GetString(0)); Assert.True(reader.IsDBNull(1)); Assert.False(reader.GetBoolean(2)); Assert.Equal("Keep existing message", reader.GetString(3));
        }
        finally { await using var cleanup = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {schema} CASCADE", connection); await cleanup.ExecuteNonQueryAsync(); }
    }
}
