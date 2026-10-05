using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJapaneseDictionaryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JapaneseDictionaryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Surface = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReadingKana = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReadingRomaji = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Meaning = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PartOfSpeech = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsKatakana = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserAccountId = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JapaneseDictionaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JapaneseDictionaryEntries_Users_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JapaneseDictionaryEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JapaneseDictionaryEntries_ReadingKana_UserId",
                table: "JapaneseDictionaryEntries",
                columns: new[] { "ReadingKana", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_JapaneseDictionaryEntries_UserAccountId",
                table: "JapaneseDictionaryEntries",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JapaneseDictionaryEntries_UserId",
                table: "JapaneseDictionaryEntries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JapaneseDictionaryEntries");
        }
    }
}
