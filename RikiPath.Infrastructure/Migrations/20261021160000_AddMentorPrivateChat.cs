using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using RikiPath.Infrastructure;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261021160000_AddMentorPrivateChat")]
    partial class AddMentorPrivateChat : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MentorId",
                table: "UserSubscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_MentorId",
                table: "UserSubscriptions",
                column: "MentorId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_Users_MentorId",
                table: "UserSubscriptions",
                column: "MentorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateTable(
                name: "MentorChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    SenderUserId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorChatMessages_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MentorChatMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorChatMessages_MentorBookingId_Id",
                table: "MentorChatMessages",
                columns: new[] { "MentorBookingId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorChatMessages_MentorBookingId_ReadAt_SenderUserId",
                table: "MentorChatMessages",
                columns: new[] { "MentorBookingId", "ReadAt", "SenderUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorChatMessages_SenderUserId",
                table: "MentorChatMessages",
                column: "SenderUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MentorChatMessages");
            migrationBuilder.DropForeignKey(name: "FK_UserSubscriptions_Users_MentorId", table: "UserSubscriptions");
            migrationBuilder.DropIndex(name: "IX_UserSubscriptions_MentorId", table: "UserSubscriptions");
            migrationBuilder.DropColumn(name: "MentorId", table: "UserSubscriptions");
        }
    }
}
