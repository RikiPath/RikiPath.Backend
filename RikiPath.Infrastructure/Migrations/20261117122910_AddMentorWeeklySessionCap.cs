using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorWeeklySessionCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeeklySessionCap",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "MentorGroupPolicies",
                keyColumn: "Id",
                keyValue: 1,
                column: "WeeklySessionCap",
                value: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_WeeklySessionCap",
                table: "MentorGroupPolicies",
                sql: "\"WeeklySessionCap\" BETWEEN 0 AND 84");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_WeeklySessionCap",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "WeeklySessionCap",
                table: "MentorGroupPolicies");
        }
    }
}
