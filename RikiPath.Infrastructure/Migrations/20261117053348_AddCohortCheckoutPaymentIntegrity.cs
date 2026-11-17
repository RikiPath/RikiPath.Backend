using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Thuần bổ sung: snapshot điều khoản đơn hàng, tiền tệ (dòng cũ = VND), tham chiếu giao dịch PayOS đã xác minh (unique), lý do chờ Admin xử lý.
    public partial class AddCohortCheckoutPaymentIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "StudyCourses",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<string>(
                name: "OrderSnapshot",
                table: "StudyCourses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentConfirmedAtUtc",
                table: "StudyCourses",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReviewReason",
                table: "StudyCourses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPaymentReference",
                table: "StudyCourses",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_ProviderPaymentReference",
                table: "StudyCourses",
                column: "ProviderPaymentReference",
                unique: true,
                filter: "\"ProviderPaymentReference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudyCourses_ProviderPaymentReference",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "OrderSnapshot",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "PaymentConfirmedAtUtc",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "PaymentReviewReason",
                table: "StudyCourses");

            migrationBuilder.DropColumn(
                name: "ProviderPaymentReference",
                table: "StudyCourses");
        }
    }
}
