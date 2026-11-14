using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyStudyMinutesRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyStudyMinutesMax",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyStudyMinutesMin",
                table: "Users",
                type: "integer",
                nullable: true);
















        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyStudyMinutesMax",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DailyStudyMinutesMin",
                table: "Users");







        }
    }
}
