using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldIntoMockTestAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuestionSnapshotJson",
                table: "MockTestAttempts",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(8993));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9002));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9004));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9005));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9006));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9008));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9009));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6562));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6586));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6588));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6589));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6591));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6595));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6596));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6598));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6599));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6602));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6603));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6605));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6606));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6608));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 15,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6609));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 16,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6610));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 17,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6612));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 18,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6614));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 19,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6616));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 20,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6673));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 21,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6675));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 22,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6676));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 23,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6677));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 24,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6679));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 25,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6680));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 26,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6681));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 27,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6683));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 28,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6684));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 29,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6685));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 30,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6687));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 31,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6689));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 32,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6690));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 33,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6691));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 34,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6694));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 35,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6695));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 36,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6697));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 37,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6698));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 38,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6699));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 39,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6701));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 40,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6702));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 41,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6704));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 42,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6705));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 43,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6707));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 44,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6708));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 45,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6709));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 46,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6757));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 47,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6786));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 48,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6789));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 49,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6790));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 50,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6792));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 51,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6793));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 52,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6795));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 53,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6796));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 54,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6797));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 55,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6799));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 56,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6800));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 57,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6802));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 58,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6803));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 59,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6805));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 60,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6806));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 61,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6808));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 62,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6809));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 63,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6811));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 64,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6812));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 65,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6813));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 66,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6864));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 67,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6865));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 68,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6867));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 69,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6868));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 70,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6869));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 71,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6871));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 72,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6873));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 73,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6874));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 74,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6875));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 75,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6877));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 76,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6878));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 77,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6880));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 78,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6881));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 79,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6883));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 80,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6884));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 81,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6885));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 82,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6887));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 83,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6888));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 84,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6890));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 85,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6891));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 86,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6893));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 87,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6894));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 88,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6895));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 89,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6897));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 90,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6898));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 91,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6899));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 92,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6901));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuestionSnapshotJson",
                table: "MockTestAttempts");

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2484));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2492));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2494));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2496));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2497));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2499));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2501));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9333));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9346));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9349));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9350));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9352));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9425));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9426));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9428));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9429));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9432));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9433));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9434));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9436));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9437));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 15,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9439));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 16,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9440));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 17,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9442));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 18,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9445));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 19,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9446));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 20,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9448));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 21,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9449));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 22,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9450));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 23,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9452));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 24,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9453));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 25,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9455));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 26,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9456));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 27,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9458));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 28,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9459));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 29,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9461));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 30,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9462));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 31,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9464));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 32,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9500));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 33,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9502));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 34,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9505));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 35,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9506));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 36,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9508));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 37,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9510));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 38,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9511));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 39,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9513));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 40,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9514));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 41,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9516));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 42,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9517));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 43,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9519));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 44,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9520));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 45,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9521));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 46,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9522));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 47,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9552));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 48,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9555));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 49,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9556));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 50,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9558));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 51,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9559));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 52,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9645));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 53,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9647));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 54,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9648));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 55,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9650));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 56,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9651));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 57,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9652));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 58,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9654));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 59,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9655));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 60,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9656));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 61,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9657));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 62,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9659));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 63,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9660));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 64,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9661));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 65,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9662));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 66,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9665));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 67,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9667));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 68,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9668));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 69,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9669));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 70,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9671));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 71,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9672));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 72,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9673));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 73,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9675));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 74,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9676));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 75,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9678));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 76,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9744));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 77,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9745));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 78,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9746));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 79,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9748));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 80,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9749));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 81,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9751));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 82,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9752));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 83,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9754));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 84,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9755));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 85,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9756));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 86,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9758));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 87,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9759));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 88,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9760));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 89,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9762));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 90,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9763));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 91,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9765));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 92,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9766));
        }
    }
}
