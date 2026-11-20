using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261116061024_RetireLegacyStudyCourseModel")]
    public partial class RetireLegacyStudyCourseModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retire the EF mappings while preserving the legacy tables and attendance history.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The physical schema is unchanged; the older EF model can still read it.
        }
    }
}
