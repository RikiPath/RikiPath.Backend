using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorGroupPolicyVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MentorGroupPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaxLearners = table.Column<int>(type: "integer", nullable: false),
                    SessionMinutes = table.Column<int>(type: "integer", nullable: false),
                    RestMinutes = table.Column<int>(type: "integer", nullable: false),
                    DailySessionCap = table.Column<int>(type: "integer", nullable: false),
                    PublicationHorizonDays = table.Column<int>(type: "integer", nullable: false),
                    CancellationLeadHours = table.Column<int>(type: "integer", nullable: false),
                    ReschedulingLeadHours = table.Column<int>(type: "integer", nullable: false),
                    HomeworkDeadlineDays = table.Column<int>(type: "integer", nullable: false),
                    PlacementAttemptLimit = table.Column<int>(type: "integer", nullable: false),
                    RemediationCompletionThresholdPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    KpiReportingPeriodDays = table.Column<int>(type: "integer", nullable: false),
                    RecurringWeekdayPatternsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorGroupPolicies", x => x.Id);
                    table.CheckConstraint("CK_MentorGroupPolicy_Capacity", "\"MaxLearners\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_MentorGroupPolicy_Positive", "\"Version\" > 0 AND \"SessionMinutes\" > 0 AND \"RestMinutes\" >= 0 AND \"DailySessionCap\" > 0 AND \"PublicationHorizonDays\" > 0 AND \"CancellationLeadHours\" >= 0 AND \"ReschedulingLeadHours\" >= 0 AND \"HomeworkDeadlineDays\" > 0 AND \"PlacementAttemptLimit\" > 0 AND \"KpiReportingPeriodDays\" > 0");
                    table.CheckConstraint("CK_MentorGroupPolicy_Remediation", "\"RemediationCompletionThresholdPercent\" > 0 AND \"RemediationCompletionThresholdPercent\" <= 100");
                    table.ForeignKey(
                        name: "FK_MentorGroupPolicies_Users_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "MentorGroupPolicies",
                columns: new[] { "Id", "CancellationLeadHours", "CreatedAtUtc", "CreatedByAdminId", "DailySessionCap", "EffectiveFromUtc", "HomeworkDeadlineDays", "KpiReportingPeriodDays", "MaxLearners", "PlacementAttemptLimit", "PublicationHorizonDays", "RecurringWeekdayPatternsJson", "RemediationCompletionThresholdPercent", "ReschedulingLeadHours", "RestMinutes", "SessionMinutes", "Version" },
                values: new object[] { 1, 24, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 7, 30, 4, 2, 30, "[[1,3,5],[2,4,6]]", 90m, 24, 15, 90, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_CreatedByAdminId",
                table: "MentorGroupPolicies",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_EffectiveFromUtc",
                table: "MentorGroupPolicies",
                column: "EffectiveFromUtc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_Version",
                table: "MentorGroupPolicies",
                column: "Version",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MentorGroupPolicies");
        }
    }
}
