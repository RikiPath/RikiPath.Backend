using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentAuthoringExamBankV4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionReference",
                table: "ReviewHistories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "MockTestSections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "MockTestSections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SectionPolicyJson",
                table: "MockTestSections",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiProvenanceJson",
                table: "MockTests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AuthorUserId",
                table: "MockTests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlueprintPolicyJson",
                table: "MockTests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExamKind",
                table: "MockTests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FocusSkillId",
                table: "MockTests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncludedSkillsJson",
                table: "MockTests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchivedVersion",
                table: "MockTests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsManagedWorkflow",
                table: "MockTests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentExamId",
                table: "MockTests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublishStatus",
                table: "MockTests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "MockTests",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewHistoryJson",
                table: "MockTests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "MockTests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "MockTests",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByName",
                table: "MockTests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RightsStatus",
                table: "MockTests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "MockTests",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "MockTests",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "MockTests",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "MockTests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "MockTests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PassageId",
                table: "MockQuestions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaybackPolicyJson",
                table: "MockQuestions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuestionBankItemId",
                table: "MockQuestions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionGroupTitle",
                table: "MockQuestions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuestionVersion",
                table: "MockQuestions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SkillMetadataJson",
                table: "MockQuestions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateKind",
                table: "MockQuestions",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiProvenanceJson",
                table: "ContentApprovalRequests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CertificateLevelId",
                table: "ContentApprovalRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "ContentApprovalRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LanguageSkillId",
                table: "ContentApprovalRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewHistoryJson",
                table: "ContentApprovalRequests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RightsConfirmed",
                table: "ContentApprovalRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RightsStatus",
                table: "ContentApprovalRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "ContentApprovalRequests",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "ContentApprovalRequests",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ImportExcel");

            migrationBuilder.AddColumn<string>(
                name: "ValidationSummaryJson",
                table: "ContentApprovalRequests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentPassages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PassageIdentity = table.Column<Guid>(type: "uuid", nullable: false),
                    PassageCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LanguageSkillId = table.Column<int>(type: "integer", nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PassageText = table.Column<string>(type: "text", nullable: true),
                    Instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ListeningSection = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    AudioDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    AudioTranscript = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    PlaybackPolicyJson = table.Column<string>(type: "jsonb", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Manual"),
                    SourceReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RightsStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AiProvenanceJson = table.Column<string>(type: "jsonb", nullable: true),
                    ContentFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublishStatus = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ParentPassageId = table.Column<int>(type: "integer", nullable: true),
                    IsArchivedVersion = table.Column<bool>(type: "boolean", nullable: false),
                    AuthorUserId = table.Column<int>(type: "integer", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByName = table.Column<string>(type: "text", nullable: true),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewHistoryJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentPassages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentPassages_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentPassages_LanguageSkills_LanguageSkillId",
                        column: x => x.LanguageSkillId,
                        principalTable: "LanguageSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SrsReviewSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    StudyMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FilterCardType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FilterJlptLevel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    FilterSourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RequestedCount = table.Column<int>(type: "integer", nullable: false),
                    TotalInitialCards = table.Column<int>(type: "integer", nullable: false),
                    QueueCardIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CompletedCardIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RetryCardIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReviewedCount = table.Column<int>(type: "integer", nullable: false),
                    RememberedCount = table.Column<int>(type: "integer", nullable: false),
                    ForgotCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastActivityAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SrsReviewSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SrsReviewSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionBankItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionIdentity = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    LanguageSkillId = table.Column<int>(type: "integer", nullable: false),
                    PassageId = table.Column<int>(type: "integer", nullable: true),
                    QuestionGroupTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GroupSortOrder = table.Column<int>(type: "integer", nullable: false),
                    TemplateKind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, defaultValue: "VocabMeaning"),
                    QuestionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "SingleChoice"),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    SkillMetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    InteractionDataJson = table.Column<string>(type: "jsonb", nullable: true),
                    AnswerKeyJson = table.Column<string>(type: "jsonb", nullable: true),
                    OptionsJson = table.Column<string>(type: "jsonb", nullable: true),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    AudioDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    AudioTranscript = table.Column<string>(type: "text", nullable: true),
                    PlaybackPolicyJson = table.Column<string>(type: "jsonb", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    Difficulty = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Topic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Manual"),
                    SourceReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RightsStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AiProvenanceJson = table.Column<string>(type: "jsonb", nullable: true),
                    ContentFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublishStatus = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ParentQuestionId = table.Column<int>(type: "integer", nullable: true),
                    IsArchivedVersion = table.Column<bool>(type: "boolean", nullable: false),
                    AuthorUserId = table.Column<int>(type: "integer", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByName = table.Column<string>(type: "text", nullable: true),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewHistoryJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionBankItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionBankItems_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionBankItems_ContentPassages_PassageId",
                        column: x => x.PassageId,
                        principalTable: "ContentPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QuestionBankItems_LanguageSkills_LanguageSkillId",
                        column: x => x.LanguageSkillId,
                        principalTable: "LanguageSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHistories_SessionReference",
                table: "ReviewHistories",
                column: "SessionReference");

            migrationBuilder.CreateIndex(
                name: "IX_MockQuestions_PassageId",
                table: "MockQuestions",
                column: "PassageId");

            migrationBuilder.CreateIndex(
                name: "IX_MockQuestions_QuestionBankItemId",
                table: "MockQuestions",
                column: "QuestionBankItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentApprovalRequests_SubmittedByUserId_IdempotencyKey",
                table: "ContentApprovalRequests",
                columns: new[] { "SubmittedByUserId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContentPassages_CertificateLevelId_LanguageSkillId_PublishS~",
                table: "ContentPassages",
                columns: new[] { "CertificateLevelId", "LanguageSkillId", "PublishStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPassages_ContentFingerprint",
                table: "ContentPassages",
                column: "ContentFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_ContentPassages_LanguageSkillId",
                table: "ContentPassages",
                column: "LanguageSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentPassages_PassageIdentity",
                table: "ContentPassages",
                column: "PassageIdentity");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBankItems_AuthorUserId",
                table: "QuestionBankItems",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBankItems_CertificateLevelId_LanguageSkillId_Publis~",
                table: "QuestionBankItems",
                columns: new[] { "CertificateLevelId", "LanguageSkillId", "PublishStatus", "IsArchivedVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBankItems_ContentFingerprint",
                table: "QuestionBankItems",
                column: "ContentFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBankItems_LanguageSkillId",
                table: "QuestionBankItems",
                column: "LanguageSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBankItems_PassageId",
                table: "QuestionBankItems",
                column: "PassageId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBankItems_QuestionIdentity",
                table: "QuestionBankItems",
                column: "QuestionIdentity");

            migrationBuilder.CreateIndex(
                name: "IX_SrsReviewSessions_SessionKey",
                table: "SrsReviewSessions",
                column: "SessionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SrsReviewSessions_UserId_Status_LastActivityAtUtc",
                table: "SrsReviewSessions",
                columns: new[] { "UserId", "Status", "LastActivityAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_MockQuestions_ContentPassages_PassageId",
                table: "MockQuestions",
                column: "PassageId",
                principalTable: "ContentPassages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MockQuestions_QuestionBankItems_QuestionBankItemId",
                table: "MockQuestions",
                column: "QuestionBankItemId",
                principalTable: "QuestionBankItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MockQuestions_ContentPassages_PassageId",
                table: "MockQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_MockQuestions_QuestionBankItems_QuestionBankItemId",
                table: "MockQuestions");

            migrationBuilder.DropTable(
                name: "QuestionBankItems");

            migrationBuilder.DropTable(
                name: "SrsReviewSessions");

            migrationBuilder.DropTable(
                name: "ContentPassages");

            migrationBuilder.DropIndex(
                name: "IX_ReviewHistories_SessionReference",
                table: "ReviewHistories");

            migrationBuilder.DropIndex(
                name: "IX_MockQuestions_PassageId",
                table: "MockQuestions");

            migrationBuilder.DropIndex(
                name: "IX_MockQuestions_QuestionBankItemId",
                table: "MockQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ContentApprovalRequests_SubmittedByUserId_IdempotencyKey",
                table: "ContentApprovalRequests");

            migrationBuilder.DropColumn(
                name: "SessionReference",
                table: "ReviewHistories");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "MockTestSections");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "MockTestSections");

            migrationBuilder.DropColumn(
                name: "SectionPolicyJson",
                table: "MockTestSections");

            migrationBuilder.DropColumn(
                name: "AiProvenanceJson",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "AuthorUserId",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "BlueprintPolicyJson",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "ExamKind",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "FocusSkillId",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "IncludedSkillsJson",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "IsArchivedVersion",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "IsManagedWorkflow",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "ParentExamId",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "PublishStatus",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "ReviewHistoryJson",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "ReviewedByName",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "RightsStatus",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "MockTests");

            migrationBuilder.DropColumn(
                name: "PassageId",
                table: "MockQuestions");

            migrationBuilder.DropColumn(
                name: "PlaybackPolicyJson",
                table: "MockQuestions");

            migrationBuilder.DropColumn(
                name: "QuestionBankItemId",
                table: "MockQuestions");

            migrationBuilder.DropColumn(
                name: "QuestionGroupTitle",
                table: "MockQuestions");

            migrationBuilder.DropColumn(
                name: "QuestionVersion",
                table: "MockQuestions");

            migrationBuilder.DropColumn(
                name: "SkillMetadataJson",
                table: "MockQuestions");

            migrationBuilder.DropColumn(
                name: "TemplateKind",
                table: "MockQuestions");

            migrationBuilder.DropColumn(
                name: "AiProvenanceJson",
                table: "ContentApprovalRequests");

            migrationBuilder.DropColumn(
                name: "CertificateLevelId",
                table: "ContentApprovalRequests");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ContentApprovalRequests");

            migrationBuilder.DropColumn(
                name: "LanguageSkillId",
                table: "ContentApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ReviewHistoryJson",
                table: "ContentApprovalRequests");

            migrationBuilder.DropColumn(
                name: "RightsConfirmed",
                table: "ContentApprovalRequests");

            migrationBuilder.DropColumn(
                name: "RightsStatus",
                table: "ContentApprovalRequests");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "ContentApprovalRequests");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "ContentApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ValidationSummaryJson",
                table: "ContentApprovalRequests");
        }
    }
}
