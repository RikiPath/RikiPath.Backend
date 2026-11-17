using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupEnrollmentAndRemediationPolicyRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies");

            migrationBuilder.AddColumn<int>(
                name: "EnrollmentCutoffMinutesBeforeFirstSession",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RemediationThresholdComparison",
                table: "MentorGroupPolicies",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: ">");

            migrationBuilder.AddColumn<bool>(
                name: "RequireAdminRemediationReview",
                table: "MentorGroupPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireOfficialExamEvidence",
                table: "MentorGroupPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "MentorGroupPolicies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EnrollmentCutoffMinutesBeforeFirstSession", "RemediationThresholdComparison", "RequireAdminRemediationReview", "RequireOfficialExamEvidence" },
                values: new object[] { 0, ">", true, true });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_Comparison",
                table: "MentorGroupPolicies",
                sql: "\"RemediationThresholdComparison\" IN ('>', '>=')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_Evidence",
                table: "MentorGroupPolicies",
                sql: "\"RequireOfficialExamEvidence\" AND \"RequireAdminRemediationReview\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies",
                sql: "\"Version\" > 0 AND \"SessionMinutes\" > 0 AND \"RestMinutes\" >= 0 AND \"DailySessionCap\" > 0 AND \"PublicationHorizonDays\" > 0 AND \"EnrollmentCutoffMinutesBeforeFirstSession\" >= 0 AND \"CancellationLeadHours\" >= 0 AND \"ReschedulingLeadHours\" >= 0 AND \"HomeworkDeadlineDays\" > 0 AND \"PlacementAttemptLimit\" > 0 AND \"KpiReportingPeriodDays\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_Comparison",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_Evidence",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "EnrollmentCutoffMinutesBeforeFirstSession",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "RemediationThresholdComparison",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "RequireAdminRemediationReview",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "RequireOfficialExamEvidence",
                table: "MentorGroupPolicies");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_Positive",
                table: "MentorGroupPolicies",
                sql: "\"Version\" > 0 AND \"SessionMinutes\" > 0 AND \"RestMinutes\" >= 0 AND \"DailySessionCap\" > 0 AND \"PublicationHorizonDays\" > 0 AND \"CancellationLeadHours\" >= 0 AND \"ReschedulingLeadHours\" >= 0 AND \"HomeworkDeadlineDays\" > 0 AND \"PlacementAttemptLimit\" > 0 AND \"KpiReportingPeriodDays\" > 0");
        }
    }
}
