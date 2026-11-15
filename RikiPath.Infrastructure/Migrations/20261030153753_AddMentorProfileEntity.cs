using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorProfileEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MentorProfiles",
                columns: table => new
                {
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Expertise = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    JlptLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: true),
                    Workplace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VerificationDocumentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IntroductionVideoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VerificationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByAdminName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorProfiles", x => x.MentorId);
                    table.ForeignKey(
                        name: "FK_MentorProfiles_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 15, 37, 52, 161, DateTimeKind.Utc).AddTicks(1108));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 15, 37, 52, 161, DateTimeKind.Utc).AddTicks(1114));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 15, 37, 52, 161, DateTimeKind.Utc).AddTicks(1116));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 15, 37, 52, 161, DateTimeKind.Utc).AddTicks(1117));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 15, 37, 52, 161, DateTimeKind.Utc).AddTicks(1118));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 15, 37, 52, 161, DateTimeKind.Utc).AddTicks(1119));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 15, 37, 52, 161, DateTimeKind.Utc).AddTicks(1120));

            migrationBuilder.InsertData(
                table: "MentorProfiles",
                columns: new[] { "MentorId", "Bio", "CreatedBy", "CreatedDate", "Expertise", "IntroductionVideoUrl", "IsDeleted", "JlptLevel", "ModifiedBy", "ModifiedDate", "RejectionReason", "ReviewNotes", "ReviewedAt", "ReviewedByAdminName", "SubmittedAt", "VerificationDocumentUrl", "VerificationStatus", "Workplace", "YearsOfExperience" },
                values: new object[,]
                {
                    { 900002, "Chuyên gia tiếng Nhật với 8 năm kinh nghiệm giảng dạy JLPT N1, N2 và tiếng Nhật doanh nghiệp.", null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Luyện thi JLPT N1-N2, Tiếng Nhật thương mại, Phỏng vấn xin việc", null, false, "N1", null, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Admin System", new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), null, "Approved", "Riki Nihongo Academy", 8 },
                    { 900003, "Giảng viên tiếng Nhật bản xứ, chuyên sâu về phát âm và giao tiếp Kaiwa thực tế.", null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Giao tiếp Kaiwa tự nhiên, Luyện phát âm chuẩn Tokyo, JLPT N3-N2", null, false, "N1", null, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Admin System", new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), null, "Approved", "Riki Nihongo Academy", 5 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MentorProfiles");

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 12, 56, 9, 149, DateTimeKind.Utc).AddTicks(6610));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 12, 56, 9, 149, DateTimeKind.Utc).AddTicks(6620));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 12, 56, 9, 149, DateTimeKind.Utc).AddTicks(6680));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 12, 56, 9, 149, DateTimeKind.Utc).AddTicks(6681));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 12, 56, 9, 149, DateTimeKind.Utc).AddTicks(6682));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 12, 56, 9, 149, DateTimeKind.Utc).AddTicks(6683));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 30, 12, 56, 9, 149, DateTimeKind.Utc).AddTicks(6684));
        }
    }
}
