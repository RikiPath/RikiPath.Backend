using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCohortEntitlementsAndRemediationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScanStorageUrl",
                table: "HomeworkSubmissions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MentorSupportPlan",
                table: "CourseRetakeRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MentorSupportPlanAtUtc",
                table: "CourseRetakeRequests",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MentorSupportPlanById",
                table: "CourseRetakeRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "CourseRetakeRequests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CohortEnrollmentEntitlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudyCohortId = table.Column<int>(type: "integer", nullable: false),
                    PlanId = table.Column<int>(type: "integer", nullable: true),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    EntitlementReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GrantedById = table.Column<int>(type: "integer", nullable: true),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConsumedByCourseId = table.Column<int>(type: "integer", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RevokedById = table.Column<int>(type: "integer", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortEnrollmentEntitlements", x => x.Id);
                    table.CheckConstraint("CK_CohortEnrollmentEntitlement_NoCredit", "\"SourceType\" NOT IN ('ConsultationCredit', 'Credit1on1', 'OneOnOneCredit')");
                    table.CheckConstraint("CK_CohortEnrollmentEntitlement_Status", "\"Status\" IN ('Active', 'Consumed', 'Revoked', 'Expired')");
                    table.ForeignKey(
                        name: "FK_CohortEnrollmentEntitlements_StudyCohorts_StudyCohortId",
                        column: x => x.StudyCohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortEnrollmentEntitlements_StudyCoursePlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "StudyCoursePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortEnrollmentEntitlements_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CohortEnrollmentEntitlements_EntitlementReference",
                table: "CohortEnrollmentEntitlements",
                column: "EntitlementReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortEnrollmentEntitlements_LearnerId",
                table: "CohortEnrollmentEntitlements",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortEnrollmentEntitlements_PlanId",
                table: "CohortEnrollmentEntitlements",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortEnrollmentEntitlements_StudyCohortId_LearnerId",
                table: "CohortEnrollmentEntitlements",
                columns: new[] { "StudyCohortId", "LearnerId" },
                unique: true,
                filter: "\"Status\" = 'Active' AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CohortEnrollmentEntitlements");

            migrationBuilder.DropColumn(
                name: "ScanStorageUrl",
                table: "HomeworkSubmissions");

            migrationBuilder.DropColumn(
                name: "MentorSupportPlan",
                table: "CourseRetakeRequests");

            migrationBuilder.DropColumn(
                name: "MentorSupportPlanAtUtc",
                table: "CourseRetakeRequests");

            migrationBuilder.DropColumn(
                name: "MentorSupportPlanById",
                table: "CourseRetakeRequests");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "CourseRetakeRequests");
        }
    }
}
