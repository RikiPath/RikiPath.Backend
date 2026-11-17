using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableGroupBookingSeatReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cùng một buổi được nhiều học viên giữ chỗ, nhưng hai buổi khác nhau của cùng cố vấn không được chồng giờ.
            migrationBuilder.Sql("""
                ALTER TABLE "MentorBookingReservations"
                    DROP CONSTRAINT IF EXISTS "EX_MentorBookingReservation_ActiveMentorRest";
                """);

            migrationBuilder.DropIndex(
                name: "IX_MentorBookingReservations_MentorAvailabilityId",
                table: "MentorBookingReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorBookingReservation_Rest",
                table: "MentorBookingReservations");

            migrationBuilder.AddColumn<int>(
                name: "LearnerId",
                table: "MentorBookings",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MentorBookings",
                keyColumn: "Id",
                keyValue: 990001,
                column: "LearnerId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MentorBookings",
                keyColumn: "Id",
                keyValue: 990002,
                column: "LearnerId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MentorBookings",
                keyColumn: "Id",
                keyValue: 990003,
                column: "LearnerId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MentorBookings",
                keyColumn: "Id",
                keyValue: 990004,
                column: "LearnerId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_MentorAvailabilityId_LearnerId",
                table: "MentorBookings",
                columns: new[] { "MentorAvailabilityId", "LearnerId" },
                unique: true,
                filter: "\"MentorAvailabilityId\" IS NOT NULL AND \"LearnerId\" IS NOT NULL AND NOT \"IsDeleted\" AND \"Status\" <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookingReservations_MentorAvailabilityId",
                table: "MentorBookingReservations",
                column: "MentorAvailabilityId",
                filter: "\"ReleasedAtUtc\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorBookingReservation_Rest",
                table: "MentorBookingReservations",
                sql: "\"RestProtectedUntilUtc\" >= \"EndUtc\"");

            migrationBuilder.Sql("""
                ALTER TABLE "MentorBookingReservations"
                    ADD CONSTRAINT "EX_MentorBookingReservation_ActiveMentorRest"
                    EXCLUDE USING gist (
                        "MentorId" WITH =,
                        "MentorAvailabilityId" WITH <>,
                        tstzrange("StartUtc", "RestProtectedUntilUtc", '[)') WITH &&
                    ) WHERE ("ReleasedAtUtc" IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "MentorBookingReservations"
                        WHERE "ReleasedAtUtc" IS NULL
                        GROUP BY "MentorAvailabilityId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot roll back group reservations while a session has multiple active learners.';
                    END IF;
                END $guard$;
                ALTER TABLE "MentorBookingReservations"
                    DROP CONSTRAINT IF EXISTS "EX_MentorBookingReservation_ActiveMentorRest";
                """);

            migrationBuilder.DropIndex(
                name: "IX_MentorBookings_MentorAvailabilityId_LearnerId",
                table: "MentorBookings");

            migrationBuilder.DropIndex(
                name: "IX_MentorBookingReservations_MentorAvailabilityId",
                table: "MentorBookingReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorBookingReservation_Rest",
                table: "MentorBookingReservations");

            migrationBuilder.DropColumn(
                name: "LearnerId",
                table: "MentorBookings");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookingReservations_MentorAvailabilityId",
                table: "MentorBookingReservations",
                column: "MentorAvailabilityId",
                unique: true,
                filter: "\"ReleasedAtUtc\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorBookingReservation_Rest",
                table: "MentorBookingReservations",
                sql: "\"RestProtectedUntilUtc\" = \"EndUtc\" + INTERVAL '15 minutes'");

            migrationBuilder.Sql("""
                ALTER TABLE "MentorBookingReservations"
                    ADD CONSTRAINT "EX_MentorBookingReservation_ActiveMentorRest"
                    EXCLUDE USING gist (
                        "MentorId" WITH =,
                        tstzrange("StartUtc", "RestProtectedUntilUtc", '[)') WITH &&
                    ) WHERE ("ReleasedAtUtc" IS NULL);
                """);
        }
    }
}
