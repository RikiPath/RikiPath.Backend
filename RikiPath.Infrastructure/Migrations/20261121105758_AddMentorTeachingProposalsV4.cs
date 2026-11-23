using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorTeachingProposalsV4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities");

            migrationBuilder.AddColumn<int>(
                name: "SourceProposalId",
                table: "StudyCohorts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeachingProposalId",
                table: "MentorAvailabilities",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MentorTeachingProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: true),
                    ProgramType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CoursePlanId = table.Column<int>(type: "integer", nullable: true),
                    CourseTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CourseObjective = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    ExpectedStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CourseDurationWeeks = table.Column<int>(type: "integer", nullable: false),
                    SessionDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    WeeklySessionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    OptionalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OptionalAttachmentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    SubmissionIdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    ReviewReason = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    GroupPolicyId = table.Column<int>(type: "integer", nullable: true),
                    PolicyVersionSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSavedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorTeachingProposals", x => x.Id);
                    table.CheckConstraint("CK_MentorTeachingProposal_Duration", "\"CourseDurationWeeks\" BETWEEN 1 AND 52 AND \"SessionDurationMinutes\" BETWEEN 30 AND 240");
                    table.CheckConstraint("CK_MentorTeachingProposal_ProgramType", "\"ProgramType\" IN ('StandardJlpt', 'IntensiveJlpt', 'ExamPrepJlpt', 'CustomJlpt')");
                    table.CheckConstraint("CK_MentorTeachingProposal_Revision", "\"Revision\" > 0");
                    table.CheckConstraint("CK_MentorTeachingProposal_Status", "\"Status\" IN ('Draft', 'PendingReview', 'ChangesRequested', 'Approved', 'Rejected', 'Withdrawn')");
                    table.ForeignKey(
                        name: "FK_MentorTeachingProposals_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorTeachingProposals_MentorGroupPolicies_GroupPolicyId",
                        column: x => x.GroupPolicyId,
                        principalTable: "MentorGroupPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorTeachingProposals_StudyCoursePlans_CoursePlanId",
                        column: x => x.CoursePlanId,
                        principalTable: "StudyCoursePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorTeachingProposals_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorTeachingProposals_Users_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorTeachingProposalEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RevisionAfter = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorTeachingProposalEvents", x => x.Id);
                    table.CheckConstraint("CK_MentorTeachingProposalEvent_RevisionAfter", "\"RevisionAfter\" > 0");
                    table.ForeignKey(
                        name: "FK_MentorTeachingProposalEvents_MentorTeachingProposals_Propos~",
                        column: x => x.ProposalId,
                        principalTable: "MentorTeachingProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990001,
                column: "TeachingProposalId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990002,
                column: "TeachingProposalId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990003,
                column: "TeachingProposalId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990004,
                column: "TeachingProposalId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohorts_SourceProposalId",
                table: "StudyCohorts",
                column: "SourceProposalId",
                filter: "\"SourceProposalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_TeachingProposalId",
                table: "MentorAvailabilities",
                column: "TeachingProposalId",
                filter: "\"TeachingProposalId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities",
                sql: "(\"GenerationKey\" IS NULL AND \"PolicyVersion\" IS NULL AND \"MentorScheduleRuleId\" IS NULL AND \"LocalBusinessDate\" IS NULL AND \"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL) OR (\"GenerationKey\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"PolicyVersion\" > 0 AND (\"MentorScheduleRuleId\" IS NOT NULL OR \"TeachingProposalId\" IS NOT NULL) AND \"LocalBusinessDate\" IS NOT NULL AND ((\"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL AND \"MaxLearners\" = 1 AND \"EndTime\" = \"StartTime\" + INTERVAL '90 minutes') OR (\"GroupPolicyId\" IS NOT NULL AND \"SessionMinutesSnapshot\" > 0 AND \"RestMinutesSnapshot\" >= 0 AND \"MaxLearners\" BETWEEN 1 AND 4 AND \"EndTime\" = \"StartTime\" + \"SessionMinutesSnapshot\" * INTERVAL '1 minute')))");

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingProposalEvents_ProposalId_CreatedAtUtc",
                table: "MentorTeachingProposalEvents",
                columns: new[] { "ProposalId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingProposals_CertificateLevelId",
                table: "MentorTeachingProposals",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingProposals_CoursePlanId",
                table: "MentorTeachingProposals",
                column: "CoursePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingProposals_GroupPolicyId",
                table: "MentorTeachingProposals",
                column: "GroupPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingProposals_MentorId_Status_UpdatedAtUtc",
                table: "MentorTeachingProposals",
                columns: new[] { "MentorId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingProposals_MentorId_SubmissionIdempotencyKey",
                table: "MentorTeachingProposals",
                columns: new[] { "MentorId", "SubmissionIdempotencyKey" },
                unique: true,
                filter: "\"SubmissionIdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingProposals_ReviewedByAdminId",
                table: "MentorTeachingProposals",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingProposals_Status_SubmittedAtUtc",
                table: "MentorTeachingProposals",
                columns: new[] { "Status", "SubmittedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_MentorAvailabilities_MentorTeachingProposals_TeachingPropos~",
                table: "MentorAvailabilities",
                column: "TeachingProposalId",
                principalTable: "MentorTeachingProposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudyCohorts_MentorTeachingProposals_SourceProposalId",
                table: "StudyCohorts",
                column: "SourceProposalId",
                principalTable: "MentorTeachingProposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorAvailabilities_MentorTeachingProposals_TeachingPropos~",
                table: "MentorAvailabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_StudyCohorts_MentorTeachingProposals_SourceProposalId",
                table: "StudyCohorts");

            migrationBuilder.DropTable(
                name: "MentorTeachingProposalEvents");

            migrationBuilder.DropTable(
                name: "MentorTeachingProposals");

            migrationBuilder.DropIndex(
                name: "IX_StudyCohorts_SourceProposalId",
                table: "StudyCohorts");

            migrationBuilder.DropIndex(
                name: "IX_MentorAvailabilities_TeachingProposalId",
                table: "MentorAvailabilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "SourceProposalId",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "TeachingProposalId",
                table: "MentorAvailabilities");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities",
                sql: "(\"GenerationKey\" IS NULL AND \"PolicyVersion\" IS NULL AND \"MentorScheduleRuleId\" IS NULL AND \"LocalBusinessDate\" IS NULL AND \"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL) OR (\"GenerationKey\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"PolicyVersion\" > 0 AND \"MentorScheduleRuleId\" IS NOT NULL AND \"LocalBusinessDate\" IS NOT NULL AND ((\"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL AND \"MaxLearners\" = 1 AND \"EndTime\" = \"StartTime\" + INTERVAL '90 minutes') OR (\"GroupPolicyId\" IS NOT NULL AND \"SessionMinutesSnapshot\" > 0 AND \"RestMinutesSnapshot\" >= 0 AND \"MaxLearners\" BETWEEN 1 AND 4 AND \"EndTime\" = \"StartTime\" + \"SessionMinutesSnapshot\" * INTERVAL '1 minute')))");
        }
    }
}
