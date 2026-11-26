using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistMockAnswerAiExplanation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AiExplanationGeneratedAtUtc",
                table: "MockTestAnswers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiExplanationInputJson",
                table: "MockTestAnswers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiExplanationJson",
                table: "MockTestAnswers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiExplanationVersion",
                table: "MockTestAnswers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiExplanationGeneratedAtUtc",
                table: "MockTestAnswers");

            migrationBuilder.DropColumn(
                name: "AiExplanationInputJson",
                table: "MockTestAnswers");

            migrationBuilder.DropColumn(
                name: "AiExplanationJson",
                table: "MockTestAnswers");

            migrationBuilder.DropColumn(
                name: "AiExplanationVersion",
                table: "MockTestAnswers");
        }
    }
}
