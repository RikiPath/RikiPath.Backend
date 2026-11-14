using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RikiPath.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261113150000_AddMentorHomeworkAndAttendance")]
public sealed class AddMentorHomeworkAndAttendance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>("AttendedAt", "MentorBookings", "timestamp without time zone", nullable: true);
        migrationBuilder.AddColumn<bool>("AttendanceTrackingEnabled", "MentorAvailabilities", "boolean", defaultValue: true);
        migrationBuilder.Sql("""
            UPDATE "MentorAvailabilities"
            SET "AttendanceTrackingEnabled" = false
            WHERE "EndTime" <= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC');
            """);
        migrationBuilder.AddColumn<int>("MentorBookingId", "HomeworkAssignments", "integer", nullable: true);
        migrationBuilder.AddColumn<int>("HomeworkAssignmentId", "PracticeExerciseAttempts", "integer", nullable: true);
        migrationBuilder.AlterColumn<int>("LessonId", "HomeworkSubmissions", "integer", nullable: true,
            oldClrType: typeof(int), oldType: "integer");

        migrationBuilder.CreateTable(
            name: "HomeworkAssignmentExercises",
            columns: table => new
            {
                HomeworkAssignmentId = table.Column<int>(type: "integer", nullable: false),
                PracticeExerciseId = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HomeworkAssignmentExercises", item => new { item.HomeworkAssignmentId, item.PracticeExerciseId });
                table.ForeignKey("FK_HomeworkAssignmentExercises_HomeworkAssignments_HomeworkAss~", item => item.HomeworkAssignmentId,
                    "HomeworkAssignments", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_HomeworkAssignmentExercises_PracticeExercises_PracticeExerc~", item => item.PracticeExerciseId,
                    "PracticeExercises", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_HomeworkAssignmentExercises_PracticeExerciseId", "HomeworkAssignmentExercises", "PracticeExerciseId");
        migrationBuilder.CreateIndex("IX_HomeworkAssignments_MentorBookingId", "HomeworkAssignments", "MentorBookingId");
        migrationBuilder.CreateIndex("IX_PracticeExerciseAttempts_HomeworkAssignmentId_PracticeExerc~",
            "PracticeExerciseAttempts", new[] { "HomeworkAssignmentId", "PracticeExerciseId" }, unique: true);

        migrationBuilder.AddForeignKey("FK_HomeworkAssignments_MentorBookings_MentorBookingId", "HomeworkAssignments",
            "MentorBookingId", "MentorBookings", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_PracticeExerciseAttempts_HomeworkAssignments_HomeworkAssign~", "PracticeExerciseAttempts",
            "HomeworkAssignmentId", "HomeworkAssignments", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_HomeworkAssignments_MentorBookings_MentorBookingId", "HomeworkAssignments");
        migrationBuilder.DropForeignKey("FK_PracticeExerciseAttempts_HomeworkAssignments_HomeworkAssign~", "PracticeExerciseAttempts");
        migrationBuilder.DropTable("HomeworkAssignmentExercises");
        migrationBuilder.DropIndex("IX_HomeworkAssignments_MentorBookingId", "HomeworkAssignments");
        migrationBuilder.DropIndex("IX_PracticeExerciseAttempts_HomeworkAssignmentId_PracticeExerc~", "PracticeExerciseAttempts");
        migrationBuilder.DropColumn("AttendedAt", "MentorBookings");
        migrationBuilder.DropColumn("AttendanceTrackingEnabled", "MentorAvailabilities");
        migrationBuilder.DropColumn("MentorBookingId", "HomeworkAssignments");
        migrationBuilder.DropColumn("HomeworkAssignmentId", "PracticeExerciseAttempts");
        migrationBuilder.AlterColumn<int>("LessonId", "HomeworkSubmissions", "integer",
            oldClrType: typeof(int), oldType: "integer", oldNullable: true);
    }
}
