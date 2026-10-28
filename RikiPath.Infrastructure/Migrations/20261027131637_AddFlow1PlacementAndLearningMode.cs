using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlow1PlacementAndLearningMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssessedCertificateLevelId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LearningMode",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<string>(
                name: "PlacementDecision",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotTaken");

            migrationBuilder.AddColumn<int>(
                name: "StartingCertificateLevelId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseActivePathId",
                table: "RecommendedLearningPaths",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "RecommendedLearningPaths",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreviewExpiresAt",
                table: "RecommendedLearningPaths",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartingCertificateLevelId",
                table: "RecommendedLearningPaths",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "RecommendedLearningPaths",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Superseded");

            migrationBuilder.AddColumn<int>(
                name: "TargetCertificateLevelId",
                table: "RecommendedLearningPaths",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "RecommendedLearningPaths",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AttemptKind",
                table: "MockTestAttempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "MockTestAttempts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionSnapshotJson",
                table: "MockTestAttempts",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900001,
                columns: new[] { "AssessedCertificateLevelId", "LearningMode", "PlacementDecision", "StartingCertificateLevelId" },
                values: new object[] { null, "Standard", "NotTaken", null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900002,
                columns: new[] { "AssessedCertificateLevelId", "LearningMode", "PlacementDecision", "StartingCertificateLevelId" },
                values: new object[] { null, "Standard", "NotTaken", null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900003,
                columns: new[] { "AssessedCertificateLevelId", "LearningMode", "PlacementDecision", "StartingCertificateLevelId" },
                values: new object[] { null, "Standard", "NotTaken", null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900004,
                columns: new[] { "AssessedCertificateLevelId", "LearningMode", "PlacementDecision", "StartingCertificateLevelId" },
                values: new object[] { null, "Standard", "NotTaken", null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900005,
                columns: new[] { "AssessedCertificateLevelId", "LearningMode", "PlacementDecision", "StartingCertificateLevelId" },
                values: new object[] { null, "Standard", "NotTaken", null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900006,
                columns: new[] { "AssessedCertificateLevelId", "LearningMode", "PlacementDecision", "StartingCertificateLevelId" },
                values: new object[] { null, "Standard", "NotTaken", null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900007,
                columns: new[] { "AssessedCertificateLevelId", "LearningMode", "PlacementDecision", "StartingCertificateLevelId" },
                values: new object[] { null, "Standard", "NotTaken", null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900008,
                columns: new[] { "AssessedCertificateLevelId", "LearningMode", "PlacementDecision", "StartingCertificateLevelId" },
                values: new object[] { null, "Standard", "NotTaken", null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900009,
                columns: new[] { "AssessedCertificateLevelId", "LearningMode", "PlacementDecision", "StartingCertificateLevelId" },
                values: new object[] { null, "Standard", "NotTaken", null });

            // Preserve every pre-existing recommendation and deterministically mark
            // only the newest path for each learner as active. Legacy progress remains intact.
            migrationBuilder.Sql("""
                WITH ranked AS (
                  SELECT p."Id", p."UserId",
                         ROW_NUMBER() OVER (PARTITION BY p."UserId" ORDER BY p."GeneratedAt", p."Id") AS version,
                         ROW_NUMBER() OVER (PARTITION BY p."UserId" ORDER BY p."GeneratedAt" DESC, p."Id" DESC) AS newest
                  FROM "RecommendedLearningPaths" p
                )
                UPDATE "RecommendedLearningPaths" p
                SET "Version" = ranked.version::integer,
                    "Status" = CASE WHEN ranked.newest = 1 THEN 'Active' ELSE 'Superseded' END,
                    "ConfirmedAt" = p."GeneratedAt",
                    "StartingCertificateLevelId" = u."TargetCertificationLevelId",
                    "TargetCertificateLevelId" = u."TargetCertificationLevelId"
                FROM ranked JOIN "Users" u ON u."Id" = ranked."UserId"
                WHERE p."Id" = ranked."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AssessedCertificateLevelId",
                table: "Users",
                column: "AssessedCertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_StartingCertificateLevelId",
                table: "Users",
                column: "StartingCertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_RecommendedLearningPaths_UserId",
                table: "RecommendedLearningPaths",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_RecommendedLearningPaths_UserId_Version",
                table: "RecommendedLearningPaths",
                columns: new[] { "UserId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MockTestAttempts_UserId_AttemptKind",
                table: "MockTestAttempts",
                columns: new[] { "UserId", "AttemptKind" },
                unique: true,
                filter: "\"AttemptKind\" = 1 AND \"IsCompleted\" = false AND \"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_CertificateLevels_AssessedCertificateLevelId",
                table: "Users",
                column: "AssessedCertificateLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_CertificateLevels_StartingCertificateLevelId",
                table: "Users",
                column: "StartingCertificateLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "This Flow 1 migration is forward-only to preserve assessment and LearningPath history.");
        }
    }
}
