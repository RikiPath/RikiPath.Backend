using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations;

/// <summary>Adds only the learner goal fields; unrelated historical schema is untouched.</summary>
public partial class AddLearningGoalCertificateAndVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "LearningGoalVersion",
            table: "Users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "SelfDeclaredCertificateLevelId",
            table: "Users",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_SelfDeclaredCertificateLevelId",
            table: "Users",
            column: "SelfDeclaredCertificateLevelId");

        migrationBuilder.AddForeignKey(
            name: "FK_Users_CertificateLevels_SelfDeclaredCertificateLevelId",
            table: "Users",
            column: "SelfDeclaredCertificateLevelId",
            principalTable: "CertificateLevels",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Users_CertificateLevels_SelfDeclaredCertificateLevelId",
            table: "Users");

        migrationBuilder.DropIndex(
            name: "IX_Users_SelfDeclaredCertificateLevelId",
            table: "Users");

        migrationBuilder.DropColumn(name: "LearningGoalVersion", table: "Users");
        migrationBuilder.DropColumn(name: "SelfDeclaredCertificateLevelId", table: "Users");
    }
}
