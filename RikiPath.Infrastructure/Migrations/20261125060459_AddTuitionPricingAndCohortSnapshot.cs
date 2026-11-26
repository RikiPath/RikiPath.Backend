using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTuitionPricingAndCohortSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalTuitionSnapshot",
                table: "StudyCohorts",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TuitionPerSessionSnapshot",
                table: "StudyCohorts",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TuitionPolicyVersionSnapshot",
                table: "StudyCohorts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TuitionSnapshotAtUtc",
                table: "StudyCohorts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumProposalLeadDays",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPublishedEnrollmentDays",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<decimal>(
                name: "TuitionPerSessionN2",
                table: "MentorGroupPolicies",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 110000m);

            migrationBuilder.AddColumn<decimal>(
                name: "TuitionPerSessionN3",
                table: "MentorGroupPolicies",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 90000m);

            migrationBuilder.AddColumn<decimal>(
                name: "TuitionPerSessionN4",
                table: "MentorGroupPolicies",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 75000m);

            migrationBuilder.AddColumn<decimal>(
                name: "TuitionPerSessionN5",
                table: "MentorGroupPolicies",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 60000m);

            migrationBuilder.UpdateData(
                table: "MentorGroupPolicies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "MinimumProposalLeadDays", "MinimumPublishedEnrollmentDays", "TuitionPerSessionN2", "TuitionPerSessionN3", "TuitionPerSessionN4", "TuitionPerSessionN5" },
                values: new object[] { 14, 7, 110000m, 90000m, 75000m, 60000m });

            // Backfill immutable tuition snapshot on existing cohorts from their linked StudyCoursePlan without mutating prices.
            migrationBuilder.Sql("""
                UPDATE "StudyCohorts" c
                SET "TotalTuitionSnapshot" = p."Price",
                    "TuitionPerSessionSnapshot" = CASE
                        WHEN COALESCE(p."TotalSessions", 0) > 0 THEN TRUNC(p."Price" / p."TotalSessions")
                        ELSE NULL
                    END,
                    "TuitionPolicyVersionSnapshot" = c."PolicyVersionSnapshot",
                    "TuitionSnapshotAtUtc" = COALESCE(c."CreatedAtUtc", NOW())
                FROM "StudyCoursePlans" p
                WHERE c."PlanId" = p."Id"
                  AND c."TotalTuitionSnapshot" IS NULL
                  AND p."Price" > 0;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_LeadDays",
                table: "MentorGroupPolicies",
                sql: "\"MinimumProposalLeadDays\" BETWEEN 1 AND 180 AND \"MinimumPublishedEnrollmentDays\" BETWEEN 1 AND 90 AND \"MinimumPublishedEnrollmentDays\" <= \"MinimumProposalLeadDays\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_Tuition",
                table: "MentorGroupPolicies",
                sql: "\"TuitionPerSessionN5\" >= 0 AND \"TuitionPerSessionN4\" >= 0 AND \"TuitionPerSessionN3\" >= 0 AND \"TuitionPerSessionN2\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_LeadDays",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_Tuition",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "TotalTuitionSnapshot",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "TuitionPerSessionSnapshot",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "TuitionPolicyVersionSnapshot",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "TuitionSnapshotAtUtc",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "MinimumProposalLeadDays",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "MinimumPublishedEnrollmentDays",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "TuitionPerSessionN2",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "TuitionPerSessionN3",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "TuitionPerSessionN4",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "TuitionPerSessionN5",
                table: "MentorGroupPolicies");
        }
    }
}
