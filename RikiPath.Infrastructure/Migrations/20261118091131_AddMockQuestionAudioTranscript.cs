using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMockQuestionAudioTranscript : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioTranscript",
                table: "MockQuestions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioTranscript",
                table: "MockQuestions");
        }
    }
}
