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
                table: "Users",
                columns: new[] { "Id", "AvatarUrl", "CreatedBy", "CreatedDate", "CurrentStreak", "DailyStudyMinutes", "Email", "EmailNotificationsEnabled", "FirebaseUid", "FirstName", "IsDeleted", "IsEmailVerified", "IsPhoneVerified", "LastName", "LastStudyDate", "LongestStreak", "ModifiedBy", "ModifiedDate", "PasswordHash", "PasswordSalt", "PhoneNumber", "Role", "StudyTimePreference", "SystemNotificationsEnabled", "TargetCertificationLevelId" },
                values: new object[,]
                {
                    { 900011, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "learner2.meeting@rikipath.local", true, null, "Linh", false, true, false, "Nguyen", null, 0, null, null, new byte[] { 217, 79, 50, 159, 85, 88, 67, 15, 23, 182, 103, 80, 220, 85, 86, 173, 204, 186, 57, 67, 183, 165, 253, 243, 150, 250, 150, 190, 122, 252, 55, 169, 85, 236, 69, 106, 19, 219, 244, 42, 239, 30, 189, 176, 52, 47, 30, 28, 125, 31, 162, 255, 61, 3, 238, 201, 168, 88, 161, 42, 216, 86, 172, 219 }, new byte[] { 85, 97, 108, 158, 219, 70, 81, 106, 73, 23, 205, 163, 105, 159, 117, 19, 175, 147, 178, 99, 112, 116, 163, 243, 109, 178, 129, 229, 245, 175, 91, 174, 66, 198, 122, 88, 80, 68, 160, 64, 85, 118, 105, 41, 135, 89, 49, 160, 120, 13, 116, 148, 255, 27, 157, 207, 106, 203, 127, 74, 19, 61, 148, 120 }, null, "Learner", null, true, null },
                    { 900009, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "learner3.meeting@rikipath.local", true, null, "Minh", false, true, false, "Tran", null, 0, null, null, new byte[] { 84, 97, 164, 196, 63, 151, 182, 39, 139, 242, 152, 22, 179, 33, 174, 113, 0, 236, 253, 114, 234, 242, 209, 76, 196, 162, 143, 195, 180, 3, 178, 193, 205, 230, 18, 114, 49, 151, 198, 217, 254, 21, 94, 176, 146, 214, 235, 122, 28, 241, 164, 161, 4, 161, 209, 38, 102, 179, 31, 107, 157, 25, 35, 63 }, new byte[] { 26, 188, 78, 205, 57, 115, 100, 129, 148, 135, 165, 205, 148, 203, 245, 36, 244, 177, 9, 104, 120, 140, 21, 164, 36, 75, 122, 51, 115, 69, 174, 180, 120, 155, 62, 37, 97, 227, 212, 134, 200, 7, 66, 255, 219, 10, 140, 149, 67, 86, 164, 170, 142, 165, 181, 103, 47, 100, 238, 24, 178, 29, 171, 226 }, null, "Learner", null, true, null },
                    { 900010, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "learner4.meeting@rikipath.local", true, null, "Hoang", false, true, false, "Pham", null, 0, null, null, new byte[] { 42, 30, 236, 129, 234, 192, 63, 84, 148, 163, 154, 246, 107, 147, 189, 95, 33, 141, 196, 67, 181, 51, 157, 136, 93, 228, 237, 165, 164, 49, 4, 146, 87, 84, 9, 47, 28, 59, 33, 130, 37, 63, 43, 229, 8, 178, 123, 74, 61, 159, 235, 68, 217, 51, 225, 29, 201, 104, 44, 152, 53, 255, 199, 28 }, new byte[] { 83, 214, 138, 3, 142, 91, 229, 97, 238, 61, 48, 237, 80, 82, 112, 249, 166, 81, 53, 95, 170, 120, 13, 87, 113, 159, 60, 37, 75, 229, 226, 107, 229, 89, 60, 57, 109, 196, 235, 81, 145, 154, 110, 120, 141, 13, 142, 77, 41, 132, 58, 80, 154, 57, 226, 168, 176, 251, 113, 167, 242, 139, 7, 177 }, null, "Learner", null, true, null }
                });

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