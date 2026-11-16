using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorBookingHomeworkProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MentorPracticeOutcomeJson",
                table: "UserSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MentorReviewCompletedAt",
                table: "UserSubscriptions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MentorReviewPlan",
                table: "UserSubscriptions",
                type: "character varying(6000)",
                maxLength: 6000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MentorReviewSessionAt",
                table: "UserSubscriptions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeFeedback",
                table: "MentorBookings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PracticeFeedbackAt",
                table: "MentorBookings",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PracticeScore",
                table: "MentorBookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateScanReference",
                table: "HomeworkSubmissions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MentorPracticeOutcomeJson",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "MentorReviewCompletedAt",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "MentorReviewPlan",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "MentorReviewSessionAt",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "PracticeFeedback",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "PracticeFeedbackAt",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "PracticeScore",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "PrivateScanReference",
                table: "HomeworkSubmissions");
        }
    }
}
