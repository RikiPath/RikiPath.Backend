using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDataSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorAvailabilities_Users_ConsultantId",
                table: "MentorAvailabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_MentorBookings_Users_UserAccountId",
                table: "MentorBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_MentorBookings_ConsultationRequestId",
                table: "Notes");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Users_ConsultantId",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_MentorBookings_UserAccountId",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "MentorBookings");

            migrationBuilder.RenameColumn(
                name: "ConsultationRequestId",
                table: "Notes",
                newName: "MentorBookingId");

            migrationBuilder.RenameColumn(
                name: "ConsultantId",
                table: "Notes",
                newName: "MentorId");

            migrationBuilder.RenameIndex(
                name: "IX_Notes_ConsultationRequestId",
                table: "Notes",
                newName: "IX_Notes_MentorBookingId");

            migrationBuilder.RenameIndex(
                name: "IX_Notes_ConsultantId",
                table: "Notes",
                newName: "IX_Notes_MentorId");

            migrationBuilder.RenameColumn(
                name: "ConsultantId",
                table: "MentorAvailabilities",
                newName: "MentorId");

            migrationBuilder.RenameIndex(
                name: "IX_MentorAvailabilities_ConsultantId_StartTime_EndTime",
                table: "MentorAvailabilities",
                newName: "IX_MentorAvailabilities_MentorId_StartTime_EndTime");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarUrl", "CreatedBy", "CreatedDate", "CurrentStreak", "DailyStudyMinutes", "Email", "EmailNotificationsEnabled", "FirebaseUid", "FirstName", "IsDeleted", "IsEmailVerified", "IsPhoneVerified", "LastName", "LastStudyDate", "LongestStreak", "ModifiedBy", "ModifiedDate", "PasswordHash", "PasswordSalt", "PhoneNumber", "Role", "StudyTimePreference", "SystemNotificationsEnabled", "TargetCertificationLevelId" },
                values: new object[,]
                {
                    { 900001, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "admin@rikipath.local", true, null, "Admin", false, true, false, "System", null, 0, null, null, new byte[] { 91, 215, 217, 54, 239, 206, 181, 216, 143, 33, 82, 211, 9, 70, 197, 173, 197, 0, 72, 71, 59, 150, 46, 114, 96, 137, 52, 174, 137, 29, 196, 61, 50, 254, 249, 101, 114, 92, 149, 32, 55, 127, 205, 126, 216, 147, 197, 192, 248, 173, 158, 92, 104, 42, 195, 107, 150, 15, 3, 74, 103, 104, 3, 229 }, new byte[] { 61, 240, 150, 172, 239, 48, 224, 17, 192, 54, 50, 58, 91, 100, 55, 168, 18, 69, 137, 112, 110, 59, 180, 37, 201, 241, 143, 238, 139, 33, 128, 70, 87, 158, 235, 219, 229, 184, 94, 72, 41, 160, 160, 197, 158, 217, 128, 188, 156, 255, 147, 13, 151, 37, 133, 164, 9, 252, 175, 124, 21, 15, 152, 196 }, null, "Admin", null, true, null },
                    { 900002, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "mentor1@rikipath.local", true, null, "Mentor", false, true, false, "One", null, 0, null, null, new byte[] { 17, 253, 106, 25, 89, 176, 204, 31, 142, 252, 245, 31, 64, 101, 213, 135, 164, 116, 40, 46, 173, 175, 8, 0, 69, 101, 65, 4, 90, 25, 176, 223, 153, 237, 220, 94, 55, 133, 227, 181, 70, 97, 155, 122, 50, 139, 216, 136, 88, 52, 76, 98, 250, 64, 52, 71, 146, 221, 225, 137, 106, 6, 178, 239 }, new byte[] { 233, 77, 244, 252, 171, 97, 38, 140, 50, 138, 10, 54, 122, 165, 186, 85, 129, 82, 49, 10, 98, 44, 165, 57, 50, 7, 238, 166, 142, 224, 94, 49, 134, 226, 247, 110, 154, 139, 99, 204, 95, 39, 171, 235, 212, 26, 217, 87, 139, 118, 253, 125, 241, 255, 57, 22, 140, 171, 11, 168, 174, 202, 32, 17 }, null, "Mentor", null, true, null },
                    { 900003, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "mentor2@rikipath.local", true, null, "Mentor", false, true, false, "Two", null, 0, null, null, new byte[] { 93, 130, 109, 171, 238, 187, 245, 15, 202, 89, 247, 220, 95, 192, 246, 138, 5, 75, 250, 28, 208, 28, 51, 79, 127, 28, 214, 237, 93, 221, 108, 26, 152, 66, 73, 65, 11, 147, 154, 102, 113, 49, 51, 24, 51, 197, 248, 254, 22, 98, 87, 249, 250, 104, 131, 74, 16, 18, 179, 200, 3, 3, 79, 47 }, new byte[] { 240, 155, 254, 183, 129, 162, 74, 247, 41, 9, 67, 242, 128, 8, 226, 85, 5, 104, 104, 72, 101, 75, 130, 95, 47, 101, 116, 89, 207, 23, 119, 178, 38, 180, 185, 145, 58, 115, 14, 30, 85, 216, 169, 133, 94, 115, 11, 117, 96, 87, 227, 240, 204, 48, 51, 144, 170, 248, 167, 56, 175, 194, 253, 135 }, null, "Mentor", null, true, null },
                    { 900004, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "author1@rikipath.local", true, null, "Content", false, true, false, "Author One", null, 0, null, null, new byte[] { 168, 249, 82, 144, 178, 145, 94, 164, 34, 152, 2, 74, 204, 105, 84, 145, 96, 199, 153, 136, 110, 82, 114, 113, 22, 139, 117, 143, 50, 239, 197, 4, 51, 189, 137, 48, 175, 14, 104, 164, 218, 249, 148, 217, 33, 44, 202, 128, 237, 183, 110, 228, 185, 36, 209, 254, 183, 182, 55, 1, 78, 16, 234, 50 }, new byte[] { 19, 216, 84, 143, 3, 184, 50, 39, 209, 137, 149, 82, 224, 206, 6, 240, 237, 242, 172, 27, 55, 92, 242, 101, 149, 76, 104, 38, 176, 43, 40, 8, 102, 202, 146, 224, 62, 43, 147, 132, 2, 158, 81, 170, 29, 103, 76, 248, 101, 105, 213, 205, 32, 51, 59, 39, 221, 11, 135, 250, 193, 8, 78, 219 }, null, "ContentAuthor", null, true, null },
                    { 900005, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "author2@rikipath.local", true, null, "Content", false, true, false, "Author Two", null, 0, null, null, new byte[] { 189, 121, 9, 1, 230, 137, 43, 239, 79, 56, 195, 196, 66, 116, 42, 1, 137, 55, 54, 249, 55, 231, 79, 224, 182, 182, 199, 133, 100, 39, 138, 5, 223, 215, 48, 46, 60, 214, 49, 88, 42, 186, 232, 116, 237, 89, 55, 144, 4, 78, 215, 231, 237, 220, 96, 225, 202, 97, 61, 165, 246, 28, 126, 198 }, new byte[] { 66, 178, 77, 56, 234, 206, 29, 78, 136, 190, 160, 142, 226, 249, 14, 17, 26, 101, 167, 36, 193, 55, 150, 94, 239, 81, 69, 54, 81, 171, 80, 22, 208, 38, 242, 2, 55, 171, 64, 38, 48, 224, 36, 216, 133, 132, 147, 100, 174, 32, 65, 134, 128, 35, 50, 138, 204, 59, 167, 96, 11, 128, 101, 102 }, null, "ContentAuthor", null, true, null }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_MentorAvailabilities_Users_MentorId",
                table: "MentorAvailabilities",
                column: "MentorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_MentorBookings_MentorBookingId",
                table: "Notes",
                column: "MentorBookingId",
                principalTable: "MentorBookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Users_MentorId",
                table: "Notes",
                column: "MentorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorAvailabilities_Users_MentorId",
                table: "MentorAvailabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_MentorBookings_MentorBookingId",
                table: "Notes");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Users_MentorId",
                table: "Notes");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900001);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900002);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900003);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900004);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900005);

            migrationBuilder.RenameColumn(
                name: "MentorId",
                table: "Notes",
                newName: "ConsultantId");

            migrationBuilder.RenameColumn(
                name: "MentorBookingId",
                table: "Notes",
                newName: "ConsultationRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_Notes_MentorId",
                table: "Notes",
                newName: "IX_Notes_ConsultantId");

            migrationBuilder.RenameIndex(
                name: "IX_Notes_MentorBookingId",
                table: "Notes",
                newName: "IX_Notes_ConsultationRequestId");

            migrationBuilder.RenameColumn(
                name: "MentorId",
                table: "MentorAvailabilities",
                newName: "ConsultantId");

            migrationBuilder.RenameIndex(
                name: "IX_MentorAvailabilities_MentorId_StartTime_EndTime",
                table: "MentorAvailabilities",
                newName: "IX_MentorAvailabilities_ConsultantId_StartTime_EndTime");

            migrationBuilder.AddColumn<int>(
                name: "UserAccountId",
                table: "MentorBookings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_UserAccountId",
                table: "MentorBookings",
                column: "UserAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorAvailabilities_Users_ConsultantId",
                table: "MentorAvailabilities",
                column: "ConsultantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MentorBookings_Users_UserAccountId",
                table: "MentorBookings",
                column: "UserAccountId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_MentorBookings_ConsultationRequestId",
                table: "Notes",
                column: "ConsultationRequestId",
                principalTable: "MentorBookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Users_ConsultantId",
                table: "Notes",
                column: "ConsultantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
