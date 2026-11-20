using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminConfigAuditAndSubscriptionVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationDaysSnapshot",
                table: "UserSubscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanCategorySnapshot",
                table: "UserSubscriptions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanNameSnapshot",
                table: "UserSubscriptions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlanRevisionSnapshot",
                table: "UserSubscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanCategory",
                table: "SubscriptionPlans",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "self_study");

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "SubscriptionPlans",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "PlanCategory", "Revision" },
                values: new object[] { "self_study", 1 });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "PlanCategory", "Revision" },
                values: new object[] { "self_study", 1 });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "PlanCategory", "Revision" },
                values: new object[] { "self_study", 1 });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "PlanCategory", "Revision" },
                values: new object[] { "self_study", 1 });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "PlanCategory", "Revision" },
                values: new object[] { "self_study", 1 });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "PlanCategory", "Revision" },
                values: new object[] { "self_study", 1 });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "PlanCategory", "Revision" },
                values: new object[] { "mentor_legacy", 1 });

            migrationBuilder.UpdateData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990001,
                columns: new[] { "DurationDaysSnapshot", "PlanCategorySnapshot", "PlanNameSnapshot", "PlanRevisionSnapshot" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990002,
                columns: new[] { "DurationDaysSnapshot", "PlanCategorySnapshot", "PlanNameSnapshot", "PlanRevisionSnapshot" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990003,
                columns: new[] { "DurationDaysSnapshot", "PlanCategorySnapshot", "PlanNameSnapshot", "PlanRevisionSnapshot" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990004,
                columns: new[] { "DurationDaysSnapshot", "PlanCategorySnapshot", "PlanNameSnapshot", "PlanRevisionSnapshot" },
                values: new object[] { null, null, null, null });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubscriptionPlans_AiGradingQuota",
                table: "SubscriptionPlans",
                sql: "\"AiGradingQuota\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubscriptionPlans_DurationDays",
                table: "SubscriptionPlans",
                sql: "\"DurationDays\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubscriptionPlans_Price",
                table: "SubscriptionPlans",
                sql: "\"Price\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubscriptionPlans_SortOrder",
                table: "SubscriptionPlans",
                sql: "\"SortOrder\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_CreatedAtUtc",
                table: "AdminAuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_EntityType_EntityId",
                table: "AdminAuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.Sql("""
                UPDATE "SubscriptionPlans"
                SET "PlanCategory" = CASE "Id"
                    WHEN 8101 THEN 'speaking'
                    WHEN 8102 THEN 'writing'
                    WHEN 8103 THEN 'ai_grading'
                    WHEN 8104 THEN 'learning_path'
                    WHEN 8105 THEN 'mentor_legacy'
                    WHEN 7 THEN 'mentor_legacy'
                    ELSE CASE WHEN "MeetingSessionCount" > 0 THEN 'mentor_legacy' ELSE 'self_study' END
                END;

                SELECT setval(
                    pg_get_serial_sequence('"SubscriptionPlans"', 'Id'),
                    GREATEST(COALESCE((SELECT MAX("Id") FROM "SubscriptionPlans"), 1), 1),
                    true
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubscriptionPlans_AiGradingQuota",
                table: "SubscriptionPlans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubscriptionPlans_DurationDays",
                table: "SubscriptionPlans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubscriptionPlans_Price",
                table: "SubscriptionPlans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubscriptionPlans_SortOrder",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "DurationDaysSnapshot",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "PlanCategorySnapshot",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "PlanNameSnapshot",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "PlanRevisionSnapshot",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "PlanCategory",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "SubscriptionPlans");
        }
    }
}
