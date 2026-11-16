using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetireLegacyStudyCourseModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorAttendanceRecords_StudyCourseSessions_CourseSessionId",
                table: "MentorAttendanceRecords");

            migrationBuilder.DropTable(
                name: "CourseAuditEntries");

            migrationBuilder.DropTable(
                name: "CourseRetakeRequests");

            migrationBuilder.DropTable(
                name: "CourseScheduleRequests");

            migrationBuilder.DropTable(
                name: "JlptExamSchedules");

            migrationBuilder.DropTable(
                name: "StudyCohortSessions");

            migrationBuilder.DropTable(
                name: "StudyCourseHomework");

            migrationBuilder.DropTable(
                name: "StudyCourseSessions");

            migrationBuilder.DropTable(
                name: "StudyCourses");

            migrationBuilder.DropTable(
                name: "StudyCohorts");

            migrationBuilder.DropTable(
                name: "StudyCoursePlans");

            migrationBuilder.DropIndex(
                name: "IX_MentorAttendanceRecords_CourseSessionId",
                table: "MentorAttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_MentorAttendanceRecords_RoomId_UserId",
                table: "MentorAttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CourseSessionId",
                table: "MentorAttendanceRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourseSessionId",
                table: "MentorAttendanceRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JlptExamSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    SourceKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JlptExamSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudyCoursePlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,0)", precision: 18, nullable: false),
                    RetakeClaimDays = table.Column<int>(type: "integer", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    SessionMinutes = table.Column<int>(type: "integer", nullable: true),
                    SessionType = table.Column<string>(type: "text", nullable: false, defaultValue: "Individual"),
                    SessionsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    TargetExamDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TotalSessions = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCoursePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCoursePlans_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyCohorts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
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
                    AvailabilityId = table.Column<int>(type: "integer", nullable: false),
                    CohortId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "StudyCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    CohortId = table.Column<int>(type: "integer", nullable: true),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: true),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    RetakeOfId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, nullable: false),
                    CheckoutUrl = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MaximumAbsencePercent = table.Column<int>(type: "integer", nullable: false),
                    MinimumHomeworkPercent = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PaymentExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaymentReference = table.Column<string>(type: "text", nullable: true),
                    PaymentStatus = table.Column<string>(type: "text", nullable: false),
                    PlanRevision = table.Column<int>(type: "integer", nullable: false),
                    PurchaseKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RetakeClaimDays = table.Column<int>(type: "integer", nullable: true),
                    SessionMinutes = table.Column<int>(type: "integer", nullable: false),
                    SessionType = table.Column<string>(type: "text", nullable: false, defaultValue: "Individual"),
                    SessionsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TargetExamDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TotalSessions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCourses_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_StudyCoursePlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "StudyCoursePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_StudyCourses_RetakeOfId",
                        column: x => x.RetakeOfId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseAuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseAuditEntries_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseRetakeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    EligibilitySnapshot = table.Column<string>(type: "text", nullable: true),
                    EvidenceUrl = table.Column<string>(type: "text", nullable: false),
                    ExamDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Response = table.Column<string>(type: "text", nullable: true),
                    ResultPublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseRetakeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseRetakeRequests_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseScheduleRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RequestedById = table.Column<int>(type: "integer", nullable: false),
                    Response = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
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
                name: "StudyCourseHomework",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Feedback = table.Column<string>(type: "text", nullable: true),
                    Instructions = table.Column<string>(type: "text", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: true),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Submission = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCourseHomework", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCourseHomework_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyCourseSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AvailabilityId = table.Column<int>(type: "integer", nullable: true),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfirmedById = table.Column<int>(type: "integer", nullable: true),
                    MeetingUrl = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCourseSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCourseSessions_MentorAvailabilities_AvailabilityId",
                        column: x => x.AvailabilityId,
                        principalTable: "MentorAvailabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourseSessions_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_CourseAuditEntries_CourseId",
                table: "CourseAuditEntries",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseRetakeRequests_CourseId",
                table: "CourseRetakeRequests",
                column: "CourseId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

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

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourseHomework_CourseId",
                table: "StudyCourseHomework",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCoursePlans_CertificateLevelId",
                table: "StudyCoursePlans",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_CertificateLevelId",
                table: "StudyCourses",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_CohortId",
                table: "StudyCourses",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_LearnerId_PurchaseKey",
                table: "StudyCourses",
                columns: new[] { "LearnerId", "PurchaseKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_MentorId",
                table: "StudyCourses",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_PlanId",
                table: "StudyCourses",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_RetakeOfId",
                table: "StudyCourses",
                column: "RetakeOfId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourseSessions_AvailabilityId",
                table: "StudyCourseSessions",
                column: "AvailabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourseSessions_CourseId_Number_Attempt",
                table: "StudyCourseSessions",
                columns: new[] { "CourseId", "Number", "Attempt" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MentorAttendanceRecords_StudyCourseSessions_CourseSessionId",
                table: "MentorAttendanceRecords",
                column: "CourseSessionId",
                principalTable: "StudyCourseSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
