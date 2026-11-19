using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentAudioTranscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentAudioTranscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AudioUrlHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Transcript = table.Column<string>(type: "text", nullable: true),
                    RawTranscriptJson = table.Column<string>(type: "jsonb", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentAudioTranscriptions", x => x.Id);
                    table.CheckConstraint("CK_ContentAudioTranscription_Attempts", "\"AttemptCount\" >= 0");
                    table.CheckConstraint("CK_ContentAudioTranscription_Language", "\"LanguageCode\" = 'ja-JP'");
                    table.CheckConstraint("CK_ContentAudioTranscription_Status", "\"Status\" IN ('Pending', 'Processing', 'Completed', 'Failed')");
                    table.ForeignKey(
                        name: "FK_ContentAudioTranscriptions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentAudioTranscriptions_AudioUrlHash",
                table: "ContentAudioTranscriptions",
                column: "AudioUrlHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentAudioTranscriptions_CreatedByUserId",
                table: "ContentAudioTranscriptions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentAudioTranscriptions_Status_NextAttemptAtUtc_LeaseExp~",
                table: "ContentAudioTranscriptions",
                columns: new[] { "Status", "NextAttemptAtUtc", "LeaseExpiresAtUtc" });

            migrationBuilder.Sql("ALTER TABLE \"ContentAudioTranscriptions\" ENABLE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentAudioTranscriptions");
        }
    }
}
