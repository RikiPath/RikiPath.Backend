using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCertificateFromPracticeExercise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PracticeExercises_CertificateLevels_CertificateLevelId",
                table: "PracticeExercises");

            migrationBuilder.DropIndex(
                name: "IX_PracticeExercises_CertificateLevelId",
                table: "PracticeExercises");

            migrationBuilder.DropColumn(
                name: "CertificateLevelId",
                table: "PracticeExercises");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CertificateLevelId",
                table: "PracticeExercises",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_CertificateLevelId",
                table: "PracticeExercises",
                column: "CertificateLevelId");

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeExercises_CertificateLevels_CertificateLevelId",
                table: "PracticeExercises",
                column: "CertificateLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
