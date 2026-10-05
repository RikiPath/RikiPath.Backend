using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreLearnersToSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorBookings_MentorAvailabilities_MentorAvailabilityId",
                table: "MentorBookings");

            migrationBuilder.DropIndex(
                name: "IX_MentorBookings_MentorAvailabilityId",
                table: "MentorBookings");

            migrationBuilder.DropIndex(
                name: "IX_MentorBookings_RoomId",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "IsBooked",
                table: "MentorAvailabilities");

            migrationBuilder.AddColumn<int>(
                name: "BookedCount",
                table: "MentorAvailabilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxLearners",
                table: "MentorAvailabilities",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "MentorAvailabilities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MentorAvailabilities",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            // Cập nhật slot 990001 có BookedCount = 4
            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990001,
                columns: new[] { "BookedCount", "MaxLearners", "RoomId" },
                values: new object[] { 4, 4, new Guid("a1000000-0000-0000-0000-000000990001") });

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990002,
                columns: new[] { "BookedCount", "MaxLearners", "RoomId" },
                values: new object[] { 0, 4, new Guid("a1000000-0000-0000-0000-000000990002") });

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990003,
                columns: new[] { "BookedCount", "MaxLearners", "RoomId" },
                values: new object[] { 0, 4, new Guid("a1000000-0000-0000-0000-000000990003") });

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990004,
                columns: new[] { "BookedCount", "MaxLearners", "RoomId" },
                values: new object[] { 0, 4, new Guid("a1000000-0000-0000-0000-000000990004") });

            migrationBuilder.UpdateData(
                table: "MentorBookings",
                keyColumn: "Id",
                keyValue: 990001,
                columns: new[] { "MeetingLink", "Question" },
                values: new object[] { "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", "Meeting demo để kiểm thử phòng họp trên frontend." });

            migrationBuilder.UpdateData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990001,
                column: "PurchasedAt",
                value: new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc));


            migrationBuilder.InsertData(
                table: "UserSubscriptions",
                columns: new[] { "Id", "AiGradingQuota", "AiGradingUsedCount", "AmountPaid", "CreatedBy", "CreatedDate", "EndDate", "IsDeleted", "MeetingSessionsIncluded", "MeetingSessionsUsed", "ModifiedBy", "ModifiedDate", "PaymentExpiresAt", "PaymentStatus", "PaymentTransactionId", "PurchasedAt", "StartDate", "SubscriptionPlanId", "UserId" },
                values: new object[,]
                {
                    { 990002, 0, 0, 459000m, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Utc), false, 12, 1, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS-2", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 7, 900011 },
                    { 990003, 0, 0, 459000m, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Utc), false, 12, 1, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS-3", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 7, 900009 },
                    { 990004, 0, 0, 459000m, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Utc), false, 12, 1, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS-4", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 7, 900010 }
                });

            migrationBuilder.InsertData(
                table: "MentorBookings",
                columns: new[] { "Id", "CompletedAt", "CreatedBy", "CreatedDate", "IsDeleted", "MeetingLink", "MentorAvailabilityId", "ModifiedBy", "ModifiedDate", "Question", "ScheduledAt", "Status", "UserSubscriptionId" },
                values: new object[,]
                {
                    { 990002, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", 990001, null, null, "Em muốn nhờ Mentor giải đáp phần ngữ pháp N3 bài 5.", new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Utc), "Assigned", 990002 },
                    { 990003, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", 990001, null, null, "Hỏi về cách phân biệt giữa ~てくる và ~ていく.", new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Utc), "Assigned", 990003 },
                    { 990004, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", 990001, null, null, "Tư vấn phương pháp luyện nghe JLPT N3 hiệu quả.", new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Utc), "Assigned", 990004 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_MentorAvailabilityId_UserSubscriptionId",
                table: "MentorBookings",
                columns: new[] { "MentorAvailabilityId", "UserSubscriptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_RoomId",
                table: "MentorAvailabilities",
                column: "RoomId",
                unique: true);

            // Sửa cú pháp Check Constraint tương thích PostgreSQL (ngoặc kép thay vì ngoặc vuông)
            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorAvailability_BookedCount",
                table: "MentorAvailabilities",
                sql: "\"BookedCount\" >= 0 AND \"BookedCount\" <= \"MaxLearners\"");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorBookings_MentorAvailabilities_MentorAvailabilityId",
                table: "MentorBookings",
                column: "MentorAvailabilityId",
                principalTable: "MentorAvailabilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorBookings_MentorAvailabilities_MentorAvailabilityId",
                table: "MentorBookings");

            migrationBuilder.DropIndex(
                name: "IX_MentorBookings_MentorAvailabilityId_UserSubscriptionId",
                table: "MentorBookings");

            migrationBuilder.DropIndex(
                name: "IX_MentorAvailabilities_RoomId",
                table: "MentorAvailabilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorAvailability_BookedCount",
                table: "MentorAvailabilities");

            migrationBuilder.DeleteData(
                table: "MentorBookings",
                keyColumn: "Id",
                keyValue: 990002);

            migrationBuilder.DeleteData(
                table: "MentorBookings",
                keyColumn: "Id",
                keyValue: 990003);

            migrationBuilder.DeleteData(
                table: "MentorBookings",
                keyColumn: "Id",
                keyValue: 990004);

            migrationBuilder.DeleteData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990002);

            migrationBuilder.DeleteData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990003);

            migrationBuilder.DeleteData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990004);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900011);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900009);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900010);

            migrationBuilder.DropColumn(
                name: "BookedCount",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "MaxLearners",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MentorAvailabilities");

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "MentorBookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBooked",
                table: "MentorAvailabilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990001,
                column: "IsBooked",
                value: true);

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990002,
                column: "IsBooked",
                value: false);

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990003,
                column: "IsBooked",
                value: false);

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990004,
                column: "IsBooked",
                value: false);

            migrationBuilder.UpdateData(
                table: "MentorBookings",
                keyColumn: "Id",
                keyValue: 990001,
                columns: new[] { "MeetingLink", "Question", "RoomId" },
                values: new object[] { "http://localhost:5173/mentor-meeting/5d75c4cc-5404-4f9a-91e8-b8596f31d2a2", "Meeting demo để kiểm thử phòng 1-1 trên frontend.", new Guid("5d75c4cc-5404-4f9a-91e8-b8596f31d2a2") });

            migrationBuilder.UpdateData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990001,
                column: "PurchasedAt",
                value: new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_MentorAvailabilityId",
                table: "MentorBookings",
                column: "MentorAvailabilityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_RoomId",
                table: "MentorBookings",
                column: "RoomId",
                unique: true,
                filter: "\"RoomId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorBookings_MentorAvailabilities_MentorAvailabilityId",
                table: "MentorBookings",
                column: "MentorAvailabilityId",
                principalTable: "MentorAvailabilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
