using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RikiPath.Infrastructure;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260930170000_AddMentorMeetingBookingFlow")]
    public partial class AddMentorMeetingBookingFlow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MeetingSessionCount",
                table: "SubscriptionPlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MeetingSessionsIncluded",
                table: "UserSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MeetingSessionsUsed",
                table: "UserSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentExpiresAt",
                table: "UserSubscriptions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "MentorBookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_RoomId",
                table: "MentorBookings",
                column: "RoomId",
                unique: true,
                filter: "\"RoomId\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MentorBookings_RoomId",
                table: "MentorBookings");

            migrationBuilder.DropColumn(name: "MeetingSessionCount", table: "SubscriptionPlans");
            migrationBuilder.DropColumn(name: "MeetingSessionsIncluded", table: "UserSubscriptions");
            migrationBuilder.DropColumn(name: "MeetingSessionsUsed", table: "UserSubscriptions");
            migrationBuilder.DropColumn(name: "PaymentExpiresAt", table: "UserSubscriptions");
            migrationBuilder.DropColumn(name: "RoomId", table: "MentorBookings");
        }
    }
}
