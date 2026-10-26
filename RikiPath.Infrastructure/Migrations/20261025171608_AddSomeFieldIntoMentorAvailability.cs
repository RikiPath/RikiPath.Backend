using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSomeFieldIntoMentorAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "MentorAvailabilities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelRejectedAt",
                table: "MentorAvailabilities",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelRejectionReason",
                table: "MentorAvailabilities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelRequestedAt",
                table: "MentorAvailabilities",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9070));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9084));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9086));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9088));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9089));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9199));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9200));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9202));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9204));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9206));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9208));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9209));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9211));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9213));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 15,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9214));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 16,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9216));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 17,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9217));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 18,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9221));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 19,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9222));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 20,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9223));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 21,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9282));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 22,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9284));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 23,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9285));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 24,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9287));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 25,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9288));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 26,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9290));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 27,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9291));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 28,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9293));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 29,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9294));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 30,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9296));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 31,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9297));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 32,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9298));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 33,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9299));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 34,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9302));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 35,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9303));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 36,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9305));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 37,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 38,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9308));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 39,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9309));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 40,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9310));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 41,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9312));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 42,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9313));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 43,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9314));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 44,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9316));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 45,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9317));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 46,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9319));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 47,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9375));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 48,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9379));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 49,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9381));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 50,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9382));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 51,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9383));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 52,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9385));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 53,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9386));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 54,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9387));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 55,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9389));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 56,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9390));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 57,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9391));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 58,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9392));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 59,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9394));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 60,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9395));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 61,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9396));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 62,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9398));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 63,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9399));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 64,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9400));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 65,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9401));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 66,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9490));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 67,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9493));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 68,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9494));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 69,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9495));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 70,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9497));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 71,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9498));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 72,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9499));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 73,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9501));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 74,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9502));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 75,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9503));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 76,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9505));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 77,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9506));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 78,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9507));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 79,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9509));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 80,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9510));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 81,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9512));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 82,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9513));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 83,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9515));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 84,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9516));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 85,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9518));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 86,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9519));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 87,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9520));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 88,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9522));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 89,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9523));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 90,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9524));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 91,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9526));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 92,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 25, 17, 16, 5, 127, DateTimeKind.Utc).AddTicks(9527));

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990001,
                columns: new[] { "CancelReason", "CancelRejectedAt", "CancelRejectionReason", "CancelRequestedAt" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990002,
                columns: new[] { "CancelReason", "CancelRejectedAt", "CancelRejectionReason", "CancelRequestedAt" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990003,
                columns: new[] { "CancelReason", "CancelRejectedAt", "CancelRejectionReason", "CancelRequestedAt" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990004,
                columns: new[] { "CancelReason", "CancelRejectedAt", "CancelRejectionReason", "CancelRequestedAt" },
                values: new object[] { null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "CancelRejectedAt",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "CancelRejectionReason",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "CancelRequestedAt",
                table: "MentorAvailabilities");

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
        }
    }
}
