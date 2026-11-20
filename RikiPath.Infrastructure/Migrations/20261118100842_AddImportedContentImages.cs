using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportedContentImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Vocabularies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "PracticeQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "PracticeQuestionOptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "PracticeExercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Lessons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Kanjis",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "GrammarPatterns",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910001,
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910002,
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910003,
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910004,
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910005,
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910006,
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910007,
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910008,
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910009,
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910010,
                column: "ImageUrl",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Vocabularies");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "PracticeQuestions");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "PracticeQuestionOptions");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "PracticeExercises");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Kanjis");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "GrammarPatterns");
        }
    }
}
