using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    public partial class AddMentorBookingCancellationMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "MentorBookings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "MentorBookings",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByRole",
                table: "MentorBookings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelledByUserId",
                table: "MentorBookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExceptionApproved",
                table: "MentorBookings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionReason",
                table: "MentorBookings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExceptionRequested",
                table: "MentorBookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "CancelledByRole",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "ExceptionApproved",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "ExceptionReason",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "ExceptionRequested",
                table: "MentorBookings");        }
    }
}
