using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExplicitActivePolicyAndDynamicCapacityLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicyLevelLimit_MaxActive",
                table: "MentorGroupPolicyLevelLimits");

            migrationBuilder.DropIndex(
                name: "IX_MentorGroupPolicies_EffectiveFromUtc",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_Capacity",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_LeadDays",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "MentorGroupPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAppliedAtUtc",
                table: "MentorGroupPolicies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastAppliedByAdminId",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"
WITH current_active AS (
    SELECT ""Id""
    FROM ""MentorGroupPolicies""
    WHERE ""EffectiveFromUtc"" <= NOW()
    ORDER BY ""EffectiveFromUtc"" DESC, ""Version"" DESC
    LIMIT 1
), fallback_active AS (
    SELECT ""Id""
    FROM ""MentorGroupPolicies""
    ORDER BY ""Version"" DESC
    LIMIT 1
), chosen AS (
    SELECT COALESCE((SELECT ""Id"" FROM current_active), (SELECT ""Id"" FROM fallback_active)) AS ""Id""
)
UPDATE ""MentorGroupPolicies"" p
SET ""IsActive"" = TRUE,
    ""LastAppliedAtUtc"" = COALESCE(p.""EffectiveFromUtc"", p.""CreatedAtUtc"", NOW()),
    ""LastAppliedByAdminId"" = p.""CreatedByAdminId""
FROM chosen
WHERE p.""Id"" = chosen.""Id"";");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicyLevelLimit_MaxActive",
                table: "MentorGroupPolicyLevelLimits",
                sql: "\"MaxActiveClasses\" BETWEEN 0 AND 50");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_EffectiveFromUtc",
                table: "MentorGroupPolicies",
                column: "EffectiveFromUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_LastAppliedByAdminId",
                table: "MentorGroupPolicies",
                column: "LastAppliedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_SingleActive",
                table: "MentorGroupPolicies",
                column: "IsActive",
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_Capacity",
                table: "MentorGroupPolicies",
                sql: "\"MaxLearners\" BETWEEN 1 AND 50");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_LeadDays",
                table: "MentorGroupPolicies",
                sql: "\"MinimumProposalLeadDays\" BETWEEN 1 AND 180 AND \"MinimumPublishedEnrollmentDays\" BETWEEN 1 AND 180 AND \"MinimumPublishedEnrollmentDays\" <= \"MinimumProposalLeadDays\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities",
                sql: "(\"GenerationKey\" IS NULL AND \"PolicyVersion\" IS NULL AND \"MentorScheduleRuleId\" IS NULL AND \"LocalBusinessDate\" IS NULL AND \"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL) OR (\"GenerationKey\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"PolicyVersion\" > 0 AND (\"MentorScheduleRuleId\" IS NOT NULL OR \"TeachingProposalId\" IS NOT NULL) AND \"LocalBusinessDate\" IS NOT NULL AND ((\"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL AND \"MaxLearners\" = 1 AND \"EndTime\" = \"StartTime\" + INTERVAL '90 minutes') OR (\"GroupPolicyId\" IS NOT NULL AND \"SessionMinutesSnapshot\" > 0 AND \"RestMinutesSnapshot\" >= 0 AND \"MaxLearners\" BETWEEN 1 AND 50 AND \"EndTime\" = \"StartTime\" + \"SessionMinutesSnapshot\" * INTERVAL '1 minute')))");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorGroupPolicies_Users_LastAppliedByAdminId",
                table: "MentorGroupPolicies",
                column: "LastAppliedByAdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorGroupPolicies_Users_LastAppliedByAdminId",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicyLevelLimit_MaxActive",
                table: "MentorGroupPolicyLevelLimits");

            migrationBuilder.DropIndex(
                name: "IX_MentorGroupPolicies_EffectiveFromUtc",
                table: "MentorGroupPolicies");

            migrationBuilder.DropIndex(
                name: "IX_MentorGroupPolicies_LastAppliedByAdminId",
                table: "MentorGroupPolicies");

            migrationBuilder.DropIndex(
                name: "IX_MentorGroupPolicies_SingleActive",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_Capacity",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_LeadDays",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "LastAppliedAtUtc",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "LastAppliedByAdminId",
                table: "MentorGroupPolicies");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicyLevelLimit_MaxActive",
                table: "MentorGroupPolicyLevelLimits",
                sql: "\"MaxActiveClasses\" BETWEEN 0 AND 20");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_EffectiveFromUtc",
                table: "MentorGroupPolicies",
                column: "EffectiveFromUtc",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_Capacity",
                table: "MentorGroupPolicies",
                sql: "\"MaxLearners\" BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_LeadDays",
                table: "MentorGroupPolicies",
                sql: "\"MinimumProposalLeadDays\" BETWEEN 1 AND 180 AND \"MinimumPublishedEnrollmentDays\" BETWEEN 1 AND 90 AND \"MinimumPublishedEnrollmentDays\" <= \"MinimumProposalLeadDays\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities",
                sql: "(\"GenerationKey\" IS NULL AND \"PolicyVersion\" IS NULL AND \"MentorScheduleRuleId\" IS NULL AND \"LocalBusinessDate\" IS NULL AND \"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL) OR (\"GenerationKey\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"PolicyVersion\" > 0 AND (\"MentorScheduleRuleId\" IS NOT NULL OR \"TeachingProposalId\" IS NOT NULL) AND \"LocalBusinessDate\" IS NOT NULL AND ((\"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL AND \"MaxLearners\" = 1 AND \"EndTime\" = \"StartTime\" + INTERVAL '90 minutes') OR (\"GroupPolicyId\" IS NOT NULL AND \"SessionMinutesSnapshot\" > 0 AND \"RestMinutesSnapshot\" >= 0 AND \"MaxLearners\" BETWEEN 1 AND 4 AND \"EndTime\" = \"StartTime\" + \"SessionMinutesSnapshot\" * INTERVAL '1 minute')))");
        }
    }
}
