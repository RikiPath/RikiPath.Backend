using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotMentorGroupPolicyOnSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities");

            migrationBuilder.AddColumn<int>(
                name: "GroupPolicyId",
                table: "MentorAvailabilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestMinutesSnapshot",
                table: "MentorAvailabilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionMinutesSnapshot",
                table: "MentorAvailabilities",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990001,
                columns: new[] { "GroupPolicyId", "RestMinutesSnapshot", "SessionMinutesSnapshot" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990002,
                columns: new[] { "GroupPolicyId", "RestMinutesSnapshot", "SessionMinutesSnapshot" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990003,
                columns: new[] { "GroupPolicyId", "RestMinutesSnapshot", "SessionMinutesSnapshot" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990004,
                columns: new[] { "GroupPolicyId", "RestMinutesSnapshot", "SessionMinutesSnapshot" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_GroupPolicyId",
                table: "MentorAvailabilities",
                column: "GroupPolicyId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities",
                sql: "(\"GenerationKey\" IS NULL AND \"PolicyVersion\" IS NULL AND \"MentorScheduleRuleId\" IS NULL AND \"LocalBusinessDate\" IS NULL AND \"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL) OR (\"GenerationKey\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"PolicyVersion\" > 0 AND \"MentorScheduleRuleId\" IS NOT NULL AND \"LocalBusinessDate\" IS NOT NULL AND ((\"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL AND \"MaxLearners\" = 1 AND \"EndTime\" = \"StartTime\" + INTERVAL '90 minutes') OR (\"GroupPolicyId\" IS NOT NULL AND \"SessionMinutesSnapshot\" > 0 AND \"RestMinutesSnapshot\" >= 0 AND \"MaxLearners\" BETWEEN 1 AND 4 AND \"EndTime\" = \"StartTime\" + \"SessionMinutesSnapshot\" * INTERVAL '1 minute')))");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorAvailabilities_MentorGroupPolicies_GroupPolicyId",
                table: "MentorAvailabilities",
                column: "GroupPolicyId",
                principalTable: "MentorGroupPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorAvailabilities_MentorGroupPolicies_GroupPolicyId",
                table: "MentorAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_MentorAvailabilities_GroupPolicyId",
                table: "MentorAvailabilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "GroupPolicyId",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "RestMinutesSnapshot",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "SessionMinutesSnapshot",
                table: "MentorAvailabilities");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities",
                sql: "(\"GenerationKey\" IS NULL AND \"PolicyVersion\" IS NULL AND \"MentorScheduleRuleId\" IS NULL AND \"LocalBusinessDate\" IS NULL) OR (\"GenerationKey\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"PolicyVersion\" > 0 AND \"MentorScheduleRuleId\" IS NOT NULL AND \"LocalBusinessDate\" IS NOT NULL AND \"MaxLearners\" = 1 AND \"EndTime\" = \"StartTime\" + INTERVAL '90 minutes')");
        }
    }
}
