using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSomeFieldIntoEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TargetExamDate",
                table: "Users",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3635));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3649));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3651));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3653));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3654));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3658));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3660));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3662));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3663));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3666));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3668));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3669));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3671));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3672));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 15,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3674));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 16,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3675));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 17,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3677));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 18,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3680));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 19,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3681));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 20,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3714));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 21,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3716));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 22,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3718));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 23,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3719));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 24,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3720));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 25,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3721));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 26,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3723));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 27,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3724));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 28,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3725));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 29,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3726));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 30,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3728));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 31,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3729));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 32,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3731));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 33,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3732));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 34,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3736));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 35,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3737));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 36,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3739));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 37,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3740));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 38,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3741));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 39,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3743));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 40,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3744));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 41,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3745));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 42,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3747));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 43,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3748));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 44,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3749));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 45,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3830));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 46,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3832));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 47,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3863));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 48,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3866));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 49,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3868));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 50,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3869));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 51,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3870));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 52,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3871));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 53,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3872));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 54,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3874));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 55,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3875));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 56,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3876));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 57,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3877));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 58,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3879));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 59,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3880));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 60,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3881));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 61,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3882));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 62,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3884));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 63,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3885));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 64,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3886));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 65,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3887));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 66,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3948));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 67,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3949));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 68,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3951));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 69,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3952));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 70,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3953));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 71,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3954));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 72,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3955));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 73,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3957));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 74,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3958));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 75,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3959));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 76,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3960));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 77,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3961));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 78,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3963));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 79,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3964));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 80,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3965));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 81,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3966));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 82,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3967));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 83,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3969));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 84,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3970));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 85,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3971));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 86,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3972));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 87,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3974));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 88,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3975));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 89,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3976));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 90,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3977));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 91,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(3978));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 92,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 22, 18, 9, 17, 553, DateTimeKind.Utc).AddTicks(4021));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900001,
                column: "TargetExamDate",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900002,
                column: "TargetExamDate",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900003,
                column: "TargetExamDate",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900004,
                column: "TargetExamDate",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900005,
                column: "TargetExamDate",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900006,
                column: "TargetExamDate",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900007,
                column: "TargetExamDate",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900008,
                column: "TargetExamDate",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900009,
                column: "TargetExamDate",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetExamDate",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7153));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7176));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7178));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7180));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7181));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7185));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7186));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7187));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7188));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7190));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7192));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7193));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7194));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7195));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 15,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7196));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 16,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7198));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 17,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7199));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 18,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7202));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 19,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7203));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 20,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7204));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 21,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7205));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 22,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7206));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 23,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7207));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 24,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7285));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 25,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7286));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 26,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7287));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 27,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7288));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 28,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7290));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 29,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7291));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 30,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7293));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 31,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7294));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 32,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7295));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 33,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7296));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 34,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7298));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 35,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7299));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 36,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7300));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 37,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7301));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 38,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7302));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 39,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7303));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 40,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7304));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 41,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7306));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 42,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7307));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 43,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7308));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 44,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7309));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 45,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7310));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 46,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7311));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 47,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7395));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 48,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7398));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 49,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7399));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 50,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7400));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 51,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7401));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 52,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7402));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 53,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7403));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 54,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7404));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 55,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7405));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 56,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7407));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 57,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7408));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 58,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7409));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 59,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7410));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 60,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7411));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 61,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7412));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 62,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7413));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 63,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7414));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 64,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7415));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 65,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7416));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 66,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7419));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 67,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7420));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 68,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7421));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 69,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7463));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 70,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7464));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 71,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7465));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 72,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7466));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 73,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7468));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 74,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7469));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 75,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7470));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 76,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7471));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 77,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7472));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 78,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7473));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 79,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7474));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 80,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7475));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 81,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7477));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 82,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7478));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 83,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7479));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 84,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7481));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 85,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7482));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 86,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7483));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 87,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7484));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 88,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7485));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 89,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7486));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 90,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7487));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 91,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7488));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 92,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7489));
        }
    }
}
