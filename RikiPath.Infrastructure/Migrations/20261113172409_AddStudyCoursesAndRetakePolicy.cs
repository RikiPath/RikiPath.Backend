using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyCoursesAndRetakePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudyCoursePlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false),
                    SessionsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    SessionMinutes = table.Column<int>(type: "integer", nullable: true),
                    TotalSessions = table.Column<int>(type: "integer", nullable: true),
                    RetakeClaimDays = table.Column<int>(type: "integer", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
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
                name: "StudyCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    PurchaseKey = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: true),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    RetakeOfId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    PlanRevision = table.Column<int>(type: "integer", nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false),
                    SessionsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    SessionMinutes = table.Column<int>(type: "integer", nullable: false),
                    TotalSessions = table.Column<int>(type: "integer", nullable: false),
                    RetakeClaimDays = table.Column<int>(type: "integer", nullable: true),
                    MinimumHomeworkPercent = table.Column<int>(type: "integer", nullable: false),
                    MaximumAbsencePercent = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    PaymentStatus = table.Column<string>(type: "text", nullable: false),
                    PaymentReference = table.Column<string>(type: "text", nullable: true),
                    CheckoutUrl = table.Column<string>(type: "text", nullable: true),
                    PaymentExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
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
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    At = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    ExamDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ResultPublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EvidenceUrl = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Response = table.Column<string>(type: "text", nullable: true),
                    EligibilitySnapshot = table.Column<string>(type: "text", nullable: true)
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
                name: "StudyCourseHomework",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Submission = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Feedback = table.Column<string>(type: "text", nullable: true)
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
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MeetingUrl = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConfirmedById = table.Column<int>(type: "integer", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCourseSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCourseSessions_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            // All access goes through Backend role/ownership checks; block direct Supabase client reads.
            foreach (var table in new[] { "StudyCoursePlans", "StudyCourses", "StudyCourseSessions", "StudyCourseHomework", "CourseRetakeRequests", "CourseAuditEntries" })
                migrationBuilder.Sql($"ALTER TABLE \"{table}\" ENABLE ROW LEVEL SECURITY;");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourseSessions_CourseId_Number_Attempt",
                table: "StudyCourseSessions",
                columns: new[] { "CourseId", "Number", "Attempt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseAuditEntries");

            migrationBuilder.DropTable(
                name: "CourseRetakeRequests");

            migrationBuilder.DropTable(
                name: "StudyCourseHomework");

            migrationBuilder.DropTable(
                name: "StudyCourseSessions");

            migrationBuilder.DropTable(
                name: "StudyCourses");

            migrationBuilder.DropTable(
                name: "StudyCoursePlans");

        }
    }
}
