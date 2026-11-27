using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyInternalRevisionAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MentorGroupPolicies_Version",
                table: "MentorGroupPolicies");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "MentorGroupPolicies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedByAdminId",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchivedRevision",
                table: "MentorGroupPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MentorGroupPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "MentorGroupPolicies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByAdminId",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MentorGroupPolicies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DeletedAtUtc", "DeletedByAdminId", "IsArchivedRevision", "IsDeleted", "Revision", "UpdatedAtUtc", "UpdatedByAdminId" },
                values: new object[] { null, null, false, false, 1, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_DeletedByAdminId",
                table: "MentorGroupPolicies",
                column: "DeletedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_LiveVersion",
                table: "MentorGroupPolicies",
                column: "Version",
                unique: true,
                filter: "\"IsArchivedRevision\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_UpdatedByAdminId",
                table: "MentorGroupPolicies",
                column: "UpdatedByAdminId");

            migrationBuilder.Sql(@"
UPDATE ""MentorGroupPolicies""
SET ""Revision"" = 1
WHERE ""Revision"" <= 0;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_State",
                table: "MentorGroupPolicies",
                sql: "\"Revision\" > 0 AND NOT (\"IsActive\" AND (\"IsArchivedRevision\" OR \"IsDeleted\"))");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorGroupPolicies_Users_DeletedByAdminId",
                table: "MentorGroupPolicies",
                column: "DeletedByAdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MentorGroupPolicies_Users_UpdatedByAdminId",
                table: "MentorGroupPolicies",
                column: "UpdatedByAdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorGroupPolicies_Users_DeletedByAdminId",
                table: "MentorGroupPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_MentorGroupPolicies_Users_UpdatedByAdminId",
                table: "MentorGroupPolicies");

            migrationBuilder.DropIndex(
                name: "IX_MentorGroupPolicies_DeletedByAdminId",
                table: "MentorGroupPolicies");

            migrationBuilder.DropIndex(
                name: "IX_MentorGroupPolicies_LiveVersion",
                table: "MentorGroupPolicies");

            migrationBuilder.DropIndex(
                name: "IX_MentorGroupPolicies_UpdatedByAdminId",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_State",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "DeletedByAdminId",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "IsArchivedRevision",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "UpdatedByAdminId",
                table: "MentorGroupPolicies");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_Version",
                table: "MentorGroupPolicies",
                column: "Version",
                unique: true);
        }
    }
}
