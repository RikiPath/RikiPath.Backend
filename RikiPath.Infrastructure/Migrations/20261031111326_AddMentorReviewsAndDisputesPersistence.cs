using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    public partial class AddMentorReviewsAndDisputesPersistence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MentorAbuseReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReporterId = table.Column<int>(type: "integer", nullable: false),
                    TargetUserId = table.Column<int>(type: "integer", nullable: false),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AdminNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorAbuseReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorAbuseReports_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MentorAbuseReports_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorAbuseReports_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorDisputes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvidenceUrls = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MentorResponse = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MentorRespondedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AdminNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AdminDecision = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsRefundApproved = table.Column<bool>(type: "boolean", nullable: true),
                    CreditRefunded = table.Column<bool>(type: "boolean", nullable: false),
                    RefundOperationKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ResolvedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorDisputes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorDisputes_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorDisputes_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorDisputes_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorDisputes_Users_ResolvedByAdminId",
                        column: x => x.ResolvedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    ModerationStatus = table.Column<int>(type: "integer", nullable: false),
                    MentorReply = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MentorRepliedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorReviews", x => x.Id);
                    table.CheckConstraint("CK_MentorReview_Rating_Range", "\"Rating\" >= 1 AND \"Rating\" <= 5");
                    table.ForeignKey(
                        name: "FK_MentorReviews_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorReviews_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorReviews_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorDisputeHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisputeId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousStatus = table.Column<int>(type: "integer", nullable: true),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorDisputeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorDisputeHistories_MentorDisputes_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "MentorDisputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(
                name: "IX_MentorAbuseReports_MentorBookingId",
                table: "MentorAbuseReports",
                column: "MentorBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAbuseReports_ReporterId",
                table: "MentorAbuseReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAbuseReports_Status",
                table: "MentorAbuseReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAbuseReports_TargetUserId",
                table: "MentorAbuseReports",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputeHistories_DisputeId",
                table: "MentorDisputeHistories",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputes_LearnerId",
                table: "MentorDisputes",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputes_MentorBookingId",
                table: "MentorDisputes",
                column: "MentorBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputes_MentorId",
                table: "MentorDisputes",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputes_ResolvedByAdminId",
                table: "MentorDisputes",
                column: "ResolvedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputes_Status",
                table: "MentorDisputes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MentorReviews_LearnerId",
                table: "MentorReviews",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorReviews_Mentor_Public_Status",
                table: "MentorReviews",
                columns: new[] { "MentorId", "IsPublic", "ModerationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorReviews_MentorBookingId",
                table: "MentorReviews",
                column: "MentorBookingId",
                unique: true);
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MentorAbuseReports");

            migrationBuilder.DropTable(
                name: "MentorDisputeHistories");

            migrationBuilder.DropTable(
                name: "MentorReviews");

            migrationBuilder.DropTable(
                name: "MentorDisputes");        }
    }
}
