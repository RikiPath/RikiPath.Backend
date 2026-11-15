using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RikiPath.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261112100000_AddDeclaredJlptCertificate")]
public sealed class AddDeclaredJlptCertificate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "DeclaredJlptCertificateCode", table: "Users", type: "text", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "DeclaredJlptCertificateCode", table: "Users");
}
