using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MentorCohortsAndExamDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<int>(
                name: "AvailabilityId",
                table: "StudyCourseSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CohortId",
                table: "StudyCourses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionType",
                table: "StudyCourses",
                type: "text",
                nullable: false,
                defaultValue: "Individual");

            migrationBuilder.AddColumn<DateTime>(
                name: "TargetExamDate",
                table: "StudyCourses",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "StudyCoursePlans",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "SessionType",
                table: "StudyCoursePlans",
                type: "text",
                nullable: false,
                defaultValue: "Individual");

            migrationBuilder.AddColumn<DateTime>(
                name: "TargetExamDate",
                table: "StudyCoursePlans",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MentorBookingId",
                table: "MentorAttendanceRecords",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "CourseSessionId",
                table: "MentorAttendanceRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseScheduleRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    RequestedById = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Response = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseScheduleRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseScheduleRequests_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JlptExamSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExamDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JlptExamSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MentorCalendarConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    ProtectedRefreshToken = table.Column<string>(type: "text", nullable: true),
                    StateHash = table.Column<string>(type: "text", nullable: true),
                    StateExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SyncedEventsJson = table.Column<string>(type: "text", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorCalendarConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorCalendarConnections_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyCohorts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCohorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCohorts_StudyCoursePlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "StudyCoursePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohorts_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyCohortSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CohortId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    AvailabilityId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCohortSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCohortSessions_MentorAvailabilities_AvailabilityId",
                        column: x => x.AvailabilityId,
                        principalTable: "MentorAvailabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohortSessions_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });








            migrationBuilder.CreateIndex(
                name: "IX_StudyCourseSessions_AvailabilityId",
                table: "StudyCourseSessions",
                column: "AvailabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_CohortId",
                table: "StudyCourses",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_CourseSessionId",
                table: "MentorAttendanceRecords",
                column: "CourseSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_RoomId_UserId",
                table: "MentorAttendanceRecords",
                columns: new[] { "RoomId", "UserId" },
                unique: true,
                filter: "\"MentorBookingId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CourseScheduleRequests_CourseId_SessionId",
                table: "CourseScheduleRequests",
                columns: new[] { "CourseId", "SessionId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_JlptExamSchedules_SourceKey",
                table: "JlptExamSchedules",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorCalendarConnections_MentorId",
                table: "MentorCalendarConnections",
                column: "MentorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorCalendarConnections_StateHash",
                table: "MentorCalendarConnections",
                column: "StateHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohorts_MentorId",
                table: "StudyCohorts",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohorts_PlanId",
                table: "StudyCohorts",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortSessions_AvailabilityId",
                table: "StudyCohortSessions",
                column: "AvailabilityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortSessions_CohortId_Number",
                table: "StudyCohortSessions",
                columns: new[] { "CohortId", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MentorAttendanceRecords_StudyCourseSessions_CourseSessionId",
                table: "MentorAttendanceRecords",
                column: "CourseSessionId",
                principalTable: "StudyCourseSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudyCourses_StudyCohorts_CohortId",
                table: "StudyCourses",
                column: "CohortId",
                principalTable: "StudyCohorts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudyCourseSessions_MentorAvailabilities_AvailabilityId",
                table: "StudyCourseSessions",
                column: "AvailabilityId",
                principalTable: "MentorAvailabilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // New tables contain private enrollment, requests and encrypted OAuth tokens.
            migrationBuilder.Sql("""
                ALTER TABLE "StudyCohorts" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "StudyCohortSessions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "CourseScheduleRequests" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "JlptExamSchedules" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "MentorCalendarConnections" ENABLE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new InvalidOperationException("Rollback requires reviewing active classes, enrollments and attendance records; no automatic data deletion.");
    }
}
