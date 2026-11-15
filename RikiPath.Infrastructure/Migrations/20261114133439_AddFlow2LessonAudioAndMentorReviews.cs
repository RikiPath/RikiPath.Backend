using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    public partial class AddFlow2LessonAudioAndMentorReviews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The review table is owned by AddMentorReviewsAndDisputesPersistence.
            migrationBuilder.Sql("""ALTER TABLE "Lessons" ADD COLUMN IF NOT EXISTS "AudioUrl" text;""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AudioUrl", table: "Lessons");
        }
    }
}
