using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorPendingSlotOnSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PendingMentorAvailabilityId",
                table: "UserSubscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingMentorQuestion",
                table: "UserSubscriptions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PendingMentorAvailabilityId",
                table: "UserSubscriptions",
                column: "PendingMentorAvailabilityId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_MentorAvailabilities_PendingMentorAvailab~",
                table: "UserSubscriptions",
                column: "PendingMentorAvailabilityId",
                principalTable: "MentorAvailabilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_MentorAvailabilities_PendingMentorAvailab~",
                table: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_PendingMentorAvailabilityId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "PendingMentorAvailabilityId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "PendingMentorQuestion",
                table: "UserSubscriptions");
        }
    }
}
