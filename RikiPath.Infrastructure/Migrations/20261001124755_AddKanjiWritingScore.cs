using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKanjiWritingScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WritingCorrectStrokeCount",
                table: "ReviewCards",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WritingPracticeMode",
                table: "ReviewCards",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WritingScore",
                table: "ReviewCards",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WritingScoreUpdatedAt",
                table: "ReviewCards",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WritingTotalStrokeCount",
                table: "ReviewCards",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_UserId_KanjiEntryId_Mode",
                table: "ReviewCards",
                columns: new[] { "UserId", "KanjiEntryId", "Mode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReviewCards_UserId_KanjiEntryId_Mode",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "WritingCorrectStrokeCount",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "WritingPracticeMode",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "WritingScore",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "WritingScoreUpdatedAt",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "WritingTotalStrokeCount",
                table: "ReviewCards");
        }
    }
}
