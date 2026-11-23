using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdaptiveRoadmapV2Metadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CertificateLevelId",
                table: "Topics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LearningObjectives",
                table: "Topics",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StageCode",
                table: "Topics",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "LessonId",
                table: "PracticeExercises",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "CertificateLevelId",
                table: "PracticeExercises",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PassingScorePercent",
                table: "PracticeExercises",
                type: "double precision",
                nullable: false,
                defaultValue: 70.0);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "PracticeExercises",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LessonQuickCheck");

            migrationBuilder.AddColumn<int>(
                name: "TopicId",
                table: "PracticeExercises",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinutes",
                table: "Lessons",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<string>(
                name: "LearningObjectives",
                table: "Lessons",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrerequisiteLessonId",
                table: "Lessons",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceProvenance",
                table: "Lessons",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "LanguageSkills",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "LanguageSkills",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "VOCABULARY");

            migrationBuilder.UpdateData(
                table: "LanguageSkills",
                keyColumn: "Id",
                keyValue: 2,
                column: "Code",
                value: "KANJI");

            migrationBuilder.UpdateData(
                table: "LanguageSkills",
                keyColumn: "Id",
                keyValue: 3,
                column: "Code",
                value: "GRAMMAR");

            migrationBuilder.UpdateData(
                table: "LanguageSkills",
                keyColumn: "Id",
                keyValue: 4,
                column: "Code",
                value: "LISTENING");

            migrationBuilder.UpdateData(
                table: "LanguageSkills",
                keyColumn: "Id",
                keyValue: 5,
                column: "Code",
                value: "READING");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_CertificateLevelId_SortOrder",
                table: "Topics",
                columns: new[] { "CertificateLevelId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_CertificateLevelId",
                table: "PracticeExercises",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_Scope_CertificateLevelId",
                table: "PracticeExercises",
                columns: new[] { "Scope", "CertificateLevelId" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_Scope_TopicId",
                table: "PracticeExercises",
                columns: new[] { "Scope", "TopicId" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_TopicId",
                table: "PracticeExercises",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_PrerequisiteLessonId",
                table: "Lessons",
                column: "PrerequisiteLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageSkills_Code",
                table: "LanguageSkills",
                column: "Code",
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_Lessons_PrerequisiteLessonId",
                table: "Lessons",
                column: "PrerequisiteLessonId",
                principalTable: "Lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeExercises_CertificateLevels_CertificateLevelId",
                table: "PracticeExercises",
                column: "CertificateLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeExercises_Topics_TopicId",
                table: "PracticeExercises",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_CertificateLevels_CertificateLevelId",
                table: "Topics",
                column: "CertificateLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_Lessons_PrerequisiteLessonId",
                table: "Lessons");

            migrationBuilder.DropForeignKey(
                name: "FK_PracticeExercises_CertificateLevels_CertificateLevelId",
                table: "PracticeExercises");

            migrationBuilder.DropForeignKey(
                name: "FK_PracticeExercises_Topics_TopicId",
                table: "PracticeExercises");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_CertificateLevels_CertificateLevelId",
                table: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_Topics_CertificateLevelId_SortOrder",
                table: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_PracticeExercises_CertificateLevelId",
                table: "PracticeExercises");

            migrationBuilder.DropIndex(
                name: "IX_PracticeExercises_Scope_CertificateLevelId",
                table: "PracticeExercises");

            migrationBuilder.DropIndex(
                name: "IX_PracticeExercises_Scope_TopicId",
                table: "PracticeExercises");

            migrationBuilder.DropIndex(
                name: "IX_PracticeExercises_TopicId",
                table: "PracticeExercises");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_PrerequisiteLessonId",
                table: "Lessons");

            migrationBuilder.DropIndex(
                name: "IX_LanguageSkills_Code",
                table: "LanguageSkills");

            migrationBuilder.DropColumn(
                name: "CertificateLevelId",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "LearningObjectives",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "StageCode",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "CertificateLevelId",
                table: "PracticeExercises");

            migrationBuilder.DropColumn(
                name: "PassingScorePercent",
                table: "PracticeExercises");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "PracticeExercises");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "PracticeExercises");

            migrationBuilder.DropColumn(
                name: "EstimatedMinutes",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "LearningObjectives",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "PrerequisiteLessonId",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "SourceProvenance",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "LanguageSkills");

            migrationBuilder.AlterColumn<int>(
                name: "LessonId",
                table: "PracticeExercises",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
