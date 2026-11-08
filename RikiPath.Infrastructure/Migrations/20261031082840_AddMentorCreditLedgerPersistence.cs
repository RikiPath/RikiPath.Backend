using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorCreditLedgerPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MentorCreditGrants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    UserSubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    InitialQuantity = table.Column<int>(type: "integer", nullable: false),
                    AvailableQuantity = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorCreditGrants", x => x.Id);
                    table.CheckConstraint("CK_MentorCreditGrant_PolicyVersion", "\"PolicyVersion\" > 0");
                    table.CheckConstraint("CK_MentorCreditGrant_Quantity", "\"InitialQuantity\" > 0 AND \"AvailableQuantity\" >= 0 AND \"AvailableQuantity\" <= \"InitialQuantity\"");
                    table.ForeignKey(
                        name: "FK_MentorCreditGrants_UserSubscriptions_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorCreditGrants_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorCreditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditGrantId = table.Column<long>(type: "bigint", nullable: false),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    OperationKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorCreditEntries", x => x.Id);
                    table.CheckConstraint("CK_MentorCreditEntry_Amount", "\"Amount\" <> 0");
                    table.ForeignKey(
                        name: "FK_MentorCreditEntries_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorCreditEntries_MentorCreditGrants_CreditGrantId",
                        column: x => x.CreditGrantId,
                        principalTable: "MentorCreditGrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorCreditEntries_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorCreditEntries_CreditGrantId_EntryType",
                table: "MentorCreditEntries",
                columns: new[] { "CreditGrantId", "EntryType" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorCreditEntries_LearnerId",
                table: "MentorCreditEntries",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorCreditEntries_MentorBookingId_EntryType",
                table: "MentorCreditEntries",
                columns: new[] { "MentorBookingId", "EntryType" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorCreditEntries_OperationKey",
                table: "MentorCreditEntries",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorCreditGrants_LearnerId_ExpiresAtUtc",
                table: "MentorCreditGrants",
                columns: new[] { "LearnerId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorCreditGrants_UserSubscriptionId",
                table: "MentorCreditGrants",
                column: "UserSubscriptionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MentorCreditEntries");

            migrationBuilder.DropTable(
                name: "MentorCreditGrants");

            }
    }
}
