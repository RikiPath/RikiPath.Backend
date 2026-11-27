using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanFlexibleTeachingModelV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MentorGroupPolicyLevelLimits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorTeachingProposal_Duration",
                table: "MentorTeachingProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_AllowedSessionsPerWeek",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_CourseDurationWeeks",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_DefaultSessionsPerWeek",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_LeadDays",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_MaxTotalActiveClasses",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_MinLearners",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_WeeklySessionCap",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "AllowedSessionsPerWeekCsv",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "CourseDurationWeeks",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "DailySessionCap",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "DefaultSessionsPerWeek",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "MaxTotalActiveClassesPerMentor",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "MinLearnersToStart",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "MinimumProposalLeadDays",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "RecurringWeekdayPatternsJson",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "WeeklySessionCap",
                table: "MentorGroupPolicies");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "StudyCohorts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "StudyCohorts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CourseDurationWeeks",
                table: "StudyCohorts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "EnrollmentClosesAtUtc",
                table: "StudyCohorts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudyCohort_DurationWeeks",
                table: "StudyCohorts",
                sql: "\"CourseDurationWeeks\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorTeachingProposal_Duration",
                table: "MentorTeachingProposals",
                sql: "\"CourseDurationWeeks\" BETWEEN 1 AND 104 AND \"SessionDurationMinutes\" BETWEEN 30 AND 240");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_EnrollmentDays",
                table: "MentorGroupPolicies",
                sql: "\"MinimumPublishedEnrollmentDays\" BETWEEN 1 AND 180");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies",
                sql: "\"Version\" > 0 AND \"SessionMinutes\" > 0 AND \"RestMinutes\" >= 0 AND \"PublicationHorizonDays\" > 0 AND \"EnrollmentCutoffMinutesBeforeFirstSession\" >= 0 AND \"CancellationLeadHours\" >= 0 AND \"ReschedulingLeadHours\" >= 0 AND \"HomeworkDeadlineDays\" > 0 AND \"PlacementAttemptLimit\" > 0 AND \"KpiReportingPeriodDays\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StudyCohort_DurationWeeks",
                table: "StudyCohorts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorTeachingProposal_Duration",
                table: "MentorTeachingProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_EnrollmentDays",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "CourseDurationWeeks",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "EnrollmentClosesAtUtc",
                table: "StudyCohorts");

            migrationBuilder.AddColumn<string>(
                name: "AllowedSessionsPerWeekCsv",
                table: "MentorGroupPolicies",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CourseDurationWeeks",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DailySessionCap",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultSessionsPerWeek",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxTotalActiveClassesPerMentor",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinLearnersToStart",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumProposalLeadDays",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RecurringWeekdayPatternsJson",
                table: "MentorGroupPolicies",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WeeklySessionCap",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MentorGroupPolicyLevelLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    MaxActiveClasses = table.Column<int>(type: "integer", nullable: false),
                    MentorGroupPolicyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorGroupPolicyLevelLimits", x => x.Id);
                    table.CheckConstraint("CK_MentorGroupPolicyLevelLimit_MaxActive", "\"MaxActiveClasses\" BETWEEN 0 AND 50");
                    table.ForeignKey(
                        name: "FK_MentorGroupPolicyLevelLimits_CertificateLevels_CertificateL~",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorGroupPolicyLevelLimits_MentorGroupPolicies_MentorGrou~",
                        column: x => x.MentorGroupPolicyId,
                        principalTable: "MentorGroupPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "MentorGroupPolicies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AllowedSessionsPerWeekCsv", "CourseDurationWeeks", "DailySessionCap", "DefaultSessionsPerWeek", "MaxTotalActiveClassesPerMentor", "MinLearnersToStart", "MinimumProposalLeadDays", "RecurringWeekdayPatternsJson", "WeeklySessionCap" },
                values: new object[] { "2,3,4", 10, 4, 3, 4, 1, 14, "[[1,3,5],[2,4,6]]", 0 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorTeachingProposal_Duration",
                table: "MentorTeachingProposals",
                sql: "\"CourseDurationWeeks\" BETWEEN 1 AND 52 AND \"SessionDurationMinutes\" BETWEEN 30 AND 240");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_AllowedSessionsPerWeek",
                table: "MentorGroupPolicies",
                sql: "\"AllowedSessionsPerWeekCsv\" ~ '^[1-9][0-9]*(,[1-9][0-9]*)*$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_CourseDurationWeeks",
                table: "MentorGroupPolicies",
                sql: "\"CourseDurationWeeks\" BETWEEN 1 AND 52");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_DefaultSessionsPerWeek",
                table: "MentorGroupPolicies",
                sql: "\"DefaultSessionsPerWeek\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_LeadDays",
                table: "MentorGroupPolicies",
                sql: "\"MinimumProposalLeadDays\" BETWEEN 1 AND 180 AND \"MinimumPublishedEnrollmentDays\" BETWEEN 1 AND 180 AND \"MinimumPublishedEnrollmentDays\" <= \"MinimumProposalLeadDays\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_MaxTotalActiveClasses",
                table: "MentorGroupPolicies",
                sql: "\"MaxTotalActiveClassesPerMentor\" BETWEEN 1 AND 50");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_MinLearners",
                table: "MentorGroupPolicies",
                sql: "\"MinLearnersToStart\" BETWEEN 1 AND \"MaxLearners\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies",
                sql: "\"Version\" > 0 AND \"SessionMinutes\" > 0 AND \"RestMinutes\" >= 0 AND \"DailySessionCap\" > 0 AND \"PublicationHorizonDays\" > 0 AND \"EnrollmentCutoffMinutesBeforeFirstSession\" >= 0 AND \"CancellationLeadHours\" >= 0 AND \"ReschedulingLeadHours\" >= 0 AND \"HomeworkDeadlineDays\" > 0 AND \"PlacementAttemptLimit\" > 0 AND \"KpiReportingPeriodDays\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_WeeklySessionCap",
                table: "MentorGroupPolicies",
                sql: "\"WeeklySessionCap\" BETWEEN 0 AND 84");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicyLevelLimits_CertificateLevelId",
                table: "MentorGroupPolicyLevelLimits",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicyLevelLimits_MentorGroupPolicyId_Certificat~",
                table: "MentorGroupPolicyLevelLimits",
                columns: new[] { "MentorGroupPolicyId", "CertificateLevelId" },
                unique: true);
        }
    }
}
