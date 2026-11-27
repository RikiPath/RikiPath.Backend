using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HideRejectedProposalFromMentorHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenFromMentorAtUtc",
                table: "MentorTeachingProposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHiddenFromMentorHistory",
                table: "MentorTeachingProposals",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HiddenFromMentorAtUtc",
                table: "MentorTeachingProposals");

            migrationBuilder.DropColumn(
                name: "IsHiddenFromMentorHistory",
                table: "MentorTeachingProposals");
        }
    }
}
