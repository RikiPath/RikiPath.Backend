using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileCanonicalGroupCohortModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "StudyCourses",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "StudyCourses",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CapacitySnapshot",
                table: "StudyCourses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupPolicyId",
                table: "StudyCourses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PolicyVersionSnapshot",
                table: "StudyCourses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CapacitySnapshot",
                table: "StudyCohorts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "StudyCohorts",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "GroupPolicyId",
                table: "StudyCohorts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PolicyVersionSnapshot",
                table: "StudyCohorts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_Cohort_ActiveLearner",
                table: "StudyCourses",
                columns: new[] { "CohortId", "LearnerId" },
                unique: true,
                filter: "\"CohortId\" IS NOT NULL AND \"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_GroupPolicyId",
                table: "StudyCourses",
                column: "GroupPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohorts_GroupPolicyId",
                table: "StudyCohorts",
                column: "GroupPolicyId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudyCohort_Capacity",
                table: "StudyCohorts",
                sql: "\"Capacity\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_StudyCohorts_MentorGroupPolicies_GroupPolicyId",
                table: "StudyCohorts",
                column: "GroupPolicyId",
                principalTable: "MentorGroupPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudyCourses_MentorGroupPolicies_GroupPolicyId",
                table: "StudyCourses",
                column: "GroupPolicyId",
                principalTable: "MentorGroupPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudyCohorts_MentorGroupPolicies_GroupPolicyId",
                table: "StudyCohorts");

            migrationBuilder.DropForeignKey(
                name: "FK_StudyCourses_MentorGroupPolicies_GroupPolicyId",
                table: "StudyCourses");

            migrationBuilder.DropIndex(
                name: "IX_StudyCourses_Cohort_ActiveLearner",
                table: "StudyCourses");

            migrationBuilder.DropIndex(
                name: "IX_StudyCourses_GroupPolicyId",
                table: "StudyCourses");

            migrationBuilder.DropIndex(
                name: "IX_StudyCohorts_GroupPolicyId",
                table: "StudyCohorts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StudyCohort_Capacity",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "CapacitySnapshot",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "GroupPolicyId",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "PolicyVersionSnapshot",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "CapacitySnapshot",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "GroupPolicyId",
                table: "StudyCohorts");

            migrationBuilder.DropColumn(
                name: "PolicyVersionSnapshot",
                table: "StudyCohorts");
        }
    }
}
