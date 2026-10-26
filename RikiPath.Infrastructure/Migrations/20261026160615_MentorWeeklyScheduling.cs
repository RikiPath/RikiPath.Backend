using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MentorWeeklyScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MentorBookings_MentorAvailabilityId_UserSubscriptionId",
                table: "MentorBookings");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "UserSubscriptions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "MentorBookings",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AlterColumn<int>(
                name: "MaxLearners",
                table: "MentorAvailabilities",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 4);

            migrationBuilder.AddColumn<string>(
                name: "ChangeRequestReason",
                table: "MentorAvailabilities",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChangeRequestedAt",
                table: "MentorAvailabilities",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeResolution",
                table: "MentorAvailabilities",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MentorWeeklyAvailabilityId",
                table: "MentorAvailabilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "MentorAvailabilities",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "MentorAvailabilities",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "MentorWeeklyAvailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Period = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorWeeklyAvailabilities", x => x.Id);
                    table.CheckConstraint("CK_MentorWeeklyAvailability_Period", "\"Period\" IN ('Morning', 'Afternoon', 'Evening')");
                    table.ForeignKey(
                        name: "FK_MentorWeeklyAvailabilities_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_MentorAvailabilityId_UserSubscriptionId",
                table: "MentorBookings",
                columns: new[] { "MentorAvailabilityId", "UserSubscriptionId" },
                unique: true,
                filter: "NOT \"IsDeleted\" AND \"Status\" <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_MentorWeeklyAvailabilityId",
                table: "MentorAvailabilities",
                column: "MentorWeeklyAvailabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorWeeklyAvailabilities_MentorId_Date_Period",
                table: "MentorWeeklyAvailabilities",
                columns: new[] { "MentorId", "Date", "Period" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorAvailabilities_MentorWeeklyAvailabilities_MentorWeekl~",
                table: "MentorAvailabilities",
                column: "MentorWeeklyAvailabilityId",
                principalTable: "MentorWeeklyAvailabilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorAvailabilities_MentorWeeklyAvailabilities_MentorWeekl~",
                table: "MentorAvailabilities");

            migrationBuilder.DropTable(
                name: "MentorWeeklyAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_MentorBookings_MentorAvailabilityId_UserSubscriptionId",
                table: "MentorBookings");

            migrationBuilder.DropIndex(
                name: "IX_MentorAvailabilities_MentorWeeklyAvailabilityId",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "ChangeRequestReason",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "ChangeRequestedAt",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "ChangeResolution",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "MentorWeeklyAvailabilityId",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "MentorAvailabilities");

            migrationBuilder.AlterColumn<int>(
                name: "MaxLearners",
                table: "MentorAvailabilities",
                type: "integer",
                nullable: false,
                defaultValue: 4,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_MentorAvailabilityId_UserSubscriptionId",
                table: "MentorBookings",
                columns: new[] { "MentorAvailabilityId", "UserSubscriptionId" },
                unique: true);
        }
    }
}
