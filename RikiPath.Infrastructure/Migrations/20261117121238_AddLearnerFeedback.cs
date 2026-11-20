using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLearnerFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearnerFeedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<int>(type: "integer", nullable: false),
                    TargetTitle = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerFeedback", x => x.Id);
                    table.CheckConstraint("CK_LearnerFeedback_Rating", "\"Rating\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_LearnerFeedback_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearnerFeedback_LearnerId_TargetType_TargetId",
                table: "LearnerFeedback",
                columns: new[] { "LearnerId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnerFeedback_UpdatedAtUtc_Id",
                table: "LearnerFeedback",
                columns: new[] { "UpdatedAtUtc", "Id" });

            migrationBuilder.Sql("""
                ALTER TABLE "LearnerFeedback" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "MentorReviews" ENABLE ROW LEVEL SECURITY;

                UPDATE "MentorReviews" SET "IsPublic" = FALSE WHERE "IsPublic";

                UPDATE "Notifications"
                SET "Title" = 'Đánh giá mentor', "Message" = 'Xem điểm trung bình trong trang đánh giá.',
                    "IsRead" = TRUE, "LinkUrl" = '/mentor-reviews/'
                WHERE "Type" = 'MentorReview' AND "Title" = 'Đánh giá mới từ học viên';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearnerFeedback");
        }
    }
}
