using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKanaWritingPractice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KanaCharacters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Character = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Romaji = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StrokeCount = table.Column<int>(type: "integer", nullable: false),
                    StrokeOrderImageUrl = table.Column<string>(type: "text", nullable: true),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContentAuthorId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanaCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanaCharacters_Users_ContentAuthorId",
                        column: x => x.ContentAuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KanaWritingPracticeCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    KanaCharacterId = table.Column<int>(type: "integer", nullable: false),
                    EaseFactor = table.Column<double>(type: "double precision", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    Repetitions = table.Column<int>(type: "integer", nullable: false),
                    NextReviewDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanaWritingPracticeCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanaWritingPracticeCards_KanaCharacters_KanaCharacterId",
                        column: x => x.KanaCharacterId,
                        principalTable: "KanaCharacters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KanaWritingPracticeCards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KanaWritingPracticeHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KanaWritingPracticeCardId = table.Column<int>(type: "integer", nullable: false),
                    TotalMistakes = table.Column<int>(type: "integer", nullable: false),
                    Quality = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanaWritingPracticeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanaWritingPracticeHistories_KanaWritingPracticeCards_KanaW~",
                        column: x => x.KanaWritingPracticeCardId,
                        principalTable: "KanaWritingPracticeCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KanaCharacters_Character_Type",
                table: "KanaCharacters",
                columns: new[] { "Character", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KanaCharacters_ContentAuthorId",
                table: "KanaCharacters",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_KanaWritingPracticeCards_KanaCharacterId",
                table: "KanaWritingPracticeCards",
                column: "KanaCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_KanaWritingPracticeCards_UserId_KanaCharacterId",
                table: "KanaWritingPracticeCards",
                columns: new[] { "UserId", "KanaCharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KanaWritingPracticeCards_UserId_NextReviewDate",
                table: "KanaWritingPracticeCards",
                columns: new[] { "UserId", "NextReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_KanaWritingPracticeHistories_KanaWritingPracticeCardId",
                table: "KanaWritingPracticeHistories",
                column: "KanaWritingPracticeCardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KanaWritingPracticeHistories");

            migrationBuilder.DropTable(
                name: "KanaWritingPracticeCards");

            migrationBuilder.DropTable(
                name: "KanaCharacters");
        }
    }
}
