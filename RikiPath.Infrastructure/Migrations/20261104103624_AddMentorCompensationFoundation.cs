using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorCompensationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompensationFormulaVersion",
                table: "MentorBookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompensationLevelId",
                table: "MentorBookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FrozenHourlyRate",
                table: "MentorBookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FrozenLevelCoefficient",
                table: "MentorBookings",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ScheduledCompensationAmount",
                table: "MentorBookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JlptLevelPayCoefficients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    Coefficient = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EffectiveUntilUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfiguredByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JlptLevelPayCoefficients", x => x.Id);
                    table.CheckConstraint("CK_JlptLevelPayCoefficient_Positive", "\"Coefficient\" > 0");
                    table.CheckConstraint("CK_JlptLevelPayCoefficient_Range", "\"EffectiveUntilUtc\" IS NULL OR \"EffectiveUntilUtc\" > \"EffectiveFromUtc\"");
                    table.ForeignKey(
                        name: "FK_JlptLevelPayCoefficients_CertificateLevels_CertificateLevel~",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JlptLevelPayCoefficients_Users_ConfiguredByAdminId",
                        column: x => x.ConfiguredByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorHourlyRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    AmountPerHour = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EffectiveUntilUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfiguredByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorHourlyRates", x => x.Id);
                    table.CheckConstraint("CK_MentorHourlyRate_Positive", "\"AmountPerHour\" > 0");
                    table.CheckConstraint("CK_MentorHourlyRate_Range", "\"EffectiveUntilUtc\" IS NULL OR \"EffectiveUntilUtc\" > \"EffectiveFromUtc\"");
                    table.ForeignKey(
                        name: "FK_MentorHourlyRates_Users_ConfiguredByAdminId",
                        column: x => x.ConfiguredByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorHourlyRates_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorTeachingQualifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorTeachingQualifications", x => x.Id);
                    table.CheckConstraint("CK_MentorTeachingQualification_Revoked", "\"RevokedAtUtc\" IS NULL OR \"RevokedAtUtc\" >= \"VerifiedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_MentorTeachingQualifications_CertificateLevels_CertificateL~",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorTeachingQualifications_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorTeachingQualifications_Users_VerifiedByAdminId",
                        column: x => x.VerifiedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_CompensationLevelId",
                table: "MentorBookings",
                column: "CompensationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_JlptLevelPayCoefficients_CertificateLevelId_EffectiveFromUtc",
                table: "JlptLevelPayCoefficients",
                columns: new[] { "CertificateLevelId", "EffectiveFromUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JlptLevelPayCoefficients_ConfiguredByAdminId",
                table: "JlptLevelPayCoefficients",
                column: "ConfiguredByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorHourlyRates_ConfiguredByAdminId",
                table: "MentorHourlyRates",
                column: "ConfiguredByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorHourlyRates_MentorUserId_EffectiveFromUtc",
                table: "MentorHourlyRates",
                columns: new[] { "MentorUserId", "EffectiveFromUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingQualifications_CertificateLevelId",
                table: "MentorTeachingQualifications",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingQualifications_MentorUserId_CertificateLevelId",
                table: "MentorTeachingQualifications",
                columns: new[] { "MentorUserId", "CertificateLevelId" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingQualifications_VerifiedByAdminId",
                table: "MentorTeachingQualifications",
                column: "VerifiedByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorBookings_CertificateLevels_CompensationLevelId",
                table: "MentorBookings",
                column: "CompensationLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorBookings_CertificateLevels_CompensationLevelId",
                table: "MentorBookings");

            migrationBuilder.DropTable(
                name: "JlptLevelPayCoefficients");

            migrationBuilder.DropTable(
                name: "MentorHourlyRates");

            migrationBuilder.DropTable(
                name: "MentorTeachingQualifications");

            migrationBuilder.DropIndex(
                name: "IX_MentorBookings_CompensationLevelId",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "CompensationFormulaVersion",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "CompensationLevelId",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "FrozenHourlyRate",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "FrozenLevelCoefficient",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "ScheduledCompensationAmount",
                table: "MentorBookings");
        }
    }
}
