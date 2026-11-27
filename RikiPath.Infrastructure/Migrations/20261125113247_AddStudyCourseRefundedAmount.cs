using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyCourseRefundedAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "StudyCourses",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAtUtc",
                table: "StudyCourses",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"StudyCourses\" SET \"RefundedAmount\" = \"Amount\" WHERE \"PaymentStatus\" = 'Refunded' AND \"RefundedAmount\" = 0 AND \"Amount\" >= 0;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudyCourses_RefundedAmount",
                table: "StudyCourses",
                sql: "\"RefundedAmount\" >= 0 AND \"RefundedAmount\" <= \"Amount\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StudyCourses_RefundedAmount",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "RefundedAtUtc",
                table: "StudyCourses");
        }
    }
}
