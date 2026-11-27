using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FlexibleTeachingV4Refactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudyCohorts_SourceProposalId",
                table: "StudyCohorts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorTeachingProposal_Status",
                table: "MentorTeachingProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "EnrollmentCutoffMinutesBeforeFirstSession",
                table: "MentorGroupPolicies");

            migrationBuilder.RenameColumn(
                name: "PublicationHorizonDays",
                table: "MentorGroupPolicies",
                newName: "MinimumPreparationDays");

            migrationBuilder.AddColumn<DateTime>(
                name: "EnrollmentClosedAtUtc",
                table: "StudyCohorts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EnrollmentOpensAtUtc",
                table: "StudyCohorts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenEnrollmentIdempotencyKey",
                table: "MentorTeachingProposals",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedClassAtUtc",
                table: "MentorTeachingProposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MentorGroupPolicies",
                keyColumn: "Id",
                keyValue: 1,
                column: "MinimumPreparationDays",
                value: 1);

            migrationBuilder.Sql(
                "UPDATE \"MentorGroupPolicies\" SET \"MinimumPreparationDays\" = 1;");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohorts_SourceProposalId",
                table: "StudyCohorts",
                column: "SourceProposalId",
                unique: true,
                filter: "\"SourceProposalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingProposals_MentorId_OpenEnrollmentIdempotencyKey",
                table: "MentorTeachingProposals",
                columns: new[] { "MentorId", "OpenEnrollmentIdempotencyKey" },
                unique: true,
                filter: "\"OpenEnrollmentIdempotencyKey\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorTeachingProposal_Status",
                table: "MentorTeachingProposals",
                sql: "\"Status\" IN ('Draft', 'PendingReview', 'ChangesRequested', 'Approved', 'OpenedClass', 'Rejected', 'Withdrawn')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies",
                sql: "\"Version\" > 0 AND \"SessionMinutes\" > 0 AND \"RestMinutes\" >= 0 AND \"MinimumPreparationDays\" >= 1 AND \"CancellationLeadHours\" >= 0 AND \"ReschedulingLeadHours\" >= 0 AND \"HomeworkDeadlineDays\" > 0 AND \"PlacementAttemptLimit\" > 0 AND \"KpiReportingPeriodDays\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudyCohorts_SourceProposalId",
                table: "StudyCohorts");

            migrationBuilder.DropIndex(
                name: "IX_MentorTeachingProposals_MentorId_OpenEnrollmentIdempotencyKey",
                table: "MentorTeachingProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorTeachingProposal_Status",
                table: "MentorTeachingProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "EnrollmentClosedAtUtc",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "EnrollmentOpensAtUtc",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "OpenEnrollmentIdempotencyKey",
                table: "MentorTeachingProposals");

            migrationBuilder.DropColumn(
                name: "OpenedClassAtUtc",
                table: "MentorTeachingProposals");

            migrationBuilder.RenameColumn(
                name: "MinimumPreparationDays",
                table: "MentorGroupPolicies",
                newName: "PublicationHorizonDays");

            migrationBuilder.AddColumn<int>(
                name: "EnrollmentCutoffMinutesBeforeFirstSession",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "MentorGroupPolicies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EnrollmentCutoffMinutesBeforeFirstSession", "PublicationHorizonDays" },
                values: new object[] { 0, 30 });

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohorts_SourceProposalId",
                table: "StudyCohorts",
                column: "SourceProposalId",
                unique: true,
                filter: "\"SourceProposalId\" IS NOT NULL AND \"Status\" <> 'Cancelled'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorTeachingProposal_Status",
                table: "MentorTeachingProposals",
                sql: "\"Status\" IN ('Draft', 'PendingReview', 'ChangesRequested', 'Approved', 'Rejected', 'Withdrawn')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies",
                sql: "\"Version\" > 0 AND \"SessionMinutes\" > 0 AND \"RestMinutes\" >= 0 AND \"PublicationHorizonDays\" > 0 AND \"EnrollmentCutoffMinutesBeforeFirstSession\" >= 0 AND \"CancellationLeadHours\" >= 0 AND \"ReschedulingLeadHours\" >= 0 AND \"HomeworkDeadlineDays\" > 0 AND \"PlacementAttemptLimit\" > 0 AND \"KpiReportingPeriodDays\" > 0");
        }
    }
}
