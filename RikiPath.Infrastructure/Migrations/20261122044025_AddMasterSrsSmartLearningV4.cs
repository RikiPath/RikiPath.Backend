using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterSrsSmartLearningV4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReviewHistories_ReviewItemId",
                table: "ReviewHistories");

            migrationBuilder.DropIndex(
                name: "IX_ReviewCards_UserId_KanjiEntryId_Mode",
                table: "ReviewCards");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Vocabularies",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Published");

            migrationBuilder.AddColumn<DateTime>(
                name: "UnpublishedAtUtc",
                table: "Vocabularies",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnpublishedReason",
                table: "Vocabularies",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Vocabularies",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "ReviewHistories",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrect",
                table: "ReviewHistories",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NewEaseFactor",
                table: "ReviewHistories",
                type: "double precision",
                nullable: false,
                defaultValue: 2.5);

            migrationBuilder.AddColumn<int>(
                name: "NewIntervalDays",
                table: "ReviewHistories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NewState",
                table: "ReviewHistories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Learning");

            migrationBuilder.AddColumn<double>(
                name: "PreviousEaseFactor",
                table: "ReviewHistories",
                type: "double precision",
                nullable: false,
                defaultValue: 2.5);

            migrationBuilder.AddColumn<int>(
                name: "PreviousIntervalDays",
                table: "ReviewHistories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PreviousState",
                table: "ReviewHistories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "New");

            migrationBuilder.AddColumn<int>(
                name: "ResponseTimeMs",
                table: "ReviewHistories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudyMode",
                table: "ReviewHistories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "flip");

            migrationBuilder.AddColumn<string>(
                name: "UserAnswer",
                table: "ReviewHistories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedReadingsJson",
                table: "ReviewCards",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "ReviewCards",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioUrl",
                table: "ReviewCards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackText",
                table: "ReviewCards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CardType",
                table: "ReviewCards",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Vocabulary");

            migrationBuilder.AddColumn<string>(
                name: "ContextSnippet",
                table: "ReviewCards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CorrectCount",
                table: "ReviewCards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExampleMeaning",
                table: "ReviewCards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExampleSentence",
                table: "ReviewCards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrontText",
                table: "ReviewCards",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "IncorrectCount",
                table: "ReviewCards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "JlptLevel",
                table: "ReviewCards",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Lapses",
                table: "ReviewCards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LearningState",
                table: "ReviewCards",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "New");

            migrationBuilder.AddColumn<int>(
                name: "LessonId",
                table: "ReviewCards",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MasteredAt",
                table: "ReviewCards",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Meaning",
                table: "ReviewCards",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedContentKey",
                table: "ReviewCards",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PersonalNote",
                table: "ReviewCards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reading",
                table: "ReviewCards",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "ReviewCards",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SourceId",
                table: "ReviewCards",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceLabel",
                table: "ReviewCards",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceLinksJson",
                table: "ReviewCards",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "ReviewCards",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<int>(
                name: "SourceVersion",
                table: "ReviewCards",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SrsCardTemplateId",
                table: "ReviewCards",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ReviewCards",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "ReviewCards",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SynonymsJson",
                table: "ReviewCards",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VocabularyId",
                table: "ReviewCards",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Lessons",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Published");

            migrationBuilder.AddColumn<DateTime>(
                name: "UnpublishedAtUtc",
                table: "Lessons",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnpublishedReason",
                table: "Lessons",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Lessons",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "LessonProgresses",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstViewedAt",
                table: "LessonProgresses",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InteractionCount",
                table: "LessonProgresses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastInteractedAt",
                table: "LessonProgresses",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Kanjis",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Published");

            migrationBuilder.AddColumn<DateTime>(
                name: "UnpublishedAtUtc",
                table: "Kanjis",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnpublishedReason",
                table: "Kanjis",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Kanjis",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "GrammarPatterns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Published");

            migrationBuilder.AddColumn<DateTime>(
                name: "UnpublishedAtUtc",
                table: "GrammarPatterns",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnpublishedReason",
                table: "GrammarPatterns",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "GrammarPatterns",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "LearnerHighlights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TargetId = table.Column<int>(type: "integer", nullable: true),
                    TargetKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourcePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SourceTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SelectedText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AnchorPrefix = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AnchorSuffix = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartOffset = table.Column<int>(type: "integer", nullable: true),
                    EndOffset = table.Column<int>(type: "integer", nullable: true),
                    ContentVersionHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TranslationSnapshot = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LinkedReviewCardId = table.Column<int>(type: "integer", nullable: true),
                    LinkedNoteEntryId = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerHighlights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearnerHighlights_LearnerNoteEntries_LinkedNoteEntryId",
                        column: x => x.LinkedNoteEntryId,
                        principalTable: "LearnerNoteEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LearnerHighlights_ReviewCards_LinkedReviewCardId",
                        column: x => x.LinkedReviewCardId,
                        principalTable: "ReviewCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LearnerHighlights_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SrsCardTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CardType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FrontText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BackText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Reading = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Meaning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExampleSentence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExampleMeaning = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UsageNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AcceptedReadingsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SynonymsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AudioUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: true),
                    JlptLevel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    LessonId = table.Column<int>(type: "integer", nullable: true),
                    VocabularyId = table.Column<int>(type: "integer", nullable: true),
                    KanjiId = table.Column<int>(type: "integer", nullable: true),
                    GrammarPatternId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    SubmittedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UnpublishedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UnpublishedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NormalizedContentKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SrsCardTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SrsCardTemplates_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SrsCardTemplates_GrammarPatterns_GrammarPatternId",
                        column: x => x.GrammarPatternId,
                        principalTable: "GrammarPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SrsCardTemplates_Kanjis_KanjiId",
                        column: x => x.KanjiId,
                        principalTable: "Kanjis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SrsCardTemplates_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SrsCardTemplates_Users_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SrsCardTemplates_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SrsCardTemplates_Vocabularies_VocabularyId",
                        column: x => x.VocabularyId,
                        principalTable: "Vocabularies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserSrsPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AutoEnrollAfterLesson = table.Column<bool>(type: "boolean", nullable: false),
                    DailyNewLimit = table.Column<int>(type: "integer", nullable: false),
                    DailyReviewLimit = table.Column<int>(type: "integer", nullable: false),
                    PreferredReviewMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AutoPlayAudio = table.Column<bool>(type: "boolean", nullable: false),
                    ShowFuriganaHint = table.Column<bool>(type: "boolean", nullable: false),
                    DailyReminderEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReminderHour = table.Column<int>(type: "integer", nullable: false),
                    ReminderMinute = table.Column<int>(type: "integer", nullable: false),
                    ReminderFrequency = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReminderDaysJson = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmailReminderEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TimezoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PreferredLocale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LastReminderSentAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastReminderSentLocalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSrsPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSrsPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910001,
                columns: new[] { "Status", "UnpublishedAtUtc", "UnpublishedReason", "Version" },
                values: new object[] { "Published", null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910002,
                columns: new[] { "Status", "UnpublishedAtUtc", "UnpublishedReason", "Version" },
                values: new object[] { "Published", null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910003,
                columns: new[] { "Status", "UnpublishedAtUtc", "UnpublishedReason", "Version" },
                values: new object[] { "Published", null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910004,
                columns: new[] { "Status", "UnpublishedAtUtc", "UnpublishedReason", "Version" },
                values: new object[] { "Published", null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910005,
                columns: new[] { "Status", "UnpublishedAtUtc", "UnpublishedReason", "Version" },
                values: new object[] { "Published", null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910006,
                columns: new[] { "Status", "UnpublishedAtUtc", "UnpublishedReason", "Version" },
                values: new object[] { "Published", null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910007,
                columns: new[] { "Status", "UnpublishedAtUtc", "UnpublishedReason", "Version" },
                values: new object[] { "Published", null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910008,
                columns: new[] { "Status", "UnpublishedAtUtc", "UnpublishedReason", "Version" },
                values: new object[] { "Published", null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910009,
                columns: new[] { "Status", "UnpublishedAtUtc", "UnpublishedReason", "Version" },
                values: new object[] { "Published", null, null, 1 });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910010,
                columns: new[] { "Status", "UnpublishedAtUtc", "UnpublishedReason", "Version" },
                values: new object[] { "Published", null, null, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_Vocabularies_Status",
                table: "Vocabularies",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHistories_ReviewItemId_IdempotencyKey",
                table: "ReviewHistories",
                columns: new[] { "ReviewItemId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHistories_ReviewItemId_ReviewedAt",
                table: "ReviewHistories",
                columns: new[] { "ReviewItemId", "ReviewedAt" });

            migrationBuilder.Sql("""
                -- Deduplicate any legacy duplicate ReviewCards before creating strict filtered unique indexes.
                WITH ranked_grammar AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "UserId", "GrammarPointId", "Mode"
                               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
                           ) AS keep_id
                    FROM "ReviewCards"
                    WHERE "GrammarPointId" IS NOT NULL
                )
                UPDATE "ReviewHistories" rh
                SET "ReviewItemId" = rg.keep_id
                FROM ranked_grammar rg
                WHERE rh."ReviewItemId" = rg."Id" AND rg."Id" <> rg.keep_id;

                WITH ranked_grammar AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "UserId", "GrammarPointId", "Mode"
                               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
                           ) AS keep_id
                    FROM "ReviewCards"
                    WHERE "GrammarPointId" IS NOT NULL
                )
                DELETE FROM "ReviewCards" rc
                USING ranked_grammar rg
                WHERE rc."Id" = rg."Id" AND rg."Id" <> rg.keep_id;

                -- Backfill VocabularyId and KanjiEntryId from linked LearnerNoteEntries when absent.
                UPDATE "ReviewCards" rc
                SET "VocabularyId" = COALESCE(rc."VocabularyId", lne."VocabularyEntryId"),
                    "KanjiEntryId" = COALESCE(rc."KanjiEntryId", lne."KanjiEntryId"),
                    "CardType" = CASE
                        WHEN COALESCE(rc."KanjiEntryId", lne."KanjiEntryId") IS NOT NULL THEN 'Kanji'
                        WHEN rc."GrammarPointId" IS NOT NULL THEN 'Grammar'
                        ELSE 'Vocabulary'
                    END,
                    "SourceType" = CASE
                        WHEN rc."VocabularyNoteEntryId" IS NOT NULL THEN 'Notebook'
                        WHEN rc."KanjiEntryId" IS NOT NULL THEN 'Kanji'
                        WHEN rc."GrammarPointId" IS NOT NULL THEN 'Grammar'
                        ELSE 'Manual'
                    END
                FROM "LearnerNoteEntries" lne
                WHERE rc."VocabularyNoteEntryId" = lne."Id";

                UPDATE "ReviewCards" rc
                SET "CardType" = 'Kanji',
                    "SourceType" = CASE WHEN rc."SourceType" = 'Manual' THEN 'Kanji' ELSE rc."SourceType" END
                WHERE rc."VocabularyNoteEntryId" IS NULL AND rc."KanjiEntryId" IS NOT NULL;

                UPDATE "ReviewCards" rc
                SET "CardType" = 'Grammar',
                    "SourceType" = CASE WHEN rc."SourceType" = 'Manual' THEN 'Grammar' ELSE rc."SourceType" END
                WHERE rc."VocabularyNoteEntryId" IS NULL AND rc."GrammarPointId" IS NOT NULL;

                -- Backfill snapshot FrontText/BackText/Reading/Meaning from canonical content banks & notes.
                UPDATE "ReviewCards" rc
                SET "FrontText" = v."Word",
                    "Reading" = v."Reading",
                    "Meaning" = v."Meaning",
                    "BackText" = v."Meaning",
                    "ExampleSentence" = v."ExampleSentence",
                    "ExampleMeaning" = v."ExampleSentenceMeaning",
                    "AudioUrl" = v."AudioUrl",
                    "NormalizedContentKey" = 'vocabulary:' || v."Id"::text
                FROM "Vocabularies" v
                WHERE rc."VocabularyId" = v."Id" AND rc."FrontText" = '';

                UPDATE "ReviewCards" rc
                SET "FrontText" = k."Character",
                    "Reading" = COALESCE(NULLIF(k."OnYomi", ''), NULLIF(k."KunYomi", ''), k."SinoVietnamese"),
                    "Meaning" = k."Meaning",
                    "BackText" = k."Meaning",
                    "AudioUrl" = k."AudioUrl",
                    "NormalizedContentKey" = 'kanji:' || k."Id"::text
                FROM "Kanjis" k
                WHERE rc."KanjiEntryId" = k."Id" AND rc."FrontText" = '';

                UPDATE "ReviewCards" rc
                SET "FrontText" = g."Title",
                    "Reading" = g."Structure",
                    "Meaning" = COALESCE(NULLIF(g."UsageNotes", ''), g."Structure"),
                    "BackText" = COALESCE(NULLIF(g."UsageNotes", ''), g."Structure"),
                    "ExampleSentence" = g."ExampleSentence",
                    "ExampleMeaning" = g."ExampleSentenceMeaning",
                    "NormalizedContentKey" = 'grammar:' || g."Id"::text
                FROM "GrammarPatterns" g
                WHERE rc."GrammarPointId" = g."Id" AND rc."FrontText" = '';

                UPDATE "ReviewCards" rc
                SET "FrontText" = COALESCE(NULLIF(lne."ManualWord", ''), 'Card #' || rc."Id"::text),
                    "Reading" = lne."ManualReading",
                    "Meaning" = lne."ManualMeaning",
                    "BackText" = COALESCE(NULLIF(lne."ManualMeaning", ''), NULLIF(lne."Note", ''), '-'),
                    "PersonalNote" = lne."Note",
                    "NormalizedContentKey" = 'noteentry:' || lne."Id"::text
                FROM "LearnerNoteEntries" lne
                WHERE rc."VocabularyNoteEntryId" = lne."Id" AND rc."FrontText" = '';

                UPDATE "ReviewCards"
                SET "LearningState" = CASE
                    WHEN "Repetitions" >= 3 AND "IntervalDays" >= 21 AND "EaseFactor" >= 2.2 THEN 'Mastered'
                    WHEN "Repetitions" >= 2 THEN 'Review'
                    WHEN "LastReviewedAt" IS NOT NULL THEN 'Learning'
                    ELSE 'New'
                END,
                "Status" = CASE WHEN "IsDeleted" THEN 'Archived' ELSE 'Active' END,
                "MasteredAt" = CASE WHEN "Repetitions" >= 3 AND "IntervalDays" >= 21 AND "EaseFactor" >= 2.2 THEN COALESCE("LastReviewedAt", NOW()) ELSE NULL END,
                "ArchivedAt" = CASE WHEN "IsDeleted" THEN COALESCE("ModifiedDate", NOW()) ELSE NULL END;

                UPDATE "LessonProgresses"
                SET "FirstViewedAt" = COALESCE("FirstViewedAt", "CreatedDate", "LastWatchedAt"),
                    "CompletedAt" = CASE WHEN "IsCompleted" THEN COALESCE("CompletedAt", "LastWatchedAt", "ModifiedDate", "CreatedDate") ELSE NULL END;

                -- Merge any duplicate VocabularyId / KanjiEntryId / NormalizedContentKey cards produced after backfilling from multiple note entries.
                WITH ranked_vocab AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "UserId", "VocabularyId", "Mode"
                               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
                           ) AS keep_id
                    FROM "ReviewCards"
                    WHERE "VocabularyId" IS NOT NULL
                )
                UPDATE "ReviewHistories" rh
                SET "ReviewItemId" = rv.keep_id
                FROM ranked_vocab rv
                WHERE rh."ReviewItemId" = rv."Id" AND rv."Id" <> rv.keep_id;

                WITH ranked_vocab AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "UserId", "VocabularyId", "Mode"
                               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
                           ) AS keep_id
                    FROM "ReviewCards"
                    WHERE "VocabularyId" IS NOT NULL
                )
                DELETE FROM "ReviewCards" rc
                USING ranked_vocab rv
                WHERE rc."Id" = rv."Id" AND rv."Id" <> rv.keep_id;

                WITH ranked_kanji AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "UserId", "KanjiEntryId", "Mode"
                               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
                           ) AS keep_id
                    FROM "ReviewCards"
                    WHERE "KanjiEntryId" IS NOT NULL
                )
                UPDATE "ReviewHistories" rh
                SET "ReviewItemId" = rk.keep_id
                FROM ranked_kanji rk
                WHERE rh."ReviewItemId" = rk."Id" AND rk."Id" <> rk.keep_id;

                WITH ranked_kanji AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "UserId", "KanjiEntryId", "Mode"
                               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
                           ) AS keep_id
                    FROM "ReviewCards"
                    WHERE "KanjiEntryId" IS NOT NULL
                )
                DELETE FROM "ReviewCards" rc
                USING ranked_kanji rk
                WHERE rc."Id" = rk."Id" AND rk."Id" <> rk.keep_id;

                WITH ranked_key AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "UserId", "NormalizedContentKey", "Mode"
                               ORDER BY "Repetitions" DESC, "Id" ASC
                           ) AS keep_id
                    FROM "ReviewCards"
                    WHERE "NormalizedContentKey" <> '' AND "IsDeleted" = false
                )
                UPDATE "ReviewCards" rc
                SET "IsDeleted" = true,
                    "Status" = 'Archived',
                    "ArchivedAt" = NOW()
                FROM ranked_key rk
                WHERE rc."Id" = rk."Id" AND rk."Id" <> rk.keep_id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_LessonId",
                table: "ReviewCards",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_SrsCardTemplateId",
                table: "ReviewCards",
                column: "SrsCardTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_UserId_GrammarPointId_Mode",
                table: "ReviewCards",
                columns: new[] { "UserId", "GrammarPointId", "Mode" },
                unique: true,
                filter: "\"GrammarPointId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_UserId_KanjiEntryId_Mode",
                table: "ReviewCards",
                columns: new[] { "UserId", "KanjiEntryId", "Mode" },
                unique: true,
                filter: "\"KanjiEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_UserId_NormalizedContentKey_Mode",
                table: "ReviewCards",
                columns: new[] { "UserId", "NormalizedContentKey", "Mode" },
                unique: true,
                filter: "\"NormalizedContentKey\" <> '' AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_UserId_Status_LearningState_NextReviewDate",
                table: "ReviewCards",
                columns: new[] { "UserId", "Status", "LearningState", "NextReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_UserId_VocabularyId_Mode",
                table: "ReviewCards",
                columns: new[] { "UserId", "VocabularyId", "Mode" },
                unique: true,
                filter: "\"VocabularyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_VocabularyId",
                table: "ReviewCards",
                column: "VocabularyId");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_Status",
                table: "Lessons",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Kanjis_Status",
                table: "Kanjis",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPatterns_Status",
                table: "GrammarPatterns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerHighlights_LinkedNoteEntryId",
                table: "LearnerHighlights",
                column: "LinkedNoteEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerHighlights_LinkedReviewCardId",
                table: "LearnerHighlights",
                column: "LinkedReviewCardId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerHighlights_UserId_CreatedDate",
                table: "LearnerHighlights",
                columns: new[] { "UserId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LearnerHighlights_UserId_TargetKey",
                table: "LearnerHighlights",
                columns: new[] { "UserId", "TargetKey" });

            migrationBuilder.CreateIndex(
                name: "IX_LearnerHighlights_UserId_TargetType_TargetId",
                table: "LearnerHighlights",
                columns: new[] { "UserId", "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_SrsCardTemplates_CertificateLevelId",
                table: "SrsCardTemplates",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsCardTemplates_GrammarPatternId",
                table: "SrsCardTemplates",
                column: "GrammarPatternId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsCardTemplates_KanjiId",
                table: "SrsCardTemplates",
                column: "KanjiId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsCardTemplates_LessonId",
                table: "SrsCardTemplates",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsCardTemplates_NormalizedContentKey",
                table: "SrsCardTemplates",
                column: "NormalizedContentKey");

            migrationBuilder.CreateIndex(
                name: "IX_SrsCardTemplates_ReviewedByAdminId",
                table: "SrsCardTemplates",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsCardTemplates_Status_JlptLevel_CardType",
                table: "SrsCardTemplates",
                columns: new[] { "Status", "JlptLevel", "CardType" });

            migrationBuilder.CreateIndex(
                name: "IX_SrsCardTemplates_SubmittedByUserId_Status",
                table: "SrsCardTemplates",
                columns: new[] { "SubmittedByUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SrsCardTemplates_VocabularyId",
                table: "SrsCardTemplates",
                column: "VocabularyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSrsPreferences_DailyReminderEnabled_LastReminderSentLoc~",
                table: "UserSrsPreferences",
                columns: new[] { "DailyReminderEnabled", "LastReminderSentLocalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSrsPreferences_UserId",
                table: "UserSrsPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewCards_Lessons_LessonId",
                table: "ReviewCards",
                column: "LessonId",
                principalTable: "Lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewCards_SrsCardTemplates_SrsCardTemplateId",
                table: "ReviewCards",
                column: "SrsCardTemplateId",
                principalTable: "SrsCardTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewCards_Vocabularies_VocabularyId",
                table: "ReviewCards",
                column: "VocabularyId",
                principalTable: "Vocabularies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewCards_Lessons_LessonId",
                table: "ReviewCards");

            migrationBuilder.DropForeignKey(
                name: "FK_ReviewCards_SrsCardTemplates_SrsCardTemplateId",
                table: "ReviewCards");

            migrationBuilder.DropForeignKey(
                name: "FK_ReviewCards_Vocabularies_VocabularyId",
                table: "ReviewCards");

            migrationBuilder.DropTable(
                name: "LearnerHighlights");

            migrationBuilder.DropTable(
                name: "SrsCardTemplates");

            migrationBuilder.DropTable(
                name: "UserSrsPreferences");

            migrationBuilder.DropIndex(
                name: "IX_Vocabularies_Status",
                table: "Vocabularies");

            migrationBuilder.DropIndex(
                name: "IX_ReviewHistories_ReviewItemId_IdempotencyKey",
                table: "ReviewHistories");

            migrationBuilder.DropIndex(
                name: "IX_ReviewHistories_ReviewItemId_ReviewedAt",
                table: "ReviewHistories");

            migrationBuilder.DropIndex(
                name: "IX_ReviewCards_LessonId",
                table: "ReviewCards");

            migrationBuilder.DropIndex(
                name: "IX_ReviewCards_SrsCardTemplateId",
                table: "ReviewCards");

            migrationBuilder.DropIndex(
                name: "IX_ReviewCards_UserId_GrammarPointId_Mode",
                table: "ReviewCards");

            migrationBuilder.DropIndex(
                name: "IX_ReviewCards_UserId_KanjiEntryId_Mode",
                table: "ReviewCards");

            migrationBuilder.DropIndex(
                name: "IX_ReviewCards_UserId_NormalizedContentKey_Mode",
                table: "ReviewCards");

            migrationBuilder.DropIndex(
                name: "IX_ReviewCards_UserId_Status_LearningState_NextReviewDate",
                table: "ReviewCards");

            migrationBuilder.DropIndex(
                name: "IX_ReviewCards_UserId_VocabularyId_Mode",
                table: "ReviewCards");

            migrationBuilder.DropIndex(
                name: "IX_ReviewCards_VocabularyId",
                table: "ReviewCards");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_Status",
                table: "Lessons");

            migrationBuilder.DropIndex(
                name: "IX_Kanjis_Status",
                table: "Kanjis");

            migrationBuilder.DropIndex(
                name: "IX_GrammarPatterns_Status",
                table: "GrammarPatterns");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Vocabularies");

            migrationBuilder.DropColumn(
                name: "UnpublishedAtUtc",
                table: "Vocabularies");

            migrationBuilder.DropColumn(
                name: "UnpublishedReason",
                table: "Vocabularies");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Vocabularies");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "IsCorrect",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "NewEaseFactor",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "NewIntervalDays",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "NewState",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "PreviousEaseFactor",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "PreviousIntervalDays",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "PreviousState",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "ResponseTimeMs",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "StudyMode",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "UserAnswer",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "AcceptedReadingsJson",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "AudioUrl",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "BackText",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "CardType",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "ContextSnippet",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "CorrectCount",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "ExampleMeaning",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "ExampleSentence",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "FrontText",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "IncorrectCount",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "JlptLevel",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "Lapses",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "LearningState",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "LessonId",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "MasteredAt",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "Meaning",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "NormalizedContentKey",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "PersonalNote",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "Reading",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "SourceLabel",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "SourceLinksJson",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "SourceVersion",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "SrsCardTemplateId",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "SynonymsJson",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "VocabularyId",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "UnpublishedAtUtc",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "UnpublishedReason",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "LessonProgresses");

            migrationBuilder.DropColumn(
                name: "FirstViewedAt",
                table: "LessonProgresses");

            migrationBuilder.DropColumn(
                name: "InteractionCount",
                table: "LessonProgresses");

            migrationBuilder.DropColumn(
                name: "LastInteractedAt",
                table: "LessonProgresses");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Kanjis");

            migrationBuilder.DropColumn(
                name: "UnpublishedAtUtc",
                table: "Kanjis");

            migrationBuilder.DropColumn(
                name: "UnpublishedReason",
                table: "Kanjis");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Kanjis");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "GrammarPatterns");

            migrationBuilder.DropColumn(
                name: "UnpublishedAtUtc",
                table: "GrammarPatterns");

            migrationBuilder.DropColumn(
                name: "UnpublishedReason",
                table: "GrammarPatterns");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "GrammarPatterns");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHistories_ReviewItemId",
                table: "ReviewHistories",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_UserId_KanjiEntryId_Mode",
                table: "ReviewCards",
                columns: new[] { "UserId", "KanjiEntryId", "Mode" },
                unique: true);
        }
    }
}
