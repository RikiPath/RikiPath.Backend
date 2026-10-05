using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixJapaneseDictionaryUserRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JapaneseDictionaryEntries_Users_UserAccountId",
                table: "JapaneseDictionaryEntries");

            migrationBuilder.DropIndex(
                name: "IX_JapaneseDictionaryEntries_UserAccountId",
                table: "JapaneseDictionaryEntries");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "JapaneseDictionaryEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserAccountId",
                table: "JapaneseDictionaryEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JapaneseDictionaryEntries_UserAccountId",
                table: "JapaneseDictionaryEntries",
                column: "UserAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_JapaneseDictionaryEntries_Users_UserAccountId",
                table: "JapaneseDictionaryEntries",
                column: "UserAccountId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
