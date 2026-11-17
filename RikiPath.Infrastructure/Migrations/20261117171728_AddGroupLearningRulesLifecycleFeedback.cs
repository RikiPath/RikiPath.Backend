using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Thuần bổ sung: quy tắc khóa học v1 (10 tuần, 2/3/4 buổi, giữ chỗ 10 phút, tối thiểu 1 học viên) cho dòng chính sách cũ;
    // bảng yêu cầu dời/hủy lớp, đánh giá Mentor theo lớp, khiếu nại kèm lịch sử. Không xóa/sửa dữ liệu lịch sử.
    public partial class AddGroupLearningRulesLifecycleFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedSessionsPerWeekCsv",
                table: "MentorGroupPolicies",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "2,3,4");

            migrationBuilder.AddColumn<int>(
                name: "CheckoutHoldMinutes",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "CourseDurationWeeks",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "DefaultSessionsPerWeek",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "MinLearnersToStart",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "CohortComplaints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    CohortId = table.Column<int>(type: "integer", nullable: true),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RefundReviewRequested = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MentorResponse = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MentorRespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Decision = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DecidedById = table.Column<int>(type: "integer", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortComplaints", x => x.Id);
                    table.CheckConstraint("CK_CohortComplaint_Category", "\"Category\" IN ('TeachingQuality', 'MentorConduct', 'Schedule', 'PaymentEnrollment', 'Other')");
                    table.CheckConstraint("CK_CohortComplaint_Decision", "(\"Status\" NOT IN ('Resolved', 'Rejected') OR (\"Decision\" IS NOT NULL AND length(btrim(\"Decision\")) > 0 AND \"DecidedById\" IS NOT NULL AND \"DecidedAtUtc\" IS NOT NULL))");
                    table.CheckConstraint("CK_CohortComplaint_Message", "length(btrim(\"Message\")) BETWEEN 10 AND 2000");
                    table.CheckConstraint("CK_CohortComplaint_RefundReview", "NOT \"RefundReviewRequested\" OR \"Category\" = 'PaymentEnrollment'");
                    table.CheckConstraint("CK_CohortComplaint_Status", "\"Status\" IN ('Open', 'UnderReview', 'Resolved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_CohortComplaints_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortComplaints_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortComplaints_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortComplaints_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortComplaints_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortMentorReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    CohortId = table.Column<int>(type: "integer", nullable: true),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortMentorReviews", x => x.Id);
                    table.CheckConstraint("CK_CohortMentorReview_Rating", "\"Rating\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_CohortMentorReviews_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortMentorReviews_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortMentorReviews_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortMentorReviews_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortScheduleRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CohortId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ShiftWeeks = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedById = table.Column<int>(type: "integer", nullable: false),
                    RequestedByRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CohortRevisionSnapshot = table.Column<int>(type: "integer", nullable: false),
                    ImpactSnapshot = table.Column<string>(type: "text", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortScheduleRequests", x => x.Id);
                    table.CheckConstraint("CK_CohortScheduleRequest_Decision", "(\"Status\" NOT IN ('Approved', 'Rejected') OR (\"ReviewedById\" IS NOT NULL AND \"ReviewedAtUtc\" IS NOT NULL)) AND (\"Status\" <> 'Rejected' OR (\"DecisionNote\" IS NOT NULL AND length(btrim(\"DecisionNote\")) > 0))");
                    table.CheckConstraint("CK_CohortScheduleRequest_Kind", "\"Kind\" IN ('ShiftWeeks', 'CancelEmptyCohort')");
                    table.CheckConstraint("CK_CohortScheduleRequest_Reason", "length(btrim(\"Reason\")) BETWEEN 10 AND 1000");
                    table.CheckConstraint("CK_CohortScheduleRequest_ShiftWeeks", "(\"Kind\" = 'ShiftWeeks' AND \"ShiftWeeks\" BETWEEN 1 AND 8) OR (\"Kind\" = 'CancelEmptyCohort' AND \"ShiftWeeks\" IS NULL)");
                    table.CheckConstraint("CK_CohortScheduleRequest_Status", "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Withdrawn')");
                    table.ForeignKey(
                        name: "FK_CohortScheduleRequests_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortScheduleRequests_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortScheduleRequests_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortComplaintEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComplaintId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortComplaintEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CohortComplaintEvents_CohortComplaints_ComplaintId",
                        column: x => x.ComplaintId,
                        principalTable: "CohortComplaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortScheduleRequestEvents",
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
                    table.PrimaryKey("PK_CohortScheduleRequestEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CohortScheduleRequestEvents_CohortScheduleRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "CohortScheduleRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "MentorGroupPolicies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AllowedSessionsPerWeekCsv", "CheckoutHoldMinutes", "CourseDurationWeeks", "DefaultSessionsPerWeek", "MinLearnersToStart" },
                values: new object[] { "2,3,4", 10, 10, 3, 1 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_AllowedSessionsPerWeek",
                table: "MentorGroupPolicies",
                sql: "\"AllowedSessionsPerWeekCsv\" ~ '^[1-7](,[1-7]){0,6}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_CheckoutHold",
                table: "MentorGroupPolicies",
                sql: "\"CheckoutHoldMinutes\" BETWEEN 1 AND 60");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_CourseDurationWeeks",
                table: "MentorGroupPolicies",
                sql: "\"CourseDurationWeeks\" BETWEEN 1 AND 52");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_DefaultSessionsPerWeek",
                table: "MentorGroupPolicies",
                sql: "\"DefaultSessionsPerWeek\" BETWEEN 1 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_MinLearners",
                table: "MentorGroupPolicies",
                sql: "\"MinLearnersToStart\" BETWEEN 1 AND \"MaxLearners\"");

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaintEvents_ComplaintId_Id",
                table: "CohortComplaintEvents",
                columns: new[] { "ComplaintId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_CohortId",
                table: "CohortComplaints",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_CourseId",
                table: "CohortComplaints",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_DecidedById",
                table: "CohortComplaints",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_LearnerId_IdempotencyKey",
                table: "CohortComplaints",
                columns: new[] { "LearnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_MentorId_Status_CreatedAtUtc",
                table: "CohortComplaints",
                columns: new[] { "MentorId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_Status_CreatedAtUtc",
                table: "CohortComplaints",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortMentorReviews_CohortId",
                table: "CohortMentorReviews",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortMentorReviews_CourseId_MentorId",
                table: "CohortMentorReviews",
                columns: new[] { "CourseId", "MentorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortMentorReviews_LearnerId",
                table: "CohortMentorReviews",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortMentorReviews_MentorId_IsHidden_CreatedAtUtc",
                table: "CohortMentorReviews",
                columns: new[] { "MentorId", "IsHidden", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequestEvents_RequestId_Id",
                table: "CohortScheduleRequestEvents",
                columns: new[] { "RequestId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequests_CohortId_Id",
                table: "CohortScheduleRequests",
                columns: new[] { "CohortId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequests_PendingCohort",
                table: "CohortScheduleRequests",
                column: "CohortId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequests_RequestedById_IdempotencyKey",
                table: "CohortScheduleRequests",
                columns: new[] { "RequestedById", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequests_ReviewedById",
                table: "CohortScheduleRequests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequests_Status_CreatedAtUtc",
                table: "CohortScheduleRequests",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CohortComplaintEvents");

            migrationBuilder.DropTable(
                name: "CohortMentorReviews");

            migrationBuilder.DropTable(
                name: "CohortScheduleRequestEvents");

            migrationBuilder.DropTable(
                name: "CohortComplaints");

            migrationBuilder.DropTable(
                name: "CohortScheduleRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_AllowedSessionsPerWeek",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_CheckoutHold",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_CourseDurationWeeks",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_DefaultSessionsPerWeek",
                table: "MentorGroupPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_MinLearners",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "AllowedSessionsPerWeekCsv",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "CheckoutHoldMinutes",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "CourseDurationWeeks",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "DefaultSessionsPerWeek",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "MinLearnersToStart",
                table: "MentorGroupPolicies");
        }
    }
}
