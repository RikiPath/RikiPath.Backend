using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDatabaseVersion2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CertificationLevelSkills_CertificationLevels_CertificationL~",
                table: "CertificationLevelSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationLevelSkills_Skills_SkillId",
                table: "CertificationLevelSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_LessonGrammars_GrammarPoints_GrammarPointId",
                table: "LessonGrammars");

            migrationBuilder.DropForeignKey(
                name: "FK_LessonKanjis_KanjiEntries_KanjiEntryId",
                table: "LessonKanjis");

            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_CertificationLevels_CertificationLevelId",
                table: "Lessons");

            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_Skills_SkillId",
                table: "Lessons");

            migrationBuilder.DropForeignKey(
                name: "FK_LessonVocabularies_VocabularyEntries_VocabularyEntryId",
                table: "LessonVocabularies");

            migrationBuilder.DropForeignKey(
                name: "FK_PracticeQuestions_PracticeTestSections_PracticeTestSectionId",
                table: "PracticeQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_PracticeSubmissions_CertificationLevels_CertificationLevelId",
                table: "PracticeSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_CertificationLevels_TargetCertificationLevelId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "ConsultationAnswers");

            migrationBuilder.DropTable(
                name: "LearningPathSuggestions");

            migrationBuilder.DropTable(
                name: "PracticeTestAnswers");

            migrationBuilder.DropTable(
                name: "PracticeTestSectionResults");

            migrationBuilder.DropTable(
                name: "ReviewLogs");

            migrationBuilder.DropTable(
                name: "ConsultationRequests");

            migrationBuilder.DropTable(
                name: "PracticeTestAttempts");

            migrationBuilder.DropTable(
                name: "PracticeTestSections");

            migrationBuilder.DropTable(
                name: "ReviewItems");

            migrationBuilder.DropTable(
                name: "ConsultantAvailabilities");

            migrationBuilder.DropTable(
                name: "ConsultationPurchases");

            migrationBuilder.DropTable(
                name: "PracticeTests");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "GrammarPoints");

            migrationBuilder.DropTable(
                name: "VocabularyNoteEntries");

            migrationBuilder.DropTable(
                name: "ConsultationPackages");

            migrationBuilder.DropTable(
                name: "KanjiEntries");

            migrationBuilder.DropTable(
                name: "VocabularyEntries");

            migrationBuilder.DropTable(
                name: "VocabularyLists");

            migrationBuilder.DropTable(
                name: "CertificationLevels");

            migrationBuilder.DropTable(
                name: "Certifications");

            migrationBuilder.DropColumn(
                name: "AudioUrl",
                table: "PracticeQuestions");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "PracticeQuestions");

            migrationBuilder.RenameColumn(
                name: "PracticeTestSectionId",
                table: "PracticeQuestions",
                newName: "PracticeExerciseId");

            migrationBuilder.RenameIndex(
                name: "IX_PracticeQuestions_PracticeTestSectionId",
                table: "PracticeQuestions",
                newName: "IX_PracticeQuestions_PracticeExerciseId");

            migrationBuilder.AlterColumn<string>(
                name: "OptionText",
                table: "PracticeQuestionOptions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Lessons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "PracticeSubmissionId",
                table: "GradingResults",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "HomeworkSubmissionId",
                table: "GradingResults",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CertificateTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Features",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Features", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomeworkAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    LessonId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkAssignments_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HomeworkAssignments_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkAssignments_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LanguageSkills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageSkills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LearnerNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearnerNotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MentorAvailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConsultantId = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsBooked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorAvailabilities_Users_ConsultantId",
                        column: x => x.ConsultantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecommendedLearningPaths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SuggestionJson = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    IsViewed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendedLearningPaths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecommendedLearningPaths_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificateLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificationId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateLevels_CertificateTypes_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "CertificateTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeatureSubscriptionPlan",
                columns: table => new
                {
                    FeaturesId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionPlansId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureSubscriptionPlan", x => new { x.FeaturesId, x.SubscriptionPlansId });
                    table.ForeignKey(
                        name: "FK_FeatureSubscriptionPlan_Features_FeaturesId",
                        column: x => x.FeaturesId,
                        principalTable: "Features",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeatureSubscriptionPlan_SubscriptionPlans_SubscriptionPlans~",
                        column: x => x.SubscriptionPlansId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeworkSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HomeworkAssignmentId = table.Column<int>(type: "integer", nullable: false),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    LessonId = table.Column<int>(type: "integer", nullable: false),
                    TextContent = table.Column<string>(type: "text", nullable: true),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmissions_HomeworkAssignments_HomeworkAssignmentId",
                        column: x => x.HomeworkAssignmentId,
                        principalTable: "HomeworkAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmissions_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmissions_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorBookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserSubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    MentorAvailabilityId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Question = table.Column<string>(type: "text", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MeetingLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UserAccountId = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorBookings_MentorAvailabilities_MentorAvailabilityId",
                        column: x => x.MentorAvailabilityId,
                        principalTable: "MentorAvailabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MentorBookings_UserSubscriptions_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorBookings_Users_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GrammarPatterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Structure = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UsageNotes = table.Column<string>(type: "text", nullable: true),
                    ExampleSentence = table.Column<string>(type: "text", nullable: true),
                    ExampleSentenceMeaning = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    ContentAuthorId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrammarPatterns_CertificateLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrammarPatterns_Users_ContentAuthorId",
                        column: x => x.ContentAuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Kanjis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Character = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Meaning = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SinoVietnamese = table.Column<string>(type: "text", nullable: true),
                    OnYomi = table.Column<string>(type: "text", nullable: true),
                    KunYomi = table.Column<string>(type: "text", nullable: true),
                    StrokeCount = table.Column<int>(type: "integer", nullable: false),
                    StrokeOrderImageUrl = table.Column<string>(type: "text", nullable: true),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    ContentAuthorId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kanjis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kanjis_CertificateLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kanjis_Users_ContentAuthorId",
                        column: x => x.ContentAuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MockTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TimeLimitMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    ContentAuthorId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MockTests_CertificateLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MockTests_Users_ContentAuthorId",
                        column: x => x.ContentAuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PracticeExercises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LessonId = table.Column<int>(type: "integer", nullable: false),
                    LanguageSkillId = table.Column<int>(type: "integer", nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    ContentAuthorId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeExercises_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PracticeExercises_LanguageSkills_LanguageSkillId",
                        column: x => x.LanguageSkillId,
                        principalTable: "LanguageSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PracticeExercises_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PracticeExercises_Users_ContentAuthorId",
                        column: x => x.ContentAuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vocabularies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Word = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reading = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Meaning = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ExampleSentence = table.Column<string>(type: "text", nullable: true),
                    ExampleSentenceMeaning = table.Column<string>(type: "text", nullable: true),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    ContentAuthorId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vocabularies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vocabularies_CertificateLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vocabularies_Users_ContentAuthorId",
                        column: x => x.ContentAuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConsultationRequestId = table.Column<int>(type: "integer", nullable: false),
                    ConsultantId = table.Column<int>(type: "integer", nullable: false),
                    AnswerText = table.Column<string>(type: "text", nullable: true),
                    MeetingNotes = table.Column<string>(type: "text", nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notes_MentorBookings_ConsultationRequestId",
                        column: x => x.ConsultationRequestId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notes_Users_ConsultantId",
                        column: x => x.ConsultantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MockTestAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PracticeTestId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    TotalScore = table.Column<double>(type: "double precision", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockTestAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MockTestAttempts_MockTests_PracticeTestId",
                        column: x => x.PracticeTestId,
                        principalTable: "MockTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MockTestAttempts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MockTestSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    PracticeTestId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockTestSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MockTestSections_LanguageSkills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "LanguageSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MockTestSections_MockTests_PracticeTestId",
                        column: x => x.PracticeTestId,
                        principalTable: "MockTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticeExerciseAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PracticeExerciseId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ScorePercent = table.Column<double>(type: "double precision", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeExerciseAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeExerciseAttempts_PracticeExercises_PracticeExercise~",
                        column: x => x.PracticeExerciseId,
                        principalTable: "PracticeExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PracticeExerciseAttempts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearnerNoteEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VocabularyListId = table.Column<int>(type: "integer", nullable: false),
                    VocabularyEntryId = table.Column<int>(type: "integer", nullable: true),
                    KanjiEntryId = table.Column<int>(type: "integer", nullable: true),
                    ManualWord = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ManualReading = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ManualMeaning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerNoteEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearnerNoteEntries_Kanjis_KanjiEntryId",
                        column: x => x.KanjiEntryId,
                        principalTable: "Kanjis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearnerNoteEntries_LearnerNotes_VocabularyListId",
                        column: x => x.VocabularyListId,
                        principalTable: "LearnerNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearnerNoteEntries_Vocabularies_VocabularyEntryId",
                        column: x => x.VocabularyEntryId,
                        principalTable: "Vocabularies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MockQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    PracticeTestSectionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MockQuestions_MockTestSections_PracticeTestSectionId",
                        column: x => x.PracticeTestSectionId,
                        principalTable: "MockTestSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MockTestSectionResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PracticeTestAttemptId = table.Column<int>(type: "integer", nullable: false),
                    PracticeTestSectionId = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    ScorePercent = table.Column<double>(type: "double precision", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockTestSectionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MockTestSectionResults_MockTestAttempts_PracticeTestAttempt~",
                        column: x => x.PracticeTestAttemptId,
                        principalTable: "MockTestAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MockTestSectionResults_MockTestSections_PracticeTestSection~",
                        column: x => x.PracticeTestSectionId,
                        principalTable: "MockTestSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearnerPracticeAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PracticeExerciseAttemptId = table.Column<int>(type: "integer", nullable: false),
                    PracticeQuestionId = table.Column<int>(type: "integer", nullable: false),
                    SelectedOptionId = table.Column<int>(type: "integer", nullable: true),
                    TextAnswer = table.Column<string>(type: "text", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerPracticeAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearnerPracticeAnswers_PracticeExerciseAttempts_PracticeExe~",
                        column: x => x.PracticeExerciseAttemptId,
                        principalTable: "PracticeExerciseAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearnerPracticeAnswers_PracticeQuestionOptions_SelectedOpti~",
                        column: x => x.SelectedOptionId,
                        principalTable: "PracticeQuestionOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LearnerPracticeAnswers_PracticeQuestions_PracticeQuestionId",
                        column: x => x.PracticeQuestionId,
                        principalTable: "PracticeQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    VocabularyNoteEntryId = table.Column<int>(type: "integer", nullable: true),
                    KanjiEntryId = table.Column<int>(type: "integer", nullable: true),
                    GrammarPointId = table.Column<int>(type: "integer", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    EaseFactor = table.Column<double>(type: "double precision", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    Repetitions = table.Column<int>(type: "integer", nullable: false),
                    NextReviewDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewCards_GrammarPatterns_GrammarPointId",
                        column: x => x.GrammarPointId,
                        principalTable: "GrammarPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewCards_Kanjis_KanjiEntryId",
                        column: x => x.KanjiEntryId,
                        principalTable: "Kanjis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewCards_LearnerNoteEntries_VocabularyNoteEntryId",
                        column: x => x.VocabularyNoteEntryId,
                        principalTable: "LearnerNoteEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewCards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MockQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    PracticeQuestionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MockQuestionOptions_MockQuestions_PracticeQuestionId",
                        column: x => x.PracticeQuestionId,
                        principalTable: "MockQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewItemId = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Quality = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewHistories_ReviewCards_ReviewItemId",
                        column: x => x.ReviewItemId,
                        principalTable: "ReviewCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MockTestAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PracticeTestAttemptId = table.Column<int>(type: "integer", nullable: false),
                    PracticeQuestionId = table.Column<int>(type: "integer", nullable: false),
                    SelectedOptionId = table.Column<int>(type: "integer", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockTestAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MockTestAnswers_MockQuestionOptions_SelectedOptionId",
                        column: x => x.SelectedOptionId,
                        principalTable: "MockQuestionOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MockTestAnswers_MockQuestions_PracticeQuestionId",
                        column: x => x.PracticeQuestionId,
                        principalTable: "MockQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MockTestAnswers_MockTestAttempts_PracticeTestAttemptId",
                        column: x => x.PracticeTestAttemptId,
                        principalTable: "MockTestAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradingResults_HomeworkSubmissionId",
                table: "GradingResults",
                column: "HomeworkSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateLevels_CertificationId_Code",
                table: "CertificateLevels",
                columns: new[] { "CertificationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateTypes_Name",
                table: "CertificateTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureSubscriptionPlan_SubscriptionPlansId",
                table: "FeatureSubscriptionPlan",
                column: "SubscriptionPlansId");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPatterns_CertificationLevelId_Status",
                table: "GrammarPatterns",
                columns: new[] { "CertificationLevelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPatterns_ContentAuthorId",
                table: "GrammarPatterns",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_LearnerId_DueAt",
                table: "HomeworkAssignments",
                columns: new[] { "LearnerId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_LessonId",
                table: "HomeworkAssignments",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_MentorId",
                table: "HomeworkAssignments",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmissions_HomeworkAssignmentId",
                table: "HomeworkSubmissions",
                column: "HomeworkAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmissions_LearnerId",
                table: "HomeworkSubmissions",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmissions_LessonId",
                table: "HomeworkSubmissions",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_Kanjis_CertificationLevelId_Status",
                table: "Kanjis",
                columns: new[] { "CertificationLevelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Kanjis_ContentAuthorId",
                table: "Kanjis",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageSkills_Name",
                table: "LanguageSkills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnerNoteEntries_KanjiEntryId",
                table: "LearnerNoteEntries",
                column: "KanjiEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerNoteEntries_VocabularyEntryId",
                table: "LearnerNoteEntries",
                column: "VocabularyEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerNoteEntries_VocabularyListId",
                table: "LearnerNoteEntries",
                column: "VocabularyListId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerNotes_UserId_Name",
                table: "LearnerNotes",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_LearnerPracticeAnswers_PracticeExerciseAttemptId_PracticeQu~",
                table: "LearnerPracticeAnswers",
                columns: new[] { "PracticeExerciseAttemptId", "PracticeQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnerPracticeAnswers_PracticeQuestionId",
                table: "LearnerPracticeAnswers",
                column: "PracticeQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerPracticeAnswers_SelectedOptionId",
                table: "LearnerPracticeAnswers",
                column: "SelectedOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_ConsultantId_StartTime_EndTime",
                table: "MentorAvailabilities",
                columns: new[] { "ConsultantId", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_MentorAvailabilityId",
                table: "MentorBookings",
                column: "MentorAvailabilityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_UserAccountId",
                table: "MentorBookings",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_UserSubscriptionId_Status",
                table: "MentorBookings",
                columns: new[] { "UserSubscriptionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MockQuestionOptions_PracticeQuestionId",
                table: "MockQuestionOptions",
                column: "PracticeQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_MockQuestions_PracticeTestSectionId",
                table: "MockQuestions",
                column: "PracticeTestSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestAnswers_PracticeQuestionId",
                table: "MockTestAnswers",
                column: "PracticeQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestAnswers_PracticeTestAttemptId_PracticeQuestionId",
                table: "MockTestAnswers",
                columns: new[] { "PracticeTestAttemptId", "PracticeQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MockTestAnswers_SelectedOptionId",
                table: "MockTestAnswers",
                column: "SelectedOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestAttempts_PracticeTestId",
                table: "MockTestAttempts",
                column: "PracticeTestId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestAttempts_UserId_PracticeTestId",
                table: "MockTestAttempts",
                columns: new[] { "UserId", "PracticeTestId" });

            migrationBuilder.CreateIndex(
                name: "IX_MockTests_CertificationLevelId_Status",
                table: "MockTests",
                columns: new[] { "CertificationLevelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MockTests_ContentAuthorId",
                table: "MockTests",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestSectionResults_PracticeTestAttemptId_PracticeTestSe~",
                table: "MockTestSectionResults",
                columns: new[] { "PracticeTestAttemptId", "PracticeTestSectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MockTestSectionResults_PracticeTestSectionId",
                table: "MockTestSectionResults",
                column: "PracticeTestSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestSections_PracticeTestId",
                table: "MockTestSections",
                column: "PracticeTestId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestSections_SkillId",
                table: "MockTestSections",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_ConsultantId",
                table: "Notes",
                column: "ConsultantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_ConsultationRequestId",
                table: "Notes",
                column: "ConsultationRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExerciseAttempts_PracticeExerciseId",
                table: "PracticeExerciseAttempts",
                column: "PracticeExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExerciseAttempts_UserId_PracticeExerciseId_StartedAt",
                table: "PracticeExerciseAttempts",
                columns: new[] { "UserId", "PracticeExerciseId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_CertificateLevelId",
                table: "PracticeExercises",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_ContentAuthorId",
                table: "PracticeExercises",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_LanguageSkillId",
                table: "PracticeExercises",
                column: "LanguageSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_LessonId_LanguageSkillId_Status",
                table: "PracticeExercises",
                columns: new[] { "LessonId", "LanguageSkillId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecommendedLearningPaths_UserId_GeneratedAt",
                table: "RecommendedLearningPaths",
                columns: new[] { "UserId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_GrammarPointId",
                table: "ReviewCards",
                column: "GrammarPointId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_KanjiEntryId",
                table: "ReviewCards",
                column: "KanjiEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_UserId_NextReviewDate",
                table: "ReviewCards",
                columns: new[] { "UserId", "NextReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_VocabularyNoteEntryId",
                table: "ReviewCards",
                column: "VocabularyNoteEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHistories_ReviewItemId",
                table: "ReviewHistories",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Vocabularies_CertificationLevelId_Status",
                table: "Vocabularies",
                columns: new[] { "CertificationLevelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vocabularies_ContentAuthorId",
                table: "Vocabularies",
                column: "ContentAuthorId");

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationLevelSkills_CertificateLevels_CertificationLev~",
                table: "CertificationLevelSkills",
                column: "CertificationLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationLevelSkills_LanguageSkills_SkillId",
                table: "CertificationLevelSkills",
                column: "SkillId",
                principalTable: "LanguageSkills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GradingResults_HomeworkSubmissions_HomeworkSubmissionId",
                table: "GradingResults",
                column: "HomeworkSubmissionId",
                principalTable: "HomeworkSubmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LessonGrammars_GrammarPatterns_GrammarPointId",
                table: "LessonGrammars",
                column: "GrammarPointId",
                principalTable: "GrammarPatterns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LessonKanjis_Kanjis_KanjiEntryId",
                table: "LessonKanjis",
                column: "KanjiEntryId",
                principalTable: "Kanjis",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_CertificateLevels_CertificationLevelId",
                table: "Lessons",
                column: "CertificationLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_LanguageSkills_SkillId",
                table: "Lessons",
                column: "SkillId",
                principalTable: "LanguageSkills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LessonVocabularies_Vocabularies_VocabularyEntryId",
                table: "LessonVocabularies",
                column: "VocabularyEntryId",
                principalTable: "Vocabularies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeQuestions_PracticeExercises_PracticeExerciseId",
                table: "PracticeQuestions",
                column: "PracticeExerciseId",
                principalTable: "PracticeExercises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeSubmissions_CertificateLevels_CertificationLevelId",
                table: "PracticeSubmissions",
                column: "CertificationLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_CertificateLevels_TargetCertificationLevelId",
                table: "Users",
                column: "TargetCertificationLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CertificationLevelSkills_CertificateLevels_CertificationLev~",
                table: "CertificationLevelSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationLevelSkills_LanguageSkills_SkillId",
                table: "CertificationLevelSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_GradingResults_HomeworkSubmissions_HomeworkSubmissionId",
                table: "GradingResults");

            migrationBuilder.DropForeignKey(
                name: "FK_LessonGrammars_GrammarPatterns_GrammarPointId",
                table: "LessonGrammars");

            migrationBuilder.DropForeignKey(
                name: "FK_LessonKanjis_Kanjis_KanjiEntryId",
                table: "LessonKanjis");

            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_CertificateLevels_CertificationLevelId",
                table: "Lessons");

            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_LanguageSkills_SkillId",
                table: "Lessons");

            migrationBuilder.DropForeignKey(
                name: "FK_LessonVocabularies_Vocabularies_VocabularyEntryId",
                table: "LessonVocabularies");

            migrationBuilder.DropForeignKey(
                name: "FK_PracticeQuestions_PracticeExercises_PracticeExerciseId",
                table: "PracticeQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_PracticeSubmissions_CertificateLevels_CertificationLevelId",
                table: "PracticeSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_CertificateLevels_TargetCertificationLevelId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "FeatureSubscriptionPlan");

            migrationBuilder.DropTable(
                name: "HomeworkSubmissions");

            migrationBuilder.DropTable(
                name: "LearnerPracticeAnswers");

            migrationBuilder.DropTable(
                name: "MockTestAnswers");

            migrationBuilder.DropTable(
                name: "MockTestSectionResults");

            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "RecommendedLearningPaths");

            migrationBuilder.DropTable(
                name: "ReviewHistories");

            migrationBuilder.DropTable(
                name: "Features");

            migrationBuilder.DropTable(
                name: "HomeworkAssignments");

            migrationBuilder.DropTable(
                name: "PracticeExerciseAttempts");

            migrationBuilder.DropTable(
                name: "MockQuestionOptions");

            migrationBuilder.DropTable(
                name: "MockTestAttempts");

            migrationBuilder.DropTable(
                name: "MentorBookings");

            migrationBuilder.DropTable(
                name: "ReviewCards");

            migrationBuilder.DropTable(
                name: "PracticeExercises");

            migrationBuilder.DropTable(
                name: "MockQuestions");

            migrationBuilder.DropTable(
                name: "MentorAvailabilities");

            migrationBuilder.DropTable(
                name: "GrammarPatterns");

            migrationBuilder.DropTable(
                name: "LearnerNoteEntries");

            migrationBuilder.DropTable(
                name: "MockTestSections");

            migrationBuilder.DropTable(
                name: "Kanjis");

            migrationBuilder.DropTable(
                name: "LearnerNotes");

            migrationBuilder.DropTable(
                name: "Vocabularies");

            migrationBuilder.DropTable(
                name: "LanguageSkills");

            migrationBuilder.DropTable(
                name: "MockTests");

            migrationBuilder.DropTable(
                name: "CertificateLevels");

            migrationBuilder.DropTable(
                name: "CertificateTypes");

            migrationBuilder.DropIndex(
                name: "IX_GradingResults_HomeworkSubmissionId",
                table: "GradingResults");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "HomeworkSubmissionId",
                table: "GradingResults");

            migrationBuilder.RenameColumn(
                name: "PracticeExerciseId",
                table: "PracticeQuestions",
                newName: "PracticeTestSectionId");

            migrationBuilder.RenameIndex(
                name: "IX_PracticeQuestions_PracticeExerciseId",
                table: "PracticeQuestions",
                newName: "IX_PracticeQuestions_PracticeTestSectionId");

            migrationBuilder.AddColumn<string>(
                name: "AudioUrl",
                table: "PracticeQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "PracticeQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OptionText",
                table: "PracticeQuestionOptions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<int>(
                name: "PracticeSubmissionId",
                table: "GradingResults",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Certifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsultantAvailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConsultantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsBooked = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultantAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultantAvailabilities_Users_ConsultantId",
                        column: x => x.ConsultantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LearningPathSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsViewed = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SuggestionJson = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningPathSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningPathSuggestions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyLists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificationLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificationId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificationLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificationLevels_Certifications_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "Certifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationPurchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConsultationPackageId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultationPurchases_ConsultationPackages_ConsultationPack~",
                        column: x => x.ConsultationPackageId,
                        principalTable: "ConsultationPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsultationPurchases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GrammarPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    ContentAuthorId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExampleSentence = table.Column<string>(type: "text", nullable: true),
                    ExampleSentenceMeaning = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Structure = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UsageNotes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrammarPoints_CertificationLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrammarPoints_Users_ContentAuthorId",
                        column: x => x.ContentAuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KanjiEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    ContentAuthorId = table.Column<int>(type: "integer", nullable: false),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    Character = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    KunYomi = table.Column<string>(type: "text", nullable: true),
                    Meaning = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OnYomi = table.Column<string>(type: "text", nullable: true),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SinoVietnamese = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StrokeCount = table.Column<int>(type: "integer", nullable: false),
                    StrokeOrderImageUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanjiEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanjiEntries_CertificationLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KanjiEntries_Users_ContentAuthorId",
                        column: x => x.ContentAuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PracticeTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    ContentAuthorId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TimeLimitMinutes = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeTests_CertificationLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PracticeTests_Users_ContentAuthorId",
                        column: x => x.ContentAuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    ContentAuthorId = table.Column<int>(type: "integer", nullable: false),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExampleSentence = table.Column<string>(type: "text", nullable: true),
                    ExampleSentenceMeaning = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Meaning = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Reading = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Word = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyEntries_CertificationLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VocabularyEntries_Users_ContentAuthorId",
                        column: x => x.ContentAuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConsultantAvailabilityId = table.Column<int>(type: "integer", nullable: true),
                    ConsultantId = table.Column<int>(type: "integer", nullable: true),
                    ConsultationPurchaseId = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MeetingLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Question = table.Column<string>(type: "text", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultationRequests_ConsultantAvailabilities_ConsultantAva~",
                        column: x => x.ConsultantAvailabilityId,
                        principalTable: "ConsultantAvailabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConsultationRequests_ConsultationPurchases_ConsultationPurc~",
                        column: x => x.ConsultationPurchaseId,
                        principalTable: "ConsultationPurchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConsultationRequests_Users_ConsultantId",
                        column: x => x.ConsultantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PracticeTestAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PracticeTestId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TotalScore = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeTestAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeTestAttempts_PracticeTests_PracticeTestId",
                        column: x => x.PracticeTestId,
                        principalTable: "PracticeTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PracticeTestAttempts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticeTestSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PracticeTestId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeTestSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeTestSections_PracticeTests_PracticeTestId",
                        column: x => x.PracticeTestId,
                        principalTable: "PracticeTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PracticeTestSections_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyNoteEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KanjiEntryId = table.Column<int>(type: "integer", nullable: true),
                    VocabularyEntryId = table.Column<int>(type: "integer", nullable: true),
                    VocabularyListId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ManualMeaning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ManualReading = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ManualWord = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyNoteEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyNoteEntries_KanjiEntries_KanjiEntryId",
                        column: x => x.KanjiEntryId,
                        principalTable: "KanjiEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VocabularyNoteEntries_VocabularyEntries_VocabularyEntryId",
                        column: x => x.VocabularyEntryId,
                        principalTable: "VocabularyEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VocabularyNoteEntries_VocabularyLists_VocabularyListId",
                        column: x => x.VocabularyListId,
                        principalTable: "VocabularyLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConsultantId = table.Column<int>(type: "integer", nullable: false),
                    ConsultationRequestId = table.Column<int>(type: "integer", nullable: false),
                    AnswerText = table.Column<string>(type: "text", nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MeetingNotes = table.Column<string>(type: "text", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultationAnswers_ConsultationRequests_ConsultationReques~",
                        column: x => x.ConsultationRequestId,
                        principalTable: "ConsultationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConsultationAnswers_Users_ConsultantId",
                        column: x => x.ConsultantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PracticeTestAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PracticeQuestionId = table.Column<int>(type: "integer", nullable: false),
                    PracticeTestAttemptId = table.Column<int>(type: "integer", nullable: false),
                    SelectedOptionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeTestAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeTestAnswers_PracticeQuestionOptions_SelectedOptionId",
                        column: x => x.SelectedOptionId,
                        principalTable: "PracticeQuestionOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PracticeTestAnswers_PracticeQuestions_PracticeQuestionId",
                        column: x => x.PracticeQuestionId,
                        principalTable: "PracticeQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PracticeTestAnswers_PracticeTestAttempts_PracticeTestAttemp~",
                        column: x => x.PracticeTestAttemptId,
                        principalTable: "PracticeTestAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticeTestSectionResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PracticeTestAttemptId = table.Column<int>(type: "integer", nullable: false),
                    PracticeTestSectionId = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ScorePercent = table.Column<double>(type: "double precision", nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeTestSectionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeTestSectionResults_PracticeTestAttempts_PracticeTes~",
                        column: x => x.PracticeTestAttemptId,
                        principalTable: "PracticeTestAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PracticeTestSectionResults_PracticeTestSections_PracticeTes~",
                        column: x => x.PracticeTestSectionId,
                        principalTable: "PracticeTestSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrammarPointId = table.Column<int>(type: "integer", nullable: true),
                    KanjiEntryId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    VocabularyNoteEntryId = table.Column<int>(type: "integer", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EaseFactor = table.Column<double>(type: "double precision", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    NextReviewDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Repetitions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewItems_GrammarPoints_GrammarPointId",
                        column: x => x.GrammarPointId,
                        principalTable: "GrammarPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewItems_KanjiEntries_KanjiEntryId",
                        column: x => x.KanjiEntryId,
                        principalTable: "KanjiEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewItems_VocabularyNoteEntries_VocabularyNoteEntryId",
                        column: x => x.VocabularyNoteEntryId,
                        principalTable: "VocabularyNoteEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewItemId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Quality = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewLogs_ReviewItems_ReviewItemId",
                        column: x => x.ReviewItemId,
                        principalTable: "ReviewItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertificationLevels_CertificationId_Code",
                table: "CertificationLevels",
                columns: new[] { "CertificationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certifications_Name",
                table: "Certifications",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantAvailabilities_ConsultantId_StartTime_EndTime",
                table: "ConsultantAvailabilities",
                columns: new[] { "ConsultantId", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationAnswers_ConsultantId",
                table: "ConsultationAnswers",
                column: "ConsultantId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationAnswers_ConsultationRequestId",
                table: "ConsultationAnswers",
                column: "ConsultationRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationPurchases_ConsultationPackageId",
                table: "ConsultationPurchases",
                column: "ConsultationPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationPurchases_UserId",
                table: "ConsultationPurchases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRequests_ConsultantAvailabilityId",
                table: "ConsultationRequests",
                column: "ConsultantAvailabilityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRequests_ConsultantId_Status",
                table: "ConsultationRequests",
                columns: new[] { "ConsultantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRequests_ConsultationPurchaseId",
                table: "ConsultationRequests",
                column: "ConsultationPurchaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPoints_CertificationLevelId_Status",
                table: "GrammarPoints",
                columns: new[] { "CertificationLevelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPoints_ContentAuthorId",
                table: "GrammarPoints",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_KanjiEntries_CertificationLevelId_Status",
                table: "KanjiEntries",
                columns: new[] { "CertificationLevelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KanjiEntries_ContentAuthorId",
                table: "KanjiEntries",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathSuggestions_UserId_GeneratedAt",
                table: "LearningPathSuggestions",
                columns: new[] { "UserId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeTestAnswers_PracticeQuestionId",
                table: "PracticeTestAnswers",
                column: "PracticeQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeTestAnswers_PracticeTestAttemptId_PracticeQuestionId",
                table: "PracticeTestAnswers",
                columns: new[] { "PracticeTestAttemptId", "PracticeQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PracticeTestAnswers_SelectedOptionId",
                table: "PracticeTestAnswers",
                column: "SelectedOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeTestAttempts_PracticeTestId",
                table: "PracticeTestAttempts",
                column: "PracticeTestId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeTestAttempts_UserId_PracticeTestId",
                table: "PracticeTestAttempts",
                columns: new[] { "UserId", "PracticeTestId" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeTests_CertificationLevelId_Status",
                table: "PracticeTests",
                columns: new[] { "CertificationLevelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeTests_ContentAuthorId",
                table: "PracticeTests",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeTestSectionResults_PracticeTestAttemptId_PracticeTe~",
                table: "PracticeTestSectionResults",
                columns: new[] { "PracticeTestAttemptId", "PracticeTestSectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PracticeTestSectionResults_PracticeTestSectionId",
                table: "PracticeTestSectionResults",
                column: "PracticeTestSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeTestSections_PracticeTestId",
                table: "PracticeTestSections",
                column: "PracticeTestId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeTestSections_SkillId",
                table: "PracticeTestSections",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewItems_GrammarPointId",
                table: "ReviewItems",
                column: "GrammarPointId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewItems_KanjiEntryId",
                table: "ReviewItems",
                column: "KanjiEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewItems_UserId_NextReviewDate",
                table: "ReviewItems",
                columns: new[] { "UserId", "NextReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewItems_VocabularyNoteEntryId",
                table: "ReviewItems",
                column: "VocabularyNoteEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLogs_ReviewItemId",
                table: "ReviewLogs",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                table: "Skills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyEntries_CertificationLevelId_Status",
                table: "VocabularyEntries",
                columns: new[] { "CertificationLevelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyEntries_ContentAuthorId",
                table: "VocabularyEntries",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyLists_UserId_Name",
                table: "VocabularyLists",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyNoteEntries_KanjiEntryId",
                table: "VocabularyNoteEntries",
                column: "KanjiEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyNoteEntries_VocabularyEntryId",
                table: "VocabularyNoteEntries",
                column: "VocabularyEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyNoteEntries_VocabularyListId",
                table: "VocabularyNoteEntries",
                column: "VocabularyListId");

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationLevelSkills_CertificationLevels_CertificationL~",
                table: "CertificationLevelSkills",
                column: "CertificationLevelId",
                principalTable: "CertificationLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationLevelSkills_Skills_SkillId",
                table: "CertificationLevelSkills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LessonGrammars_GrammarPoints_GrammarPointId",
                table: "LessonGrammars",
                column: "GrammarPointId",
                principalTable: "GrammarPoints",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LessonKanjis_KanjiEntries_KanjiEntryId",
                table: "LessonKanjis",
                column: "KanjiEntryId",
                principalTable: "KanjiEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_CertificationLevels_CertificationLevelId",
                table: "Lessons",
                column: "CertificationLevelId",
                principalTable: "CertificationLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_Skills_SkillId",
                table: "Lessons",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LessonVocabularies_VocabularyEntries_VocabularyEntryId",
                table: "LessonVocabularies",
                column: "VocabularyEntryId",
                principalTable: "VocabularyEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeQuestions_PracticeTestSections_PracticeTestSectionId",
                table: "PracticeQuestions",
                column: "PracticeTestSectionId",
                principalTable: "PracticeTestSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeSubmissions_CertificationLevels_CertificationLevelId",
                table: "PracticeSubmissions",
                column: "CertificationLevelId",
                principalTable: "CertificationLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_CertificationLevels_TargetCertificationLevelId",
                table: "Users",
                column: "TargetCertificationLevelId",
                principalTable: "CertificationLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
