using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HasDataMentorBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990001,
                column: "IsBooked",
                value: true);

            migrationBuilder.InsertData(
                table: "MentorBookings",
                columns: new[] { "Id", "CompletedAt", "CreatedBy", "CreatedDate", "IsDeleted", "MeetingLink", "MentorAvailabilityId", "ModifiedBy", "ModifiedDate", "Question", "RoomId", "ScheduledAt", "Status", "UserSubscriptionId" },
                values: new object[] { 990001, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "http://localhost:5173/mentor-meeting/5d75c4cc-5404-4f9a-91e8-b8596f31d2a2", 990001, null, null, "Meeting demo để kiểm thử phòng 1-1 trên frontend.", new Guid("5d75c4cc-5404-4f9a-91e8-b8596f31d2a2"), new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified), "Assigned", 990001 });

            migrationBuilder.UpdateData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990001,
                column: "MeetingSessionsUsed",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "MentorBookings",
                keyColumn: "Id",
                keyValue: 990001);

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990001,
                column: "IsBooked",
                value: false);

            migrationBuilder.UpdateData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990001,
                column: "MeetingSessionsUsed",
                value: 0);
        }
    }
}
