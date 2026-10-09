using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentLevelMappingEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentLevelMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    KanjiId = table.Column<int>(type: "integer", nullable: true),
                    VocabularyId = table.Column<int>(type: "integer", nullable: true),
                    GrammarPatternId = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentLevelMappings", x => x.Id);
                    table.CheckConstraint("CK_ContentLevelMappings_ExactlyOneContent", "(CASE WHEN \"KanjiId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"VocabularyId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"GrammarPatternId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_ContentLevelMappings_CertificateLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentLevelMappings_GrammarPatterns_GrammarPatternId",
                        column: x => x.GrammarPatternId,
                        principalTable: "GrammarPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentLevelMappings_Kanjis_KanjiId",
                        column: x => x.KanjiId,
                        principalTable: "Kanjis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentLevelMappings_Vocabularies_VocabularyId",
                        column: x => x.VocabularyId,
                        principalTable: "Vocabularies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ContentLevelMappings",
                columns: new[] { "Id", "CertificationLevelId", "CreatedBy", "CreatedDate", "GrammarPatternId", "IsDeleted", "KanjiId", "ModifiedBy", "ModifiedDate", "VocabularyId" },
                values: new object[,]
                {
                    { 920001, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910001, null, null, null },
                    { 920002, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910002, null, null, null },
                    { 920003, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910003, null, null, null },
                    { 920004, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910004, null, null, null },
                    { 920005, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910005, null, null, null },
                    { 920006, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910006, null, null, null },
                    { 920007, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910007, null, null, null },
                    { 920008, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910008, null, null, null },
                    { 920009, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910009, null, null, null },
                    { 920010, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910010, null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_CertificationLevelId_GrammarPatternId",
                table: "ContentLevelMappings",
                columns: new[] { "CertificationLevelId", "GrammarPatternId" },
                unique: true,
                filter: "\"GrammarPatternId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_CertificationLevelId_KanjiId",
                table: "ContentLevelMappings",
                columns: new[] { "CertificationLevelId", "KanjiId" },
                unique: true,
                filter: "\"KanjiId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_CertificationLevelId_VocabularyId",
                table: "ContentLevelMappings",
                columns: new[] { "CertificationLevelId", "VocabularyId" },
                unique: true,
                filter: "\"VocabularyId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_GrammarPatternId",
                table: "ContentLevelMappings",
                column: "GrammarPatternId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_KanjiId",
                table: "ContentLevelMappings",
                column: "KanjiId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_VocabularyId",
                table: "ContentLevelMappings",
                column: "VocabularyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentLevelMappings");
        }
    }
}
