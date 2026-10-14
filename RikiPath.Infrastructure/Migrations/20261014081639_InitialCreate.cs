using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    AiGradingQuota = table.Column<int>(type: "integer", nullable: false),
                    MeetingSessionCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsPopular = table.Column<bool>(type: "boolean", nullable: false),
                    IsTrial = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
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
                name: "CertificateSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
                    PassingScore = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateSections_CertificateLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PasswordHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    PasswordSalt = table.Column<byte[]>(type: "bytea", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FirebaseUid = table.Column<string>(type: "text", nullable: true),
                    IsPhoneVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsEmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SystemNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DailyStudyMinutes = table.Column<int>(type: "integer", nullable: false),
                    TargetCertificationLevelId = table.Column<int>(type: "integer", nullable: true),
                    StudyTimePreference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                    LongestStreak = table.Column<int>(type: "integer", nullable: false),
                    LastStudyDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_CertificateLevels_TargetCertificationLevelId",
                        column: x => x.TargetCertificationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AiCreditTopUps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCreditTopUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiCreditTopUps_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    TargetEntityId = table.Column<int>(type: "integer", nullable: true),
                    RowKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    DiffSummaryJson = table.Column<string>(type: "text", nullable: true),
                    SourceRowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedByUserId = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedByName = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentApprovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentApprovalRequests_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailVerifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VerificationCode = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailVerifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrammarPatterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Structure = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UsageNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExampleSentence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExampleSentenceMeaning = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UserAccountId = table.Column<int>(type: "integer", nullable: true),
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
                        name: "FK_GrammarPatterns_Users_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KanaCharacters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Character = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Romaji = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StrokeCount = table.Column<int>(type: "integer", nullable: false),
                    StrokeOrderImageUrl = table.Column<string>(type: "text", nullable: true),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    ContentAuthorId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanaCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanaCharacters_Users_ContentAuthorId",
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
                    UserAccountId = table.Column<int>(type: "integer", nullable: true),
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
                        name: "FK_Kanjis_Users_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LearnerEssays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ImageStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalOcrText = table.Column<string>(type: "text", nullable: false),
                    ContentText = table.Column<string>(type: "text", nullable: false),
                    OcrLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerEssays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearnerEssays_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "Lessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    UserAccountId = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lessons_CertificateLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lessons_LanguageSkills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "LanguageSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lessons_Users_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MentorAvailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxLearners = table.Column<int>(type: "integer", nullable: false, defaultValue: 4),
                    BookedCount = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    AdminName = table.Column<string>(type: "text", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorAvailabilities", x => x.Id);
                    table.CheckConstraint("CK_MentorAvailability_BookedCount", "\"BookedCount\" >= 0 AND \"BookedCount\" <= \"MaxLearners\"");
                    table.ForeignKey(
                        name: "FK_MentorAvailabilities_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MockTestAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    TotalScore = table.Column<double>(type: "double precision", nullable: true),
                    UserAccountId = table.Column<int>(type: "integer", nullable: true),
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
                        name: "FK_MockTestAttempts_Users_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MockTestAttempts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    UserAccountId = table.Column<int>(type: "integer", nullable: true),
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
                        name: "FK_MockTests_Users_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    LinkUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticeSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TextContent = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeSubmissions_CertificateLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PracticeSubmissions_Users_UserId",
                        column: x => x.UserId,
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
                name: "UserSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionPlanId = table.Column<int>(type: "integer", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AiGradingQuota = table.Column<int>(type: "integer", nullable: false),
                    AiGradingUsedCount = table.Column<int>(type: "integer", nullable: false),
                    MeetingSessionsIncluded = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MeetingSessionsUsed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PaymentExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Users_UserId",
                        column: x => x.UserId,
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
                    UserAccountId = table.Column<int>(type: "integer", nullable: true),
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
                        name: "FK_Vocabularies_Users_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KanaWritingPracticeCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    KanaCharacterId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_KanaWritingPracticeCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanaWritingPracticeCards_KanaCharacters_KanaCharacterId",
                        column: x => x.KanaCharacterId,
                        principalTable: "KanaCharacters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KanaWritingPracticeCards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "LessonGrammars",
                columns: table => new
                {
                    LessonId = table.Column<int>(type: "integer", nullable: false),
                    GrammarPointId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonGrammars", x => new { x.LessonId, x.GrammarPointId });
                    table.ForeignKey(
                        name: "FK_LessonGrammars_GrammarPatterns_GrammarPointId",
                        column: x => x.GrammarPointId,
                        principalTable: "GrammarPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonGrammars_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LessonKanjis",
                columns: table => new
                {
                    LessonId = table.Column<int>(type: "integer", nullable: false),
                    KanjiEntryId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonKanjis", x => new { x.LessonId, x.KanjiEntryId });
                    table.ForeignKey(
                        name: "FK_LessonKanjis_Kanjis_KanjiEntryId",
                        column: x => x.KanjiEntryId,
                        principalTable: "Kanjis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonKanjis_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LessonProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LessonId = table.Column<int>(type: "integer", nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastPositionSeconds = table.Column<int>(type: "integer", nullable: false),
                    LastWatchedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonProgresses_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonProgresses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                });

            migrationBuilder.CreateTable(
                name: "MockTestSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    MockTestId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    CertificationSectionId = table.Column<int>(type: "integer", nullable: false),
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
                        name: "FK_MockTestSections_CertificateSections_CertificationSectionId",
                        column: x => x.CertificationSectionId,
                        principalTable: "CertificateSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MockTestSections_LanguageSkills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "LanguageSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MockTestSections_MockTests_MockTestId",
                        column: x => x.MockTestId,
                        principalTable: "MockTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorBookings_UserSubscriptions_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentLevelMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    KanjiId = table.Column<int>(type: "integer", nullable: true),
                    VocabularyId = table.Column<int>(type: "integer", nullable: true),
                    GrammarPatternId = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentLevelMappings", x => x.Id);
                    table.CheckConstraint("CK_ContentLevelMappings_ExactlyOneContent", "(CASE WHEN \"KanjiId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"VocabularyId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"GrammarPatternId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_ContentLevelMappings_CertificateLevels_CertificationLevelId",
                        column: x => x.CertificationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentLevelMappings_GrammarPatterns_GrammarPatternId",
                        column: x => x.GrammarPatternId,
                        principalTable: "GrammarPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentLevelMappings_Kanjis_KanjiId",
                        column: x => x.KanjiId,
                        principalTable: "Kanjis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentLevelMappings_Vocabularies_VocabularyId",
                        column: x => x.VocabularyId,
                        principalTable: "Vocabularies",
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
                name: "LessonVocabularies",
                columns: table => new
                {
                    LessonId = table.Column<int>(type: "integer", nullable: false),
                    VocabularyEntryId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonVocabularies", x => new { x.LessonId, x.VocabularyEntryId });
                    table.ForeignKey(
                        name: "FK_LessonVocabularies_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonVocabularies_Vocabularies_VocabularyEntryId",
                        column: x => x.VocabularyEntryId,
                        principalTable: "Vocabularies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KanaWritingPracticeHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KanaWritingPracticeCardId = table.Column<int>(type: "integer", nullable: false),
                    TotalMistakes = table.Column<int>(type: "integer", nullable: false),
                    Quality = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanaWritingPracticeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanaWritingPracticeHistories_KanaWritingPracticeCards_KanaW~",
                        column: x => x.KanaWritingPracticeCardId,
                        principalTable: "KanaWritingPracticeCards",
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
                name: "PracticeQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    PracticeExerciseId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeQuestions_PracticeExercises_PracticeExerciseId",
                        column: x => x.PracticeExerciseId,
                        principalTable: "PracticeExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    MockTestAttemptId = table.Column<int>(type: "integer", nullable: false),
                    MockTestSectionId = table.Column<int>(type: "integer", nullable: false),
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
                        name: "FK_MockTestSectionResults_MockTestAttempts_MockTestAttemptId",
                        column: x => x.MockTestAttemptId,
                        principalTable: "MockTestAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MockTestSectionResults_MockTestSections_MockTestSectionId",
                        column: x => x.MockTestSectionId,
                        principalTable: "MockTestSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AuthorRole = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
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
                        name: "FK_Notes_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
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
                    WritingScore = table.Column<int>(type: "integer", nullable: true),
                    WritingCorrectStrokeCount = table.Column<int>(type: "integer", nullable: true),
                    WritingTotalStrokeCount = table.Column<int>(type: "integer", nullable: true),
                    WritingPracticeMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    WritingScoreUpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                name: "GradingResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PracticeSubmissionId = table.Column<int>(type: "integer", nullable: true),
                    HomeworkSubmissionId = table.Column<int>(type: "integer", nullable: true),
                    OverallScore = table.Column<double>(type: "double precision", nullable: false),
                    FeedbackJson = table.Column<string>(type: "text", nullable: false),
                    GradedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradingResults_HomeworkSubmissions_HomeworkSubmissionId",
                        column: x => x.HomeworkSubmissionId,
                        principalTable: "HomeworkSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GradingResults_PracticeSubmissions_PracticeSubmissionId",
                        column: x => x.PracticeSubmissionId,
                        principalTable: "PracticeSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticeQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptionText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
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
                    table.PrimaryKey("PK_PracticeQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeQuestionOptions_PracticeQuestions_PracticeQuestionId",
                        column: x => x.PracticeQuestionId,
                        principalTable: "PracticeQuestions",
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

            migrationBuilder.InsertData(
                table: "CertificateTypes",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "IsActive", "IsDeleted", "ModifiedBy", "ModifiedDate", "Name", "SortOrder" },
                values: new object[] { 1, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese-Language Proficiency Test", true, false, null, null, "JLPT", 1 });

            migrationBuilder.InsertData(
                table: "Features",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "IsDeleted", "ModifiedBy", "ModifiedDate", "Name" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập lesson, từ vựng, ngữ pháp và kanji theo các chứng chỉ tiếng Nhật được cấu hình trong hệ thống.", false, null, null, "Thư viện học tiếng Nhật" },
                    { 2, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Luyện viết hiragana, katakana và kanji theo thứ tự nét, xem lại lỗi và tiến độ luyện tập.", false, null, null, "Luyện viết Kana và Kanji" },
                    { 3, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Làm bài luyện tập theo kỹ năng và cấp độ chứng chỉ tiếng Nhật.", false, null, null, "Bài luyện tập theo kỹ năng" },
                    { 4, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Luyện đề thi thử và theo dõi kết quả theo chứng chỉ tiếng Nhật có trong hệ thống.", false, null, null, "Đề thi thử chứng chỉ" },
                    { 5, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Xem tiến độ học lesson, kết quả luyện tập và lịch sử làm đề.", false, null, null, "Theo dõi tiến độ học" },
                    { 6, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sử dụng các chức năng AI hiện có để nhận gợi ý lộ trình và phản hồi bài làm tiếng Nhật.", false, null, null, "Hỗ trợ học tập bằng AI" },
                    { 7, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Gói bao gồm 12 buổi ôn luyện trực tiếp 1-1 với Mentor.", false, null, null, "12 buổi meeting 1-1 với Mentor" },
                    { 8, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Chọn lịch trống của Mentor; mỗi buổi đã đặt có phòng học trực tuyến realtime.", false, null, null, "Đặt lịch và phòng học trực tuyến" }
                });

            migrationBuilder.InsertData(
                table: "Kanjis",
                columns: new[] { "Id", "AudioUrl", "Character", "CreatedBy", "CreatedDate", "IsDeleted", "KunYomi", "Meaning", "ModifiedBy", "ModifiedDate", "OnYomi", "SinoVietnamese", "StrokeCount", "StrokeOrderImageUrl", "UserAccountId" },
                values: new object[,]
                {
                    { 910001, null, "日", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), false, "ひ、か", "Mặt trời; ngày", null, null, "ニチ、ジツ", "Nhật", 4, null, null },
                    { 910002, null, "月", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), false, "つき", "Mặt trăng; tháng", null, null, "ゲツ、ガツ", "Nguyệt", 4, null, null },
                    { 910003, null, "火", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), false, "ひ、ほ", "Lửa", null, null, "カ", "Hỏa", 4, null, null },
                    { 910004, null, "水", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), false, "みず", "Nước", null, null, "スイ", "Thủy", 4, null, null },
                    { 910005, null, "木", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), false, "き、こ", "Cây; gỗ", null, null, "モク、ボク", "Mộc", 4, null, null },
                    { 910006, null, "金", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), false, "かね、かな", "Vàng; tiền", null, null, "キン、コン", "Kim", 8, null, null },
                    { 910007, null, "土", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), false, "つち", "Đất", null, null, "ド、ト", "Thổ", 3, null, null },
                    { 910008, null, "山", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), false, "やま", "Núi", null, null, "サン", "Sơn", 3, null, null },
                    { 910009, null, "川", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), false, "かわ", "Sông", null, null, "セン", "Xuyên", 3, null, null },
                    { 910010, null, "人", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), false, "ひと", "Người", null, null, "ジン、ニン", "Nhân", 2, null, null }
                });

            migrationBuilder.InsertData(
                table: "LanguageSkills",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "IsDeleted", "ModifiedBy", "ModifiedDate", "Name" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese vocabulary learning skill.", false, null, null, "Vocabulary" },
                    { 2, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese kanji learning skill.", false, null, null, "Kanji" },
                    { 3, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese grammar learning skill.", false, null, null, "Grammar" },
                    { 4, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese listening comprehension skill.", false, null, null, "Listening" },
                    { 5, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese reading comprehension skill.", false, null, null, "Reading" }
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "AiGradingQuota", "CreatedBy", "CreatedDate", "Description", "DurationDays", "IsActive", "IsDeleted", "IsPopular", "IsTrial", "ModifiedBy", "ModifiedDate", "Name", "Price", "SortOrder" },
                values: new object[,]
                {
                    { 1, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 1 tuần.", 7, true, false, false, false, null, null, "Tự học tiếng Nhật - 1 tuần", 79000m, 6 },
                    { 2, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 2 tuần.", 14, true, false, false, false, null, null, "Tự học tiếng Nhật - 2 tuần", 119000m, 5 },
                    { 3, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 3 tuần.", 21, true, false, false, false, null, null, "Tự học tiếng Nhật - 3 tuần", 159000m, 4 },
                    { 4, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 1 tháng.", 30, true, false, true, false, null, null, "Tự học tiếng Nhật - 1 tháng", 199000m, 3 },
                    { 5, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 2 tháng.", 60, true, false, false, false, null, null, "Tự học tiếng Nhật - 2 tháng", 349000m, 2 },
                    { 6, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 3 tháng.", 90, true, false, false, false, null, null, "Tự học tiếng Nhật - 3 tháng", 459000m, 1 }
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "AiGradingQuota", "CreatedBy", "CreatedDate", "Description", "DurationDays", "IsActive", "IsDeleted", "IsPopular", "IsTrial", "MeetingSessionCount", "ModifiedBy", "ModifiedDate", "Name", "Price", "SortOrder" },
                values: new object[] { 7, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Gói demo gồm 12 buổi ôn luyện 1-1 với Mentor, thời hạn 90 ngày. Dữ liệu thanh toán demo được seed để kiểm thử đặt lịch trên frontend.", 90, true, false, false, false, 12, null, null, "Meeting 1-1 với Mentor - 12 buổi", 459000m, 7 });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarUrl", "CreatedBy", "CreatedDate", "CurrentStreak", "DailyStudyMinutes", "Email", "EmailNotificationsEnabled", "FirebaseUid", "FirstName", "IsDeleted", "IsEmailVerified", "IsPhoneVerified", "LastName", "LastStudyDate", "LongestStreak", "ModifiedBy", "ModifiedDate", "PasswordHash", "PasswordSalt", "PhoneNumber", "Role", "StudyTimePreference", "SystemNotificationsEnabled", "TargetCertificationLevelId" },
                values: new object[,]
                {
                    { 900001, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "admin@rikipath.local", true, null, "Admin", false, true, false, "System", null, 0, null, null, new byte[] { 91, 215, 217, 54, 239, 206, 181, 216, 143, 33, 82, 211, 9, 70, 197, 173, 197, 0, 72, 71, 59, 150, 46, 114, 96, 137, 52, 174, 137, 29, 196, 61, 50, 254, 249, 101, 114, 92, 149, 32, 55, 127, 205, 126, 216, 147, 197, 192, 248, 173, 158, 92, 104, 42, 195, 107, 150, 15, 3, 74, 103, 104, 3, 229 }, new byte[] { 61, 240, 150, 172, 239, 48, 224, 17, 192, 54, 50, 58, 91, 100, 55, 168, 18, 69, 137, 112, 110, 59, 180, 37, 201, 241, 143, 238, 139, 33, 128, 70, 87, 158, 235, 219, 229, 184, 94, 72, 41, 160, 160, 197, 158, 217, 128, 188, 156, 255, 147, 13, 151, 37, 133, 164, 9, 252, 175, 124, 21, 15, 152, 196 }, null, "Admin", null, true, null },
                    { 900002, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "mentor1@rikipath.local", true, null, "Mentor", false, true, false, "One", null, 0, null, null, new byte[] { 17, 253, 106, 25, 89, 176, 204, 31, 142, 252, 245, 31, 64, 101, 213, 135, 164, 116, 40, 46, 173, 175, 8, 0, 69, 101, 65, 4, 90, 25, 176, 223, 153, 237, 220, 94, 55, 133, 227, 181, 70, 97, 155, 122, 50, 139, 216, 136, 88, 52, 76, 98, 250, 64, 52, 71, 146, 221, 225, 137, 106, 6, 178, 239 }, new byte[] { 233, 77, 244, 252, 171, 97, 38, 140, 50, 138, 10, 54, 122, 165, 186, 85, 129, 82, 49, 10, 98, 44, 165, 57, 50, 7, 238, 166, 142, 224, 94, 49, 134, 226, 247, 110, 154, 139, 99, 204, 95, 39, 171, 235, 212, 26, 217, 87, 139, 118, 253, 125, 241, 255, 57, 22, 140, 171, 11, 168, 174, 202, 32, 17 }, null, "Mentor", null, true, null },
                    { 900003, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "mentor2@rikipath.local", true, null, "Mentor", false, true, false, "Two", null, 0, null, null, new byte[] { 93, 130, 109, 171, 238, 187, 245, 15, 202, 89, 247, 220, 95, 192, 246, 138, 5, 75, 250, 28, 208, 28, 51, 79, 127, 28, 214, 237, 93, 221, 108, 26, 152, 66, 73, 65, 11, 147, 154, 102, 113, 49, 51, 24, 51, 197, 248, 254, 22, 98, 87, 249, 250, 104, 131, 74, 16, 18, 179, 200, 3, 3, 79, 47 }, new byte[] { 240, 155, 254, 183, 129, 162, 74, 247, 41, 9, 67, 242, 128, 8, 226, 85, 5, 104, 104, 72, 101, 75, 130, 95, 47, 101, 116, 89, 207, 23, 119, 178, 38, 180, 185, 145, 58, 115, 14, 30, 85, 216, 169, 133, 94, 115, 11, 117, 96, 87, 227, 240, 204, 48, 51, 144, 170, 248, 167, 56, 175, 194, 253, 135 }, null, "Mentor", null, true, null },
                    { 900004, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "author1@rikipath.local", true, null, "Content", false, true, false, "Author One", null, 0, null, null, new byte[] { 168, 249, 82, 144, 178, 145, 94, 164, 34, 152, 2, 74, 204, 105, 84, 145, 96, 199, 153, 136, 110, 82, 114, 113, 22, 139, 117, 143, 50, 239, 197, 4, 51, 189, 137, 48, 175, 14, 104, 164, 218, 249, 148, 217, 33, 44, 202, 128, 237, 183, 110, 228, 185, 36, 209, 254, 183, 182, 55, 1, 78, 16, 234, 50 }, new byte[] { 19, 216, 84, 143, 3, 184, 50, 39, 209, 137, 149, 82, 224, 206, 6, 240, 237, 242, 172, 27, 55, 92, 242, 101, 149, 76, 104, 38, 176, 43, 40, 8, 102, 202, 146, 224, 62, 43, 147, 132, 2, 158, 81, 170, 29, 103, 76, 248, 101, 105, 213, 205, 32, 51, 59, 39, 221, 11, 135, 250, 193, 8, 78, 219 }, null, "ContentAuthor", null, true, null },
                    { 900005, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "author2@rikipath.local", true, null, "Content", false, true, false, "Author Two", null, 0, null, null, new byte[] { 189, 121, 9, 1, 230, 137, 43, 239, 79, 56, 195, 196, 66, 116, 42, 1, 137, 55, 54, 249, 55, 231, 79, 224, 182, 182, 199, 133, 100, 39, 138, 5, 223, 215, 48, 46, 60, 214, 49, 88, 42, 186, 232, 116, 237, 89, 55, 144, 4, 78, 215, 231, 237, 220, 96, 225, 202, 97, 61, 165, 246, 28, 126, 198 }, new byte[] { 66, 178, 77, 56, 234, 206, 29, 78, 136, 190, 160, 142, 226, 249, 14, 17, 26, 101, 167, 36, 193, 55, 150, 94, 239, 81, 69, 54, 81, 171, 80, 22, 208, 38, 242, 2, 55, 171, 64, 38, 48, 224, 36, 216, 133, 132, 147, 100, 174, 32, 65, 134, 128, 35, 50, 138, 204, 59, 167, 96, 11, 128, 101, 102 }, null, "ContentAuthor", null, true, null },
                    { 900006, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "learner.meeting@rikipath.local", true, null, "Meeting", false, true, false, "Test Learner", null, 0, null, null, new byte[] { 9, 206, 219, 58, 155, 234, 107, 145, 121, 174, 134, 51, 96, 253, 64, 216, 157, 114, 92, 36, 123, 206, 67, 217, 207, 166, 54, 71, 254, 251, 156, 200, 185, 110, 198, 135, 221, 146, 243, 40, 237, 229, 16, 133, 163, 161, 156, 79, 138, 103, 85, 25, 114, 81, 99, 182, 25, 73, 212, 198, 212, 192, 54, 129 }, new byte[] { 231, 202, 97, 95, 11, 9, 242, 47, 62, 77, 67, 36, 170, 78, 120, 81, 56, 69, 47, 36, 209, 212, 107, 244, 15, 34, 189, 110, 57, 66, 205, 213, 224, 19, 245, 192, 104, 146, 130, 44, 54, 107, 2, 15, 109, 136, 118, 220, 228, 44, 110, 219, 31, 100, 161, 164, 63, 231, 196, 79, 120, 106, 181, 71 }, null, "Learner", null, true, null },
                    { 900007, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "learner2.meeting@rikipath.local", true, null, "Linh", false, true, false, "Nguyen", null, 0, null, null, new byte[] { 217, 79, 50, 159, 85, 88, 67, 15, 23, 182, 103, 80, 220, 85, 86, 173, 204, 186, 57, 67, 183, 165, 253, 243, 150, 250, 150, 190, 122, 252, 55, 169, 85, 236, 69, 106, 19, 219, 244, 42, 239, 30, 189, 176, 52, 47, 30, 28, 125, 31, 162, 255, 61, 3, 238, 201, 168, 88, 161, 42, 216, 86, 172, 219 }, new byte[] { 85, 97, 108, 158, 219, 70, 81, 106, 73, 23, 205, 163, 105, 159, 117, 19, 175, 147, 178, 99, 112, 116, 163, 243, 109, 178, 129, 229, 245, 175, 91, 174, 66, 198, 122, 88, 80, 68, 160, 64, 85, 118, 105, 41, 135, 89, 49, 160, 120, 13, 116, 148, 255, 27, 157, 207, 106, 203, 127, 74, 19, 61, 148, 120 }, null, "Learner", null, true, null },
                    { 900008, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "learner3.meeting@rikipath.local", true, null, "Minh", false, true, false, "Tran", null, 0, null, null, new byte[] { 84, 97, 164, 196, 63, 151, 182, 39, 139, 242, 152, 22, 179, 33, 174, 113, 0, 236, 253, 114, 234, 242, 209, 76, 196, 162, 143, 195, 180, 3, 178, 193, 205, 230, 18, 114, 49, 151, 198, 217, 254, 21, 94, 176, 146, 214, 235, 122, 28, 241, 164, 161, 4, 161, 209, 38, 102, 179, 31, 107, 157, 25, 35, 63 }, new byte[] { 26, 188, 78, 205, 57, 115, 100, 129, 148, 135, 165, 205, 148, 203, 245, 36, 244, 177, 9, 104, 120, 140, 21, 164, 36, 75, 122, 51, 115, 69, 174, 180, 120, 155, 62, 37, 97, 227, 212, 134, 200, 7, 66, 255, 219, 10, 140, 149, 67, 86, 164, 170, 142, 165, 181, 103, 47, 100, 238, 24, 178, 29, 171, 226 }, null, "Learner", null, true, null },
                    { 900009, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "learner4.meeting@rikipath.local", true, null, "Hoang", false, true, false, "Pham", null, 0, null, null, new byte[] { 42, 30, 236, 129, 234, 192, 63, 84, 148, 163, 154, 246, 107, 147, 189, 95, 33, 141, 196, 67, 181, 51, 157, 136, 93, 228, 237, 165, 164, 49, 4, 146, 87, 84, 9, 47, 28, 59, 33, 130, 37, 63, 43, 229, 8, 178, 123, 74, 61, 159, 235, 68, 217, 51, 225, 29, 201, 104, 44, 152, 53, 255, 199, 28 }, new byte[] { 83, 214, 138, 3, 142, 91, 229, 97, 238, 61, 48, 237, 80, 82, 112, 249, 166, 81, 53, 95, 170, 120, 13, 87, 113, 159, 60, 37, 75, 229, 226, 107, 229, 89, 60, 57, 109, 196, 235, 81, 145, 154, 110, 120, 141, 13, 142, 77, 41, 132, 58, 80, 154, 57, 226, 168, 176, 251, 113, 167, 242, 139, 7, 177 }, null, "Learner", null, true, null }
                });

            migrationBuilder.InsertData(
                table: "CertificateLevels",
                columns: new[] { "Id", "CertificationId", "Code", "CreatedBy", "CreatedDate", "Description", "IsDeleted", "ModifiedBy", "ModifiedDate", "SortOrder" },
                values: new object[,]
                {
                    { 1, 1, "N5", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N5", false, null, null, 1 },
                    { 2, 1, "N4", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N4", false, null, null, 2 },
                    { 3, 1, "N3", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N3", false, null, null, 3 },
                    { 4, 1, "N2", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N2", false, null, null, 4 },
                    { 5, 1, "N1", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N1", false, null, null, 5 }
                });

            migrationBuilder.InsertData(
                table: "FeatureSubscriptionPlan",
                columns: new[] { "FeaturesId", "SubscriptionPlansId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 1, 2 },
                    { 1, 3 },
                    { 1, 4 },
                    { 1, 5 },
                    { 1, 6 },
                    { 2, 1 },
                    { 2, 2 },
                    { 2, 3 },
                    { 2, 4 },
                    { 2, 5 },
                    { 2, 6 },
                    { 3, 1 },
                    { 3, 2 },
                    { 3, 3 },
                    { 3, 4 },
                    { 3, 5 },
                    { 3, 6 },
                    { 4, 1 },
                    { 4, 2 },
                    { 4, 3 },
                    { 4, 4 },
                    { 4, 5 },
                    { 4, 6 },
                    { 5, 1 },
                    { 5, 2 },
                    { 5, 3 },
                    { 5, 4 },
                    { 5, 5 },
                    { 5, 6 },
                    { 6, 1 },
                    { 6, 2 },
                    { 6, 3 },
                    { 6, 4 },
                    { 6, 5 },
                    { 6, 6 },
                    { 7, 7 },
                    { 8, 7 }
                });

            migrationBuilder.InsertData(
                table: "MentorAvailabilities",
                columns: new[] { "Id", "AdminName", "BookedCount", "CreatedBy", "CreatedDate", "EndTime", "IsApproved", "IsDeleted", "MaxLearners", "MentorId", "ModifiedBy", "ModifiedDate", "RejectedAt", "RejectionReason", "RoomId", "StartTime" },
                values: new object[,]
                {
                    { 990001, "Seed Demo", 1, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 2, 10, 0, 0, 0, DateTimeKind.Unspecified), true, false, 4, 900002, null, null, null, null, new Guid("a1000000-0000-0000-0000-000000990001"), new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 990002, "Seed Demo", 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 3, 14, 0, 0, 0, DateTimeKind.Unspecified), true, false, 4, 900002, null, null, null, null, new Guid("a1000000-0000-0000-0000-000000990002"), new DateTime(2026, 10, 3, 13, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 990003, "Seed Demo", 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 4, 10, 0, 0, 0, DateTimeKind.Unspecified), true, false, 4, 900003, null, null, null, null, new Guid("a1000000-0000-0000-0000-000000990003"), new DateTime(2026, 10, 4, 9, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 990004, "Seed Demo", 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 5, 14, 0, 0, 0, DateTimeKind.Unspecified), true, false, 4, 900003, null, null, null, null, new Guid("a1000000-0000-0000-0000-000000990004"), new DateTime(2026, 10, 5, 13, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.InsertData(
                table: "UserSubscriptions",
                columns: new[] { "Id", "AiGradingQuota", "AiGradingUsedCount", "AmountPaid", "CreatedBy", "CreatedDate", "EndDate", "IsDeleted", "MeetingSessionsIncluded", "MeetingSessionsUsed", "ModifiedBy", "ModifiedDate", "PaymentExpiresAt", "PaymentStatus", "PaymentTransactionId", "PurchasedAt", "StartDate", "SubscriptionPlanId", "UserId" },
                values: new object[,]
                {
                    { 990001, 0, 0, 459000m, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Unspecified), false, 12, 1, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, 900006 },
                    { 990002, 0, 0, 459000m, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Unspecified), false, 12, 1, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS-2", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, 900007 },
                    { 990003, 0, 0, 459000m, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Unspecified), false, 12, 1, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS-3", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, 900008 },
                    { 990004, 0, 0, 459000m, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Unspecified), false, 12, 1, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS-4", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, 900009 }
                });

            migrationBuilder.InsertData(
                table: "ContentLevelMappings",
                columns: new[] { "Id", "CertificationLevelId", "CreatedBy", "CreatedDate", "GrammarPatternId", "IsDeleted", "KanjiId", "ModifiedBy", "ModifiedDate", "VocabularyId" },
                values: new object[,]
                {
                    { 920001, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910001, null, null, null },
                    { 920002, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910002, null, null, null },
                    { 920003, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910003, null, null, null },
                    { 920004, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910004, null, null, null },
                    { 920005, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910005, null, null, null },
                    { 920006, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910006, null, null, null },
                    { 920007, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910007, null, null, null },
                    { 920008, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910008, null, null, null },
                    { 920009, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910009, null, null, null },
                    { 920010, 1, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 910010, null, null, null }
                });

            migrationBuilder.InsertData(
                table: "MentorBookings",
                columns: new[] { "Id", "CompletedAt", "CreatedBy", "CreatedDate", "IsDeleted", "MeetingLink", "MentorAvailabilityId", "ModifiedBy", "ModifiedDate", "Question", "ScheduledAt", "Status", "UserSubscriptionId" },
                values: new object[,]
                {
                    { 990001, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", 990001, null, null, "Meeting demo để kiểm thử phòng họp trên frontend.", new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified), "Assigned", 990001 },
                    { 990002, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", 990001, null, null, "Em muốn nhờ Mentor giải đáp phần ngữ pháp N3 bài 5.", new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified), "Assigned", 990002 },
                    { 990003, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", 990001, null, null, "Hỏi về cách phân biệt giữa ~てくる và ~ていく.", new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified), "Assigned", 990003 },
                    { 990004, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", 990001, null, null, "Tư vấn phương pháp luyện nghe JLPT N3 hiệu quả.", new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified), "Assigned", 990004 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditTopUps_UserId_PaymentStatus",
                table: "AiCreditTopUps",
                columns: new[] { "UserId", "PaymentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateLevels_CertificationId_Code",
                table: "CertificateLevels",
                columns: new[] { "CertificationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateSections_CertificationLevelId_Code",
                table: "CertificateSections",
                columns: new[] { "CertificationLevelId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateTypes_Name",
                table: "CertificateTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentApprovalRequests_BatchId",
                table: "ContentApprovalRequests",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentApprovalRequests_EntityType_Status",
                table: "ContentApprovalRequests",
                columns: new[] { "EntityType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentApprovalRequests_SubmittedByUserId",
                table: "ContentApprovalRequests",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_CertificationLevelId_GrammarPatternId",
                table: "ContentLevelMappings",
                columns: new[] { "CertificationLevelId", "GrammarPatternId" },
                unique: true,
                filter: "\"GrammarPatternId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_CertificationLevelId_KanjiId",
                table: "ContentLevelMappings",
                columns: new[] { "CertificationLevelId", "KanjiId" },
                unique: true,
                filter: "\"KanjiId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_CertificationLevelId_VocabularyId",
                table: "ContentLevelMappings",
                columns: new[] { "CertificationLevelId", "VocabularyId" },
                unique: true,
                filter: "\"VocabularyId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_GrammarPatternId",
                table: "ContentLevelMappings",
                column: "GrammarPatternId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_KanjiId",
                table: "ContentLevelMappings",
                column: "KanjiId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLevelMappings_VocabularyId",
                table: "ContentLevelMappings",
                column: "VocabularyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerifications_UserId",
                table: "EmailVerifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureSubscriptionPlan_SubscriptionPlansId",
                table: "FeatureSubscriptionPlan",
                column: "SubscriptionPlansId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingResults_HomeworkSubmissionId",
                table: "GradingResults",
                column: "HomeworkSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradingResults_PracticeSubmissionId",
                table: "GradingResults",
                column: "PracticeSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPatterns_Title",
                table: "GrammarPatterns",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPatterns_UserAccountId",
                table: "GrammarPatterns",
                column: "UserAccountId");

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
                name: "IX_KanaCharacters_Character_Type",
                table: "KanaCharacters",
                columns: new[] { "Character", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KanaCharacters_ContentAuthorId",
                table: "KanaCharacters",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_KanaWritingPracticeCards_KanaCharacterId",
                table: "KanaWritingPracticeCards",
                column: "KanaCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_KanaWritingPracticeCards_UserId_KanaCharacterId",
                table: "KanaWritingPracticeCards",
                columns: new[] { "UserId", "KanaCharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KanaWritingPracticeCards_UserId_NextReviewDate",
                table: "KanaWritingPracticeCards",
                columns: new[] { "UserId", "NextReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_KanaWritingPracticeHistories_KanaWritingPracticeCardId",
                table: "KanaWritingPracticeHistories",
                column: "KanaWritingPracticeCardId");

            migrationBuilder.CreateIndex(
                name: "IX_Kanjis_Character",
                table: "Kanjis",
                column: "Character");

            migrationBuilder.CreateIndex(
                name: "IX_Kanjis_UserAccountId",
                table: "Kanjis",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageSkills_Name",
                table: "LanguageSkills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnerEssays_UserId_CreatedDate",
                table: "LearnerEssays",
                columns: new[] { "UserId", "CreatedDate" });

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
                name: "IX_LessonGrammars_GrammarPointId",
                table: "LessonGrammars",
                column: "GrammarPointId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonKanjis_KanjiEntryId",
                table: "LessonKanjis",
                column: "KanjiEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonProgresses_LessonId",
                table: "LessonProgresses",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonProgresses_UserId_LessonId",
                table: "LessonProgresses",
                columns: new[] { "UserId", "LessonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_CertificationLevelId_SkillId",
                table: "Lessons",
                columns: new[] { "CertificationLevelId", "SkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_SkillId",
                table: "Lessons",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_UserAccountId",
                table: "Lessons",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonVocabularies_VocabularyEntryId",
                table: "LessonVocabularies",
                column: "VocabularyEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_MentorId_StartTime_EndTime",
                table: "MentorAvailabilities",
                columns: new[] { "MentorId", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_RoomId",
                table: "MentorAvailabilities",
                column: "RoomId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_MentorAvailabilityId_UserSubscriptionId",
                table: "MentorBookings",
                columns: new[] { "MentorAvailabilityId", "UserSubscriptionId" },
                unique: true);

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
                name: "IX_MockTestAttempts_StartedAt",
                table: "MockTestAttempts",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestAttempts_UserAccountId",
                table: "MockTestAttempts",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestAttempts_UserId",
                table: "MockTestAttempts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTests_CertificationLevelId",
                table: "MockTests",
                column: "CertificationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTests_UserAccountId",
                table: "MockTests",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestSectionResults_MockTestAttemptId_MockTestSectionId",
                table: "MockTestSectionResults",
                columns: new[] { "MockTestAttemptId", "MockTestSectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MockTestSectionResults_MockTestSectionId",
                table: "MockTestSectionResults",
                column: "MockTestSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestSections_CertificationSectionId",
                table: "MockTestSections",
                column: "CertificationSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MockTestSections_MockTestId_CertificationSectionId",
                table: "MockTestSections",
                columns: new[] { "MockTestId", "CertificationSectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_MockTestSections_SkillId",
                table: "MockTestSections",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_MentorBookingId",
                table: "Notes",
                column: "MentorBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notes_UserId",
                table: "Notes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExerciseAttempts_PracticeExerciseId",
                table: "PracticeExerciseAttempts",
                column: "PracticeExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExerciseAttempts_UserId_PracticeExerciseId_StartedAt",
                table: "PracticeExerciseAttempts",
                columns: new[] { "UserId", "PracticeExerciseId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_LanguageSkillId",
                table: "PracticeExercises",
                column: "LanguageSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_LessonId_LanguageSkillId",
                table: "PracticeExercises",
                columns: new[] { "LessonId", "LanguageSkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeQuestionOptions_PracticeQuestionId",
                table: "PracticeQuestionOptions",
                column: "PracticeQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeQuestions_PracticeExerciseId",
                table: "PracticeQuestions",
                column: "PracticeExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSubmissions_CertificationLevelId",
                table: "PracticeSubmissions",
                column: "CertificationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSubmissions_UserId_Type_SubmittedAt",
                table: "PracticeSubmissions",
                columns: new[] { "UserId", "Type", "SubmittedAt" });

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
                name: "IX_ReviewCards_UserId_KanjiEntryId_Mode",
                table: "ReviewCards",
                columns: new[] { "UserId", "KanjiEntryId", "Mode" },
                unique: true);

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
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TargetCertificationLevelId",
                table: "Users",
                column: "TargetCertificationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_SubscriptionPlanId",
                table: "UserSubscriptions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId_PaymentStatus_EndDate",
                table: "UserSubscriptions",
                columns: new[] { "UserId", "PaymentStatus", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Vocabularies_UserAccountId",
                table: "Vocabularies",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Vocabularies_Word_Reading",
                table: "Vocabularies",
                columns: new[] { "Word", "Reading" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiCreditTopUps");

            migrationBuilder.DropTable(
                name: "ContentApprovalRequests");

            migrationBuilder.DropTable(
                name: "ContentLevelMappings");

            migrationBuilder.DropTable(
                name: "EmailVerifications");

            migrationBuilder.DropTable(
                name: "FeatureSubscriptionPlan");

            migrationBuilder.DropTable(
                name: "GradingResults");

            migrationBuilder.DropTable(
                name: "KanaWritingPracticeHistories");

            migrationBuilder.DropTable(
                name: "LearnerEssays");

            migrationBuilder.DropTable(
                name: "LearnerPracticeAnswers");

            migrationBuilder.DropTable(
                name: "LessonGrammars");

            migrationBuilder.DropTable(
                name: "LessonKanjis");

            migrationBuilder.DropTable(
                name: "LessonProgresses");

            migrationBuilder.DropTable(
                name: "LessonVocabularies");

            migrationBuilder.DropTable(
                name: "MockTestAnswers");

            migrationBuilder.DropTable(
                name: "MockTestSectionResults");

            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "RecommendedLearningPaths");

            migrationBuilder.DropTable(
                name: "ReviewHistories");

            migrationBuilder.DropTable(
                name: "Features");

            migrationBuilder.DropTable(
                name: "HomeworkSubmissions");

            migrationBuilder.DropTable(
                name: "PracticeSubmissions");

            migrationBuilder.DropTable(
                name: "KanaWritingPracticeCards");

            migrationBuilder.DropTable(
                name: "PracticeExerciseAttempts");

            migrationBuilder.DropTable(
                name: "PracticeQuestionOptions");

            migrationBuilder.DropTable(
                name: "MockQuestionOptions");

            migrationBuilder.DropTable(
                name: "MockTestAttempts");

            migrationBuilder.DropTable(
                name: "MentorBookings");

            migrationBuilder.DropTable(
                name: "ReviewCards");

            migrationBuilder.DropTable(
                name: "HomeworkAssignments");

            migrationBuilder.DropTable(
                name: "KanaCharacters");

            migrationBuilder.DropTable(
                name: "PracticeQuestions");

            migrationBuilder.DropTable(
                name: "MockQuestions");

            migrationBuilder.DropTable(
                name: "MentorAvailabilities");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "GrammarPatterns");

            migrationBuilder.DropTable(
                name: "LearnerNoteEntries");

            migrationBuilder.DropTable(
                name: "PracticeExercises");

            migrationBuilder.DropTable(
                name: "MockTestSections");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "Kanjis");

            migrationBuilder.DropTable(
                name: "LearnerNotes");

            migrationBuilder.DropTable(
                name: "Vocabularies");

            migrationBuilder.DropTable(
                name: "Lessons");

            migrationBuilder.DropTable(
                name: "CertificateSections");

            migrationBuilder.DropTable(
                name: "MockTests");

            migrationBuilder.DropTable(
                name: "LanguageSkills");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "CertificateLevels");

            migrationBuilder.DropTable(
                name: "CertificateTypes");
        }
    }
}
