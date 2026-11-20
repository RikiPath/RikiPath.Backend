using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniversalProfileSecurityAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotificationPreferencesJson",
                table: "Users",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingEmail",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingEmailRequestedAtUtc",
                table: "Users",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TokenVersion",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900001,
                columns: new[] { "NotificationPreferencesJson", "PendingEmail", "PendingEmailRequestedAtUtc", "TokenVersion" },
                values: new object[] { null, null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900002,
                columns: new[] { "NotificationPreferencesJson", "PendingEmail", "PendingEmailRequestedAtUtc", "TokenVersion" },
                values: new object[] { null, null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900003,
                columns: new[] { "NotificationPreferencesJson", "PendingEmail", "PendingEmailRequestedAtUtc", "TokenVersion" },
                values: new object[] { null, null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900004,
                columns: new[] { "NotificationPreferencesJson", "PendingEmail", "PendingEmailRequestedAtUtc", "TokenVersion" },
                values: new object[] { null, null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900005,
                columns: new[] { "NotificationPreferencesJson", "PendingEmail", "PendingEmailRequestedAtUtc", "TokenVersion" },
                values: new object[] { null, null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900006,
                columns: new[] { "NotificationPreferencesJson", "PendingEmail", "PendingEmailRequestedAtUtc", "TokenVersion" },
                values: new object[] { null, null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900007,
                columns: new[] { "NotificationPreferencesJson", "PendingEmail", "PendingEmailRequestedAtUtc", "TokenVersion" },
                values: new object[] { null, null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900008,
                columns: new[] { "NotificationPreferencesJson", "PendingEmail", "PendingEmailRequestedAtUtc", "TokenVersion" },
                values: new object[] { null, null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900009,
                columns: new[] { "NotificationPreferencesJson", "PendingEmail", "PendingEmailRequestedAtUtc", "TokenVersion" },
                values: new object[] { null, null, null, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationPreferencesJson",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PendingEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PendingEmailRequestedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "Users");
        }
    }
}
