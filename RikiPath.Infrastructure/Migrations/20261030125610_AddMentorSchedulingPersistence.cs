using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorSchedulingPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GenerationKey",
                table: "MentorAvailabilities",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LocalBusinessDate",
                table: "MentorAvailabilities",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MentorScheduleRuleId",
                table: "MentorAvailabilities",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PolicyVersion",
                table: "MentorAvailabilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MentorAvailabilities_Id_MentorId",
                table: "MentorAvailabilities",
                columns: new[] { "Id", "MentorId" });

            migrationBuilder.CreateTable(
                name: "MentorBookingDayGuards",
                columns: table => new
                {
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    LocalBusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActiveBookingCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorBookingDayGuards", x => new { x.MentorId, x.LocalBusinessDate });
                    table.CheckConstraint("CK_MentorBookingDayGuard_Count", "\"ActiveBookingCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_MentorBookingDayGuards_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorBookingIntents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    IntentKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorBookingIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorBookingIntents_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorBookingIntents_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorBookingReservations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    MentorAvailabilityId = table.Column<int>(type: "integer", nullable: false),
                    LocalBusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RestProtectedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorBookingReservations", x => x.Id);
                    table.CheckConstraint("CK_MentorBookingReservation_Interval", "\"EndUtc\" > \"StartUtc\"");
                    table.CheckConstraint("CK_MentorBookingReservation_PolicyVersion", "\"PolicyVersion\" > 0");
                    table.CheckConstraint("CK_MentorBookingReservation_Rest", "\"RestProtectedUntilUtc\" = \"EndUtc\" + INTERVAL '15 minutes'");
                    table.ForeignKey(
                        name: "FK_MentorBookingReservations_MentorAvailabilities_MentorAvaila~",
                        columns: x => new { x.MentorAvailabilityId, x.MentorId },
                        principalTable: "MentorAvailabilities",
                        principalColumns: new[] { "Id", "MentorId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorBookingReservations_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorBookingReservations_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorBookingTerms",
                columns: table => new
                {
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    MentorAvailabilityId = table.Column<int>(type: "integer", nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    ScheduledStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorBookingTerms", x => x.MentorBookingId);
                    table.CheckConstraint("CK_MentorBookingTerms_Interval", "\"ScheduledEndUtc\" > \"ScheduledStartUtc\"");
                    table.CheckConstraint("CK_MentorBookingTerms_PolicyVersion", "\"PolicyVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_MentorBookingTerms_MentorAvailabilities_MentorAvailabilityId",
                        column: x => x.MentorAvailabilityId,
                        principalTable: "MentorAvailabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorBookingTerms_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorOutboxEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AggregateId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AvailableAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorOutboxEvents", x => x.Id);
                    table.CheckConstraint("CK_MentorOutbox_Attempts", "\"Attempts\" >= 0");
                    table.CheckConstraint("CK_MentorOutbox_EventVersion", "\"EventVersion\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "MentorSchedules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AutoPublish = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    GeneratedThrough = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorSchedules", x => x.Id);
                    table.CheckConstraint("CK_MentorSchedule_PolicyVersion", "\"PolicyVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_MentorSchedules_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorScheduleRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduleId = table.Column<long>(type: "bigint", nullable: false),
                    Weekday = table.Column<int>(type: "integer", nullable: false),
                    DaypartCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LocalStart = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    LocalEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorScheduleRules", x => x.Id);
                    table.CheckConstraint("CK_MentorScheduleRule_Daypart", "\"DaypartCode\" IN ('Morning', 'Noon', 'Afternoon', 'Evening')");
                    table.CheckConstraint("CK_MentorScheduleRule_Time", "\"LocalEnd\" > \"LocalStart\"");
                    table.CheckConstraint("CK_MentorScheduleRule_Weekday", "\"Weekday\" BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_MentorScheduleRules_MentorSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "MentorSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_GenerationKey",
                table: "MentorAvailabilities",
                column: "GenerationKey",
                unique: true,
                filter: "\"GenerationKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_MentorScheduleRuleId",
                table: "MentorAvailabilities",
                column: "MentorScheduleRuleId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities",
                sql: "(\"GenerationKey\" IS NULL AND \"PolicyVersion\" IS NULL AND \"MentorScheduleRuleId\" IS NULL AND \"LocalBusinessDate\" IS NULL) OR (\"GenerationKey\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"PolicyVersion\" > 0 AND \"MentorScheduleRuleId\" IS NOT NULL AND \"LocalBusinessDate\" IS NOT NULL AND \"MaxLearners\" = 1 AND \"EndTime\" = \"StartTime\" + INTERVAL '90 minutes')");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookingIntents_LearnerId_IntentKey",
                table: "MentorBookingIntents",
                columns: new[] { "LearnerId", "IntentKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookingIntents_MentorBookingId",
                table: "MentorBookingIntents",
                column: "MentorBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookingReservations_MentorAvailabilityId",
                table: "MentorBookingReservations",
                column: "MentorAvailabilityId",
                unique: true,
                filter: "\"ReleasedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookingReservations_MentorAvailabilityId_MentorId",
                table: "MentorBookingReservations",
                columns: new[] { "MentorAvailabilityId", "MentorId" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookingReservations_MentorBookingId",
                table: "MentorBookingReservations",
                column: "MentorBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookingReservations_MentorId_LocalBusinessDate_Releas~",
                table: "MentorBookingReservations",
                columns: new[] { "MentorId", "LocalBusinessDate", "ReleasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookingTerms_MentorAvailabilityId",
                table: "MentorBookingTerms",
                column: "MentorAvailabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorOutboxEvents_DeliveredAtUtc_AvailableAtUtc_CreatedAtU~",
                table: "MentorOutboxEvents",
                columns: new[] { "DeliveredAtUtc", "AvailableAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorOutboxEvents_EventKey",
                table: "MentorOutboxEvents",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorScheduleRules_ScheduleId_Weekday_DaypartCode_Effectiv~",
                table: "MentorScheduleRules",
                columns: new[] { "ScheduleId", "Weekday", "DaypartCode", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorSchedules_MentorId_PolicyVersion",
                table: "MentorSchedules",
                columns: new[] { "MentorId", "PolicyVersion" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MentorAvailabilities_MentorScheduleRules_MentorScheduleRule~",
                table: "MentorAvailabilities",
                column: "MentorScheduleRuleId",
                principalTable: "MentorScheduleRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                DO $gist_block$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'btree_gist') THEN
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_constraint c
                            JOIN pg_namespace n ON n.oid = c.connamespace
                            WHERE n.nspname = 'public' AND c.conname = 'EX_MentorBookingReservation_ActiveMentorRest'
                        ) THEN
                            ALTER TABLE "MentorBookingReservations"
                                ADD CONSTRAINT "EX_MentorBookingReservation_ActiveMentorRest"
                                EXCLUDE USING gist (
                                    "MentorId" WITH =,
                                    tstzrange("StartUtc", "RestProtectedUntilUtc", '[)') WITH &&
                                ) WHERE ("ReleasedAtUtc" IS NULL);
                        END IF;
                    ELSE
                        RAISE EXCEPTION 'Prerequisite extension btree_gist is not installed. EX_MentorBookingReservation_ActiveMentorRest requires btree_gist for integer equality in GiST exclusion constraint. Migration AddMentorSchedulingPersistence aborted (FAIL-CLOSED).';
                    END IF;
                END $gist_block$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "MentorBookingReservations"
                    DROP CONSTRAINT IF EXISTS "EX_MentorBookingReservation_ActiveMentorRest";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MentorAvailabilities_MentorScheduleRules_MentorScheduleRule~",
                table: "MentorAvailabilities");

            migrationBuilder.DropTable(
                name: "MentorBookingDayGuards");

            migrationBuilder.DropTable(
                name: "MentorBookingIntents");

            migrationBuilder.DropTable(
                name: "MentorBookingReservations");

            migrationBuilder.DropTable(
                name: "MentorBookingTerms");

            migrationBuilder.DropTable(
                name: "MentorOutboxEvents");

            migrationBuilder.DropTable(
                name: "MentorScheduleRules");

            migrationBuilder.DropTable(
                name: "MentorSchedules");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MentorAvailabilities_Id_MentorId",
                table: "MentorAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_MentorAvailabilities_GenerationKey",
                table: "MentorAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_MentorAvailabilities_MentorScheduleRuleId",
                table: "MentorAvailabilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorAvailability_GeneratedSlot",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "GenerationKey",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "LocalBusinessDate",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "MentorScheduleRuleId",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "MentorAvailabilities");
        }
    }
}
