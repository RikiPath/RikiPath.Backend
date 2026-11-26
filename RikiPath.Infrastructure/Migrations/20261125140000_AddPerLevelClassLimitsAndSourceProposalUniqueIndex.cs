using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerLevelClassLimitsAndSourceProposalUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxTotalActiveClassesPerMentor",
                table: "MentorGroupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.UpdateData(
                table: "MentorGroupPolicies",
                keyColumn: "Id",
                keyValue: 1,
                column: "MaxTotalActiveClassesPerMentor",
                value: 4);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorGroupPolicy_MaxTotalActiveClasses",
                table: "MentorGroupPolicies",
                sql: "\"MaxTotalActiveClassesPerMentor\" BETWEEN 1 AND 50");

            migrationBuilder.CreateTable(
                name: "MentorGroupPolicyLevelLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorGroupPolicyId = table.Column<int>(type: "integer", nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    MaxActiveClasses = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorGroupPolicyLevelLimits", x => x.Id);
                    table.CheckConstraint("CK_MentorGroupPolicyLevelLimit_MaxActive", "\"MaxActiveClasses\" BETWEEN 0 AND 20");
                    table.ForeignKey(
                        name: "FK_MentorGroupPolicyLevelLimits_CertificateLevels_CertificateL~",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorGroupPolicyLevelLimits_MentorGroupPolicies_MentorGrou~",
                        column: x => x.MentorGroupPolicyId,
                        principalTable: "MentorGroupPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicyLevelLimits_CertificateLevelId",
                table: "MentorGroupPolicyLevelLimits",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicyLevelLimits_MentorGroupPolicyId_Certific~",
                table: "MentorGroupPolicyLevelLimits",
                columns: new[] { "MentorGroupPolicyId", "CertificateLevelId" },
                unique: true);

            // Backfill per-level concurrent class limits safely for all existing policy versions.
            migrationBuilder.Sql("""
                INSERT INTO "MentorGroupPolicyLevelLimits" ("MentorGroupPolicyId", "CertificateLevelId", "MaxActiveClasses")
                SELECT p."Id", l."Id", 1
                FROM "MentorGroupPolicies" p
                CROSS JOIN "CertificateLevels" l
                WHERE NOT l."IsDeleted"
                  AND UPPER(TRIM(l."Code")) IN ('N5', 'N4', 'N3', 'N2')
                ON CONFLICT ("MentorGroupPolicyId", "CertificateLevelId") DO NOTHING;
                """);

            // Non-destructive guard: fail loudly instead of deleting existing rows if duplicate active cohorts exist for the same proposal.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "StudyCohorts"
                        WHERE "SourceProposalId" IS NOT NULL
                          AND "Status" <> 'Cancelled'
                        GROUP BY "SourceProposalId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Phát hiện dữ liệu trùng SourceProposalId chưa hủy trong bảng StudyCohorts. Vui lòng rà soát thủ công trước khi áp dụng unique index.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_StudyCohorts_SourceProposalId",
                table: "StudyCohorts");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohorts_SourceProposalId",
                table: "StudyCohorts",
                column: "SourceProposalId",
                unique: true,
                filter: "\"SourceProposalId\" IS NOT NULL AND \"Status\" <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudyCohorts_SourceProposalId",
                table: "StudyCohorts");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohorts_SourceProposalId",
                table: "StudyCohorts",
                column: "SourceProposalId",
                filter: "\"SourceProposalId\" IS NOT NULL");

            migrationBuilder.DropTable(
                name: "MentorGroupPolicyLevelLimits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorGroupPolicy_MaxTotalActiveClasses",
                table: "MentorGroupPolicies");

            migrationBuilder.DropColumn(
                name: "MaxTotalActiveClassesPerMentor",
                table: "MentorGroupPolicies");
        }
    }
}
