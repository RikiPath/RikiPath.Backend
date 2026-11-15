using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RikiPath.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261111150000_AddLearnerPlanOffers")]
public sealed class AddLearnerPlanOffers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "SubscriptionPlans"
            SET "Price" = CASE WHEN "Id" <= 2 THEN 1000 WHEN "Id" <= 4 THEN 2000 ELSE 3000 END,
                "AiGradingQuota" = CASE "Id" WHEN 1 THEN 1 WHEN 2 THEN 2 WHEN 3 THEN 3 WHEN 4 THEN 5 WHEN 5 THEN 10 ELSE 15 END
            WHERE "Id" BETWEEN 1 AND 6;

            INSERT INTO "SubscriptionPlans"
              ("Id", "Name", "Description", "DurationDays", "Price", "AiGradingQuota",
               "MeetingSessionCount", "IsPopular", "IsTrial", "IsActive", "SortOrder", "IsDeleted")
            VALUES
              (8101, 'Luyện nói', 'Luyện nói trong 7 ngày, gồm 5 lượt chuyển giọng nói thành văn bản.', 7, 1000, 5, 0, false, false, true, 10, false),
              (8102, 'Luyện viết', 'Luyện viết trong 7 ngày, gồm 5 lượt AI chấm bài.', 7, 1000, 5, 0, false, false, true, 11, false),
              (8103, 'Mua 1 lượt AI luyện nói hoặc viết', 'Dùng cho một lượt chuyển giọng nói thành văn bản hoặc chấm bài viết.', 0, 1000, 1, 0, false, false, true, 12, false),
              (8104, 'Mua 1 lượt AI đề xuất lộ trình', 'Dùng cho một lần tạo hoặc xem trước lộ trình AI.', 0, 1000, 1, 0, false, false, true, 13, false),
              (8105, 'Ôn luyện 1-1 với Mentor', 'Một buổi ôn luyện với Mentor trong vòng 14 ngày.', 14, 1000, 0, 1, false, false, true, 14, false)
            ON CONFLICT ("Id") DO UPDATE SET "IsActive" = true;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "SubscriptionPlans"
            SET "Price" = CASE "Id" WHEN 1 THEN 79000 WHEN 2 THEN 119000 WHEN 3 THEN 159000
                WHEN 4 THEN 199000 WHEN 5 THEN 349000 ELSE 459000 END, "AiGradingQuota" = 0
            WHERE "Id" BETWEEN 1 AND 6;
            UPDATE "SubscriptionPlans" SET "IsActive" = false WHERE "Id" BETWEEN 8101 AND 8105;
            """);
    }
}

