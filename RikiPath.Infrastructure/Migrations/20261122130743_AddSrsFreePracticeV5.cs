using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSrsFreePracticeV5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RetryCardIdsJson",
                table: "SrsReviewSessions",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "QueueCardIdsJson",
                table: "SrsReviewSessions",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "CompletedCardIdsJson",
                table: "SrsReviewSessions",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadlineAtUtc",
                table: "SrsReviewSessions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterLearningState",
                table: "SrsReviewSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessedStepKeysJson",
                table: "SrsReviewSessions",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "SelectionScope",
                table: "SrsReviewSessions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionKind",
                table: "SrsReviewSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Scheduled");

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleEnabled",
                table: "SrsReviewSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitMinutes",
                table: "SrsReviewSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SrsReviewSessions_UserId_SessionKind_Status_LastActivityAtU~",
                table: "SrsReviewSessions",
                columns: new[] { "UserId", "SessionKind", "Status", "LastActivityAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SrsReviewSessions_UserId_SessionKind_Status_LastActivityAtU~",
                table: "SrsReviewSessions");

            migrationBuilder.DropColumn(
                name: "DeadlineAtUtc",
                table: "SrsReviewSessions");

            migrationBuilder.DropColumn(
                name: "FilterLearningState",
                table: "SrsReviewSessions");

            migrationBuilder.DropColumn(
                name: "ProcessedStepKeysJson",
                table: "SrsReviewSessions");

            migrationBuilder.DropColumn(
                name: "SelectionScope",
                table: "SrsReviewSessions");

            migrationBuilder.DropColumn(
                name: "SessionKind",
                table: "SrsReviewSessions");

            migrationBuilder.DropColumn(
                name: "ShuffleEnabled",
                table: "SrsReviewSessions");

            migrationBuilder.DropColumn(
                name: "TimeLimitMinutes",
                table: "SrsReviewSessions");

            migrationBuilder.AlterColumn<string>(
                name: "RetryCardIdsJson",
                table: "SrsReviewSessions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8000)",
                oldMaxLength: 8000);

            migrationBuilder.AlterColumn<string>(
                name: "QueueCardIdsJson",
                table: "SrsReviewSessions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8000)",
                oldMaxLength: 8000);

            migrationBuilder.AlterColumn<string>(
                name: "CompletedCardIdsJson",
                table: "SrsReviewSessions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8000)",
                oldMaxLength: 8000);
        }
    }
}
