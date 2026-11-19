using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorAuthoredHomework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrivateAudioReference",
                table: "HomeworkSubmissions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedOptionsJson",
                table: "HomeworkSubmissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthoredDefinitionJson",
                table: "HomeworkAssignments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrivateAudioReference",
                table: "HomeworkSubmissions");

            migrationBuilder.DropColumn(
                name: "SelectedOptionsJson",
                table: "HomeworkSubmissions");

            migrationBuilder.DropColumn(
                name: "AuthoredDefinitionJson",
                table: "HomeworkAssignments");
        }
    }
}
