using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    public partial class AddMentorAttendancePersistence : Migration
    {
            protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MentorAttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FirstJoinedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastJoinedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LeftAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReconnectionCount = table.Column<int>(type: "integer", nullable: false),
                    TotalDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorAttendanceRecords", x => x.Id);
                    table.CheckConstraint("CK_MentorAttendanceRecord_Duration", "\"TotalDurationSeconds\" >= 0");
                    table.CheckConstraint("CK_MentorAttendanceRecord_Reconnection", "\"ReconnectionCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_MentorAttendanceRecords_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MentorAttendanceRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });






migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_Booking_User",
                table: "MentorAttendanceRecords",
                columns: new[] { "MentorBookingId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_RoomId",
                table: "MentorAttendanceRecords",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_Status",
                table: "MentorAttendanceRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_UserId",
                table: "MentorAttendanceRecords",
                column: "UserId");
        }

            protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MentorAttendanceRecords");






}
    }
}
