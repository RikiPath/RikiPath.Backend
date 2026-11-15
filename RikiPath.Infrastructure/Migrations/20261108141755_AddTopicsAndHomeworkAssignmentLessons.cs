using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicsAndHomeworkAssignmentLessons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HomeworkAssignments_Lessons_LessonId",
                table: "HomeworkAssignments");

            migrationBuilder.DropIndex(
                name: "IX_HomeworkAssignments_LessonId",
                table: "HomeworkAssignments");

            migrationBuilder.AddColumn<int>(
                name: "TopicId",
                table: "Lessons",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HomeworkAssignmentLessons",
                columns: table => new
                {
                    HomeworkAssignmentId = table.Column<int>(type: "integer", nullable: false),
                    LessonId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkAssignmentLessons", x => new { x.HomeworkAssignmentId, x.LessonId });
                    table.ForeignKey(
                        name: "FK_HomeworkAssignmentLessons_HomeworkAssignments_HomeworkAssig~",
                        column: x => x.HomeworkAssignmentId,
                        principalTable: "HomeworkAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkAssignmentLessons_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "HomeworkAssignmentLessons" ("HomeworkAssignmentId", "LessonId")
                SELECT "Id", "LessonId" FROM "HomeworkAssignments"
                WHERE "LessonId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "LessonId",
                table: "HomeworkAssignments");

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_TopicId",
                table: "Lessons",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignmentLessons_LessonId",
                table: "HomeworkAssignmentLessons",
                column: "LessonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_Topics_TopicId",
                table: "Lessons",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old schema cannot represent multiple lessons. Refuse a lossy rollback.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "HomeworkAssignmentLessons"
                        GROUP BY "HomeworkAssignmentId" HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot roll back: homework assignments have multiple lessons.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_Topics_TopicId",
                table: "Lessons");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_TopicId",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "Lessons");

            migrationBuilder.AddColumn<int>(
                name: "LessonId",
                table: "HomeworkAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "HomeworkAssignments" AS assignment
                SET "LessonId" = link."LessonId"
                FROM "HomeworkAssignmentLessons" AS link
                WHERE assignment."Id" = link."HomeworkAssignmentId";
                """);

            migrationBuilder.DropTable(
                name: "HomeworkAssignmentLessons");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_LessonId",
                table: "HomeworkAssignments",
                column: "LessonId");

            migrationBuilder.AddForeignKey(
                name: "FK_HomeworkAssignments_Lessons_LessonId",
                table: "HomeworkAssignments",
                column: "LessonId",
                principalTable: "Lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
