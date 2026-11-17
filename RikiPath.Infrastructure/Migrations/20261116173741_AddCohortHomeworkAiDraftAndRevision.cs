using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCohortHomeworkAiDraftAndRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRevisedAtUtc",
                table: "HomeworkSubmissions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionCount",
                table: "HomeworkSubmissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AiDraftFeedback",
                table: "HomeworkAssignments",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AiDraftGeneratedAtUtc",
                table: "HomeworkAssignments",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AiDraftScore",
                table: "HomeworkAssignments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiDraftUnavailableReason",
                table: "HomeworkAssignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentScope",
                table: "HomeworkAssignments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Booking1on1");

            migrationBuilder.AddColumn<Guid>(
                name: "CohortAssignmentGroupKey",
                table: "HomeworkAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CohortId",
                table: "HomeworkAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MentorFeedback",
                table: "HomeworkAssignments",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MentorScore",
                table: "HomeworkAssignments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Passed",
                table: "HomeworkAssignments",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublishedByMentorId",
                table: "HomeworkAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedFeedbackAtUtc",
                table: "HomeworkAssignments",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudyCourseId",
                table: "HomeworkAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_CohortId_LearnerId",
                table: "HomeworkAssignments",
                columns: new[] { "CohortId", "LearnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_StudyCourseId",
                table: "HomeworkAssignments",
                column: "StudyCourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_HomeworkAssignments_StudyCohorts_CohortId",
                table: "HomeworkAssignments",
                column: "CohortId",
                principalTable: "StudyCohorts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HomeworkAssignments_StudyCourses_StudyCourseId",
                table: "HomeworkAssignments",
                column: "StudyCourseId",
                principalTable: "StudyCourses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HomeworkAssignments_StudyCohorts_CohortId",
                table: "HomeworkAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_HomeworkAssignments_StudyCourses_StudyCourseId",
                table: "HomeworkAssignments");

            migrationBuilder.DropIndex(
                name: "IX_HomeworkAssignments_CohortId_LearnerId",
                table: "HomeworkAssignments");

            migrationBuilder.DropIndex(
                name: "IX_HomeworkAssignments_StudyCourseId",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "LastRevisedAtUtc",
                table: "HomeworkSubmissions");

            migrationBuilder.DropColumn(
                name: "RevisionCount",
                table: "HomeworkSubmissions");

            migrationBuilder.DropColumn(
                name: "AiDraftFeedback",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "AiDraftGeneratedAtUtc",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "AiDraftScore",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "AiDraftUnavailableReason",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "AssignmentScope",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "CohortAssignmentGroupKey",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "CohortId",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "MentorFeedback",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "MentorScore",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "Passed",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "PublishedByMentorId",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "PublishedFeedbackAtUtc",
                table: "HomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "StudyCourseId",
                table: "HomeworkAssignments");
        }
    }
}
