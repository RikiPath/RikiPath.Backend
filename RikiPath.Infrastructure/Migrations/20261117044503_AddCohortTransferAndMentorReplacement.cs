using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Thuần bổ sung (additive): thêm bảng/cột/chỉ mục mới; không xóa hay sửa dữ liệu lịch sử.
    // Dòng chính sách cũ nhận mặc định: cho phép chuyển lớp (cần Admin duyệt), không chuyển sau khai giảng, cho phép thay cố vấn, báo trước 24 giờ.
    public partial class AddCohortTransferAndMentorReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TransferredAtUtc",
                table: "StudyCourses",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferredFromCourseId",
                table: "StudyCourses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LearnerTransferEnabled",
                table: "MentorGroupPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MentorReplacementEnabled",
                table: "MentorGroupPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MentorReplacementLeadHours",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<bool>(
                name: "TransferAllowedAfterSourceStart",
                table: "MentorGroupPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CohortTransferRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    SourceCourseId = table.Column<int>(type: "integer", nullable: false),
                    SourceCohortId = table.Column<int>(type: "integer", nullable: false),
                    RequestedTargetCohortId = table.Column<int>(type: "integer", nullable: true),
                    TargetCohortId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<int>(type: "integer", nullable: true),
                    PolicyVersionSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DestinationCourseId = table.Column<int>(type: "integer", nullable: true),
                    EligibilitySnapshot = table.Column<string>(type: "text", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortTransferRequests", x => x.Id);
                    table.CheckConstraint("CK_CohortTransferRequest_Decision", "(\"Status\" <> 'Approved' OR (\"TargetCohortId\" IS NOT NULL AND \"DestinationCourseId\" IS NOT NULL AND \"ReviewedById\" IS NOT NULL)) AND (\"Status\" <> 'Rejected' OR (\"DecisionNote\" IS NOT NULL AND length(btrim(\"DecisionNote\")) > 0 AND \"ReviewedById\" IS NOT NULL))");
                    table.CheckConstraint("CK_CohortTransferRequest_Status", "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Withdrawn')");
                    table.CheckConstraint("CK_CohortTransferRequest_Target", "\"TargetCohortId\" IS NULL OR \"TargetCohortId\" <> \"SourceCohortId\"");
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_MentorGroupPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "MentorGroupPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_StudyCohorts_RequestedTargetCohortId",
                        column: x => x.RequestedTargetCohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_StudyCohorts_SourceCohortId",
                        column: x => x.SourceCohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_StudyCohorts_TargetCohortId",
                        column: x => x.TargetCohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_StudyCourses_DestinationCourseId",
                        column: x => x.DestinationCourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_StudyCourses_SourceCourseId",
                        column: x => x.SourceCourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyCohortMentorAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CohortId = table.Column<int>(type: "integer", nullable: false),
                    PreviousMentorId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromSessionNumber = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AffectedSessionCount = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AssignedById = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<int>(type: "integer", nullable: true),
                    PolicyVersionSnapshot = table.Column<int>(type: "integer", nullable: true),
                    ImpactSnapshot = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCohortMentorAssignments", x => x.Id);
                    table.CheckConstraint("CK_StudyCohortMentorAssignment_DifferentMentor", "\"MentorId\" <> \"PreviousMentorId\"");
                    table.CheckConstraint("CK_StudyCohortMentorAssignment_Reason", "length(btrim(\"Reason\")) > 0");
                    table.CheckConstraint("CK_StudyCohortMentorAssignment_Sessions", "\"EffectiveFromSessionNumber\" > 0 AND \"AffectedSessionCount\" > 0");
                    table.ForeignKey(
                        name: "FK_StudyCohortMentorAssignments_MentorGroupPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "MentorGroupPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohortMentorAssignments_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohortMentorAssignments_Users_AssignedById",
                        column: x => x.AssignedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohortMentorAssignments_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohortMentorAssignments_Users_PreviousMentorId",
                        column: x => x.PreviousMentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortTransferRequestEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortTransferRequestEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequestEvents_CohortTransferRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "CohortTransferRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "MentorGroupPolicies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LearnerTransferEnabled", "MentorReplacementEnabled", "MentorReplacementLeadHours", "TransferAllowedAfterSourceStart" },
                values: new object[] { true, true, 24, false });

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_TransferredFromCourseId",
                table: "StudyCourses",
                column: "TransferredFromCourseId",
                unique: true,
                filter: "\"TransferredFromCourseId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_ReplacementLead",
                table: "MentorGroupPolicies",
                sql: "\"MentorReplacementLeadHours\" BETWEEN 0 AND 720");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_TransferAfterStart",
                table: "MentorGroupPolicies",
                sql: "\"LearnerTransferEnabled\" OR NOT \"TransferAllowedAfterSourceStart\"");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequestEvents_RequestId_Id",
                table: "CohortTransferRequestEvents",
                columns: new[] { "RequestId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_DestinationCourseId",
                table: "CohortTransferRequests",
                column: "DestinationCourseId",
                unique: true,
                filter: "\"DestinationCourseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_LearnerId_IdempotencyKey",
                table: "CohortTransferRequests",
                columns: new[] { "LearnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_PendingSource",
                table: "CohortTransferRequests",
                column: "SourceCourseId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_PolicyId",
                table: "CohortTransferRequests",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_RequestedTargetCohortId",
                table: "CohortTransferRequests",
                column: "RequestedTargetCohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_ReviewedById",
                table: "CohortTransferRequests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_SourceCohortId",
                table: "CohortTransferRequests",
                column: "SourceCohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_Status_CreatedAtUtc",
                table: "CohortTransferRequests",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_TargetCohortId",
                table: "CohortTransferRequests",
                column: "TargetCohortId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortMentorAssignments_AssignedById",
                table: "StudyCohortMentorAssignments",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortMentorAssignments_CohortId_Id",
                table: "StudyCohortMentorAssignments",
                columns: new[] { "CohortId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortMentorAssignments_IdempotencyKey",
                table: "StudyCohortMentorAssignments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortMentorAssignments_MentorId",
                table: "StudyCohortMentorAssignments",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortMentorAssignments_PolicyId",
                table: "StudyCohortMentorAssignments",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortMentorAssignments_PreviousMentorId",
                table: "StudyCohortMentorAssignments",
                column: "PreviousMentorId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudyCourses_StudyCourses_TransferredFromCourseId",
                table: "StudyCourses",
                column: "TransferredFromCourseId",
                principalTable: "StudyCourses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudyCourses_StudyCourses_TransferredFromCourseId",
                table: "StudyCourses");

            migrationBuilder.DropTable(
                name: "CohortTransferRequestEvents");

            migrationBuilder.DropTable(
                name: "StudyCohortMentorAssignments");

            migrationBuilder.DropTable(
                name: "CohortTransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_StudyCourses_TransferredFromCourseId",
                table: "StudyCourses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_ReplacementLead",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_TransferAfterStart",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "TransferredAtUtc",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "TransferredFromCourseId",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "LearnerTransferEnabled",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "MentorReplacementEnabled",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "MentorReplacementLeadHours",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "TransferAllowedAfterSourceStart",
                table: "MentorGroupPolicies");
        }
    }
}
