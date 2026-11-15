using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RikiPath.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261113160000_AddGoogleSubject")]
public sealed class AddGoogleSubject : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("GoogleSubject", "Users", "character varying(255)", maxLength: 255, nullable: true);
        migrationBuilder.CreateIndex("IX_Users_GoogleSubject", "Users", "GoogleSubject", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Users_GoogleSubject", "Users");
        migrationBuilder.DropColumn("GoogleSubject", "Users");
    }
}
