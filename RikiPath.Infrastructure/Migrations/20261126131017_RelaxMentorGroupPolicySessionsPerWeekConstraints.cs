using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RelaxMentorGroupPolicySessionsPerWeekConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_AllowedSessionsPerWeek",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_DefaultSessionsPerWeek",
                table: "MentorGroupPolicies");

            migrationBuilder.AlterColumn<string>(
                name: "AllowedSessionsPerWeekCsv",
                table: "MentorGroupPolicies",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_AllowedSessionsPerWeek",
                table: "MentorGroupPolicies",
                sql: "\"AllowedSessionsPerWeekCsv\" ~ '^[1-9][0-9]*(,[1-9][0-9]*)*$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_DefaultSessionsPerWeek",
                table: "MentorGroupPolicies",
                sql: "\"DefaultSessionsPerWeek\" >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_AllowedSessionsPerWeek",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_DefaultSessionsPerWeek",
                table: "MentorGroupPolicies");

            migrationBuilder.AlterColumn<string>(
                name: "AllowedSessionsPerWeekCsv",
                table: "MentorGroupPolicies",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_AllowedSessionsPerWeek",
                table: "MentorGroupPolicies",
                sql: "\"AllowedSessionsPerWeekCsv\" ~ '^[1-7](,[1-7]){0,6}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_DefaultSessionsPerWeek",
                table: "MentorGroupPolicies",
                sql: "\"DefaultSessionsPerWeek\" BETWEEN 1 AND 7");
        }
    }
}
