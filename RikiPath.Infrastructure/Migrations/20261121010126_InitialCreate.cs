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
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                });

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
                    Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateTypes", x => x.Id);
                    table.CheckConstraint("CK_CertificateTypes_SortOrder", "\"SortOrder\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "ContentAiReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContentId = table.Column<int>(type: "integer", nullable: false),
                    Verdict = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    WarningCount = table.Column<int>(type: "integer", nullable: false),
                    IssuesJson = table.Column<string>(type: "text", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentAiReviews", x => x.Id);
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
                name: "JlptExamSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExamDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JlptExamSchedules", x => x.Id);
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
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanaCharacters", x => x.Id);
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
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
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
                    table.PrimaryKey("PK_Topics", x => x.Id);
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
                    Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateLevels", x => x.Id);
                    table.CheckConstraint("CK_CertificateLevels_SortOrder", "\"SortOrder\" >= 1");
                    table.ForeignKey(
                        name: "FK_CertificateLevels_CertificateTypes_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "CertificateTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "StudyCoursePlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionType = table.Column<string>(type: "text", nullable: false, defaultValue: "Individual"),
                    Capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    TargetExamDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false),
                    SessionsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    SessionMinutes = table.Column<int>(type: "integer", nullable: true),
                    TotalSessions = table.Column<int>(type: "integer", nullable: true),
                    RetakeClaimDays = table.Column<int>(type: "integer", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCoursePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCoursePlans_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: true),
                    PlanCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "self_study"),
                    IsPopular = table.Column<bool>(type: "boolean", nullable: false),
                    IsTrial = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                    table.CheckConstraint("CK_SubscriptionPlans_AiGradingQuota", "\"AiGradingQuota\" >= 0");
                    table.CheckConstraint("CK_SubscriptionPlans_DurationDays", "\"DurationDays\" >= 0");
                    table.CheckConstraint("CK_SubscriptionPlans_Price", "\"Price\" >= 0");
                    table.CheckConstraint("CK_SubscriptionPlans_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_SubscriptionPlans_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
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
                    PendingEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PendingEmailRequestedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FirebaseUid = table.Column<string>(type: "text", nullable: true),
                    GoogleSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsPhoneVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsEmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TokenVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    SystemNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationPreferencesJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DailyStudyMinutesMin = table.Column<int>(type: "integer", nullable: true),
                    DailyStudyMinutesMax = table.Column<int>(type: "integer", nullable: true),
                    DailyStudyMinutes = table.Column<int>(type: "integer", nullable: false),
                    TargetCertificationLevelId = table.Column<int>(type: "integer", nullable: true),
                    DeclaredJlptCertificateCode = table.Column<string>(type: "text", nullable: true),
                    StartingCertificateLevelId = table.Column<int>(type: "integer", nullable: true),
                    AssessedCertificateLevelId = table.Column<int>(type: "integer", nullable: true),
                    SelfDeclaredCertificateLevelId = table.Column<int>(type: "integer", nullable: true),
                    LearningGoalVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PlacementDecision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LearningMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetExamDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                        name: "FK_Users_CertificateLevels_AssessedCertificateLevelId",
                        column: x => x.AssessedCertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Users_CertificateLevels_SelfDeclaredCertificateLevelId",
                        column: x => x.SelfDeclaredCertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Users_CertificateLevels_StartingCertificateLevelId",
                        column: x => x.StartingCertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Users_CertificateLevels_TargetCertificationLevelId",
                        column: x => x.TargetCertificationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
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
                name: "JlptLevelPayCoefficients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    Coefficient = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfiguredByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JlptLevelPayCoefficients", x => x.Id);
                    table.CheckConstraint("CK_JlptLevelPayCoefficient_Positive", "\"Coefficient\" > 0");
                    table.CheckConstraint("CK_JlptLevelPayCoefficient_Range", "\"EffectiveUntilUtc\" IS NULL OR \"EffectiveUntilUtc\" > \"EffectiveFromUtc\"");
                    table.ForeignKey(
                        name: "FK_JlptLevelPayCoefficients_CertificateLevels_CertificateLevel~",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JlptLevelPayCoefficients_Users_ConfiguredByAdminId",
                        column: x => x.ConfiguredByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "Kanjis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
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
                name: "LearnerFeedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<int>(type: "integer", nullable: false),
                    TargetTitle = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerFeedback", x => x.Id);
                    table.CheckConstraint("CK_LearnerFeedback_Rating", "\"Rating\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_LearnerFeedback_Users_LearnerId",
                        column: x => x.LearnerId,
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
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: true),
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
                        name: "FK_Lessons_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lessons_Users_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MentorBankAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    AccountVersion = table.Column<int>(type: "integer", nullable: false),
                    BankCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountHolderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EncryptedAccountNumber = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MaskedAccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccountNumberHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorBankAccounts", x => x.Id);
                    table.CheckConstraint("CK_MentorBankAccount_Version", "\"AccountVersion\" >= 1");
                    table.ForeignKey(
                        name: "FK_MentorBankAccounts_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorBankAccounts_Users_VerifiedByAdminId",
                        column: x => x.VerifiedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "MentorCalendarConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    ProtectedRefreshToken = table.Column<string>(type: "text", nullable: true),
                    StateHash = table.Column<string>(type: "text", nullable: true),
                    StateExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SyncedEventsJson = table.Column<string>(type: "text", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorCalendarConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorCalendarConnections_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorGroupPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaxLearners = table.Column<int>(type: "integer", nullable: false),
                    EnrollmentCutoffMinutesBeforeFirstSession = table.Column<int>(type: "integer", nullable: false),
                    SessionMinutes = table.Column<int>(type: "integer", nullable: false),
                    RestMinutes = table.Column<int>(type: "integer", nullable: false),
                    DailySessionCap = table.Column<int>(type: "integer", nullable: false),
                    WeeklySessionCap = table.Column<int>(type: "integer", nullable: false),
                    PublicationHorizonDays = table.Column<int>(type: "integer", nullable: false),
                    CancellationLeadHours = table.Column<int>(type: "integer", nullable: false),
                    ReschedulingLeadHours = table.Column<int>(type: "integer", nullable: false),
                    HomeworkDeadlineDays = table.Column<int>(type: "integer", nullable: false),
                    PlacementAttemptLimit = table.Column<int>(type: "integer", nullable: false),
                    RemediationCompletionThresholdPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RemediationThresholdComparison = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    RequireOfficialExamEvidence = table.Column<bool>(type: "boolean", nullable: false),
                    RequireAdminRemediationReview = table.Column<bool>(type: "boolean", nullable: false),
                    KpiReportingPeriodDays = table.Column<int>(type: "integer", nullable: false),
                    LearnerTransferEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TransferAllowedAfterSourceStart = table.Column<bool>(type: "boolean", nullable: false),
                    MentorReplacementEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MentorReplacementLeadHours = table.Column<int>(type: "integer", nullable: false),
                    RecurringWeekdayPatternsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CourseDurationWeeks = table.Column<int>(type: "integer", nullable: false),
                    AllowedSessionsPerWeekCsv = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefaultSessionsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    CheckoutHoldMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinLearnersToStart = table.Column<int>(type: "integer", nullable: false),
                    CreatedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorGroupPolicies", x => x.Id);
                    table.CheckConstraint("CK_MentorGroupPolicy_AllowedSessionsPerWeek", "\"AllowedSessionsPerWeekCsv\" ~ '^[1-7](,[1-7]){0,6}$'");
                    table.CheckConstraint("CK_MentorGroupPolicy_Capacity", "\"MaxLearners\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_MentorGroupPolicy_CheckoutHold", "\"CheckoutHoldMinutes\" BETWEEN 1 AND 60");
                    table.CheckConstraint("CK_MentorGroupPolicy_Comparison", "\"RemediationThresholdComparison\" IN ('>', '>=')");
                    table.CheckConstraint("CK_MentorGroupPolicy_CourseDurationWeeks", "\"CourseDurationWeeks\" BETWEEN 1 AND 52");
                    table.CheckConstraint("CK_MentorGroupPolicy_DefaultSessionsPerWeek", "\"DefaultSessionsPerWeek\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_MentorGroupPolicy_Evidence", "\"RequireOfficialExamEvidence\" AND \"RequireAdminRemediationReview\"");
                    table.CheckConstraint("CK_MentorGroupPolicy_MinLearners", "\"MinLearnersToStart\" BETWEEN 1 AND \"MaxLearners\"");
                    table.CheckConstraint("CK_MentorGroupPolicy_Positive", "\"Version\" > 0 AND \"SessionMinutes\" > 0 AND \"RestMinutes\" >= 0 AND \"DailySessionCap\" > 0 AND \"PublicationHorizonDays\" > 0 AND \"EnrollmentCutoffMinutesBeforeFirstSession\" >= 0 AND \"CancellationLeadHours\" >= 0 AND \"ReschedulingLeadHours\" >= 0 AND \"HomeworkDeadlineDays\" > 0 AND \"PlacementAttemptLimit\" > 0 AND \"KpiReportingPeriodDays\" > 0");
                    table.CheckConstraint("CK_MentorGroupPolicy_Remediation", "\"RemediationCompletionThresholdPercent\" > 0 AND \"RemediationCompletionThresholdPercent\" <= 100");
                    table.CheckConstraint("CK_MentorGroupPolicy_ReplacementLead", "\"MentorReplacementLeadHours\" BETWEEN 0 AND 720");
                    table.CheckConstraint("CK_MentorGroupPolicy_TransferAfterStart", "\"LearnerTransferEnabled\" OR NOT \"TransferAllowedAfterSourceStart\"");
                    table.CheckConstraint("CK_MentorGroupPolicy_WeeklySessionCap", "\"WeeklySessionCap\" BETWEEN 0 AND 84");
                    table.ForeignKey(
                        name: "FK_MentorGroupPolicies_Users_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorHourlyRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    AmountPerHour = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfiguredByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorHourlyRates", x => x.Id);
                    table.CheckConstraint("CK_MentorHourlyRate_Positive", "\"AmountPerHour\" > 0");
                    table.CheckConstraint("CK_MentorHourlyRate_Range", "\"EffectiveUntilUtc\" IS NULL OR \"EffectiveUntilUtc\" > \"EffectiveFromUtc\"");
                    table.ForeignKey(
                        name: "FK_MentorHourlyRates_Users_ConfiguredByAdminId",
                        column: x => x.ConfiguredByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorHourlyRates_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorPayrollPeriods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    SupplementalReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TotalEarningsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAdjustmentsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossPayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreparedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    PreparedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPayrollPeriods", x => x.Id);
                    table.CheckConstraint("CK_MentorPayrollPeriod_Payable", "\"TotalEarningsAmount\" >= 0 AND \"GrossPayableAmount\" >= 0");
                    table.CheckConstraint("CK_MentorPayrollPeriod_Period", "\"Year\" >= 2020 AND \"Year\" <= 2100 AND \"Month\" >= 1 AND \"Month\" <= 12 AND \"SequenceNumber\" >= 1");
                    table.CheckConstraint("CK_MentorPayrollPeriod_Supplemental", "\"SequenceNumber\" = 1 OR (\"SupplementalReason\" IS NOT NULL AND length(trim(\"SupplementalReason\")) > 0)");
                    table.ForeignKey(
                        name: "FK_MentorPayrollPeriods_Users_ApprovedByAdminId",
                        column: x => x.ApprovedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayrollPeriods_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayrollPeriods_Users_PreparedByAdminId",
                        column: x => x.PreparedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorProfiles",
                columns: table => new
                {
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Expertise = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    JlptLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: true),
                    Workplace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VerificationDocumentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IntroductionVideoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VerificationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByAdminName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorProfiles", x => x.MentorId);
                    table.ForeignKey(
                        name: "FK_MentorProfiles_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "MentorTeachingQualifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorTeachingQualifications", x => x.Id);
                    table.CheckConstraint("CK_MentorTeachingQualification_Revoked", "\"RevokedAtUtc\" IS NULL OR \"RevokedAtUtc\" >= \"VerifiedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_MentorTeachingQualifications_CertificateLevels_CertificateL~",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorTeachingQualifications_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorTeachingQualifications_Users_VerifiedByAdminId",
                        column: x => x.VerifiedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorWeeklyAvailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Period = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorWeeklyAvailabilities", x => x.Id);
                    table.CheckConstraint("CK_MentorWeeklyAvailability_Period", "\"Period\" IN ('Morning', 'Afternoon', 'Evening')");
                    table.ForeignKey(
                        name: "FK_MentorWeeklyAvailabilities_Users_MentorId",
                        column: x => x.MentorId,
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
                    AttemptKind = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    TotalScore = table.Column<double>(type: "double precision", nullable: true),
                    IsPlacementAssessment = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    QuestionSnapshotJson = table.Column<string>(type: "text", nullable: true),
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
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeliveredTokens = table.Column<string>(type: "text", nullable: true),
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
                name: "OrganizationPayoutAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BankCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountHolderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MaskedAccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderAccountReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ConfiguredByAdminId = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationPayoutAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationPayoutAccounts_Users_ConfiguredByAdminId",
                        column: x => x.ConfiguredByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartingCertificateLevelId = table.Column<int>(type: "integer", nullable: true),
                    TargetCertificateLevelId = table.Column<int>(type: "integer", nullable: true),
                    BaseActivePathId = table.Column<int>(type: "integer", nullable: true),
                    PreviewExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                name: "UserDeviceTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FcmToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDeviceTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDeviceTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vocabularies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
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
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
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
                name: "StudyCohorts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    CapacitySnapshot = table.Column<int>(type: "integer", nullable: true),
                    GroupPolicyId = table.Column<int>(type: "integer", nullable: true),
                    PolicyVersionSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCohorts", x => x.Id);
                    table.CheckConstraint("CK_StudyCohort_Capacity", "\"Capacity\" > 0");
                    table.ForeignKey(
                        name: "FK_StudyCohorts_MentorGroupPolicies_GroupPolicyId",
                        column: x => x.GroupPolicyId,
                        principalTable: "MentorGroupPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohorts_StudyCoursePlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "StudyCoursePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohorts_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorPayrollAdjustments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollPeriodId = table.Column<long>(type: "bigint", nullable: false),
                    RelatedEarningEntryId = table.Column<long>(type: "bigint", nullable: true),
                    RelatedBookingId = table.Column<int>(type: "integer", nullable: true),
                    SignedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OperationKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ApprovedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPayrollAdjustments", x => x.Id);
                    table.CheckConstraint("CK_MentorPayrollAdjustment_NonZero", "\"SignedAmount\" <> 0");
                    table.ForeignKey(
                        name: "FK_MentorPayrollAdjustments_MentorPayrollPeriods_PayrollPeriod~",
                        column: x => x.PayrollPeriodId,
                        principalTable: "MentorPayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayrollAdjustments_Users_ApprovedByAdminId",
                        column: x => x.ApprovedByAdminId,
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
                name: "MentorPayoutObligations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollPeriodId = table.Column<long>(type: "bigint", nullable: false),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    LockedBankAccountId = table.Column<long>(type: "bigint", nullable: true),
                    OrganizationPayoutAccountId = table.Column<long>(type: "bigint", nullable: true),
                    PayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SelectedMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActiveAttemptId = table.Column<long>(type: "bigint", nullable: true),
                    SettledAttemptId = table.Column<long>(type: "bigint", nullable: true),
                    SettledReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPayoutObligations", x => x.Id);
                    table.CheckConstraint("CK_MentorPayoutObligation_Positive", "\"PayableAmount\" > 0");
                    table.CheckConstraint("CK_MentorPayoutObligation_Settlement", "(\"Status\" <> 'Paid') OR (\"SettledAttemptId\" IS NOT NULL AND \"SettledReference\" IS NOT NULL AND \"SettledAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_MentorPayoutObligations_MentorBankAccounts_LockedBankAccoun~",
                        column: x => x.LockedBankAccountId,
                        principalTable: "MentorBankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutObligations_MentorPayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "MentorPayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutObligations_OrganizationPayoutAccounts_Organiza~",
                        column: x => x.OrganizationPayoutAccountId,
                        principalTable: "OrganizationPayoutAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutObligations_Users_ApprovedByAdminId",
                        column: x => x.ApprovedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutObligations_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
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
                name: "PracticeQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
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
                name: "CohortEnrollmentEntitlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudyCohortId = table.Column<int>(type: "integer", nullable: false),
                    PlanId = table.Column<int>(type: "integer", nullable: true),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    EntitlementReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GrantedById = table.Column<int>(type: "integer", nullable: true),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConsumedByCourseId = table.Column<int>(type: "integer", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RevokedById = table.Column<int>(type: "integer", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortEnrollmentEntitlements", x => x.Id);
                    table.CheckConstraint("CK_CohortEnrollmentEntitlement_NoCredit", "\"SourceType\" NOT IN ('ConsultationCredit', 'Credit1on1', 'OneOnOneCredit')");
                    table.CheckConstraint("CK_CohortEnrollmentEntitlement_Status", "\"Status\" IN ('Active', 'Consumed', 'Revoked', 'Expired')");
                    table.ForeignKey(
                        name: "FK_CohortEnrollmentEntitlements_StudyCohorts_StudyCohortId",
                        column: x => x.StudyCohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortEnrollmentEntitlements_StudyCoursePlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "StudyCoursePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortEnrollmentEntitlements_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortScheduleRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CohortId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ShiftWeeks = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedById = table.Column<int>(type: "integer", nullable: false),
                    RequestedByRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CohortRevisionSnapshot = table.Column<int>(type: "integer", nullable: false),
                    ImpactSnapshot = table.Column<string>(type: "text", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortScheduleRequests", x => x.Id);
                    table.CheckConstraint("CK_CohortScheduleRequest_Decision", "(\"Status\" NOT IN ('Approved', 'Rejected') OR (\"ReviewedById\" IS NOT NULL AND \"ReviewedAtUtc\" IS NOT NULL)) AND (\"Status\" <> 'Rejected' OR (\"DecisionNote\" IS NOT NULL AND length(btrim(\"DecisionNote\")) > 0))");
                    table.CheckConstraint("CK_CohortScheduleRequest_Kind", "\"Kind\" IN ('ShiftWeeks', 'CancelEmptyCohort')");
                    table.CheckConstraint("CK_CohortScheduleRequest_Reason", "length(btrim(\"Reason\")) BETWEEN 10 AND 1000");
                    table.CheckConstraint("CK_CohortScheduleRequest_ShiftWeeks", "(\"Kind\" = 'ShiftWeeks' AND \"ShiftWeeks\" BETWEEN 1 AND 8) OR (\"Kind\" = 'CancelEmptyCohort' AND \"ShiftWeeks\" IS NULL)");
                    table.CheckConstraint("CK_CohortScheduleRequest_Status", "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Withdrawn')");
                    table.ForeignKey(
                        name: "FK_CohortScheduleRequests_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortScheduleRequests_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortScheduleRequests_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyCohortMentorAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CohortId = table.Column<int>(type: "integer", nullable: false),
                    PreviousMentorId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromSessionNumber = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AffectedSessionCount = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AssignedById = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<int>(type: "integer", nullable: true),
                    PolicyVersionSnapshot = table.Column<int>(type: "integer", nullable: true),
                    ImpactSnapshot = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCohortMentorAssignments", x => x.Id);
                    table.CheckConstraint("CK_StudyCohortMentorAssignment_DifferentMentor", "\"MentorId\" <> \"PreviousMentorId\"");
                    table.CheckConstraint("CK_StudyCohortMentorAssignment_Reason", "length(btrim(\"Reason\")) > 0");
                    table.CheckConstraint("CK_StudyCohortMentorAssignment_Sessions", "\"EffectiveFromSessionNumber\" > 0 AND \"AffectedSessionCount\" > 0");
                    table.ForeignKey(
                        name: "FK_StudyCohortMentorAssignments_MentorGroupPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "MentorGroupPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohortMentorAssignments_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohortMentorAssignments_Users_AssignedById",
                        column: x => x.AssignedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohortMentorAssignments_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohortMentorAssignments_Users_PreviousMentorId",
                        column: x => x.PreviousMentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CohortId = table.Column<int>(type: "integer", nullable: true),
                    SessionType = table.Column<string>(type: "text", nullable: false, defaultValue: "Individual"),
                    TargetExamDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    PurchaseKey = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: true),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    RetakeOfId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    PlanRevision = table.Column<int>(type: "integer", nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false),
                    SessionsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    SessionMinutes = table.Column<int>(type: "integer", nullable: false),
                    TotalSessions = table.Column<int>(type: "integer", nullable: false),
                    RetakeClaimDays = table.Column<int>(type: "integer", nullable: true),
                    MinimumHomeworkPercent = table.Column<int>(type: "integer", nullable: false),
                    MaximumAbsencePercent = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaymentStatus = table.Column<string>(type: "text", nullable: false),
                    PaymentReference = table.Column<string>(type: "text", nullable: true),
                    OrderSnapshot = table.Column<string>(type: "text", nullable: true),
                    ProviderPaymentReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PaymentConfirmedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaymentReviewReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "text", nullable: true),
                    PaymentExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GroupPolicyId = table.Column<int>(type: "integer", nullable: true),
                    PolicyVersionSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CapacitySnapshot = table.Column<int>(type: "integer", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TransferredFromCourseId = table.Column<int>(type: "integer", nullable: true),
                    TransferredAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCourses_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_MentorGroupPolicies_GroupPolicyId",
                        column: x => x.GroupPolicyId,
                        principalTable: "MentorGroupPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_StudyCoursePlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "StudyCoursePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_StudyCourses_RetakeOfId",
                        column: x => x.RetakeOfId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_StudyCourses_TransferredFromCourseId",
                        column: x => x.TransferredFromCourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourses_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorAvailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorWeeklyAvailabilityId = table.Column<int>(type: "integer", nullable: true),
                    MentorScheduleRuleId = table.Column<long>(type: "bigint", nullable: true),
                    GenerationKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    GroupPolicyId = table.Column<int>(type: "integer", nullable: true),
                    SessionMinutesSnapshot = table.Column<int>(type: "integer", nullable: true),
                    RestMinutesSnapshot = table.Column<int>(type: "integer", nullable: true),
                    LocalBusinessDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ChangeRequestReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ChangeRequestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ChangeResolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceTrackingEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MaxLearners = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    BookedCount = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    AdminName = table.Column<string>(type: "text", nullable: true),
                    CancelRequestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "text", nullable: true),
                    CancelRejectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelRejectionReason = table.Column<string>(type: "text", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorAvailabilities", x => x.Id);
                    table.UniqueConstraint("AK_MentorAvailabilities_Id_MentorId", x => new { x.Id, x.MentorId });
                    table.CheckConstraint("CK_MentorAvailability_BookedCount", "\"BookedCount\" >= 0 AND \"BookedCount\" <= \"MaxLearners\"");
                    table.CheckConstraint("CK_MentorAvailability_GeneratedSlot", "(\"GenerationKey\" IS NULL AND \"PolicyVersion\" IS NULL AND \"MentorScheduleRuleId\" IS NULL AND \"LocalBusinessDate\" IS NULL AND \"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL) OR (\"GenerationKey\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"PolicyVersion\" > 0 AND \"MentorScheduleRuleId\" IS NOT NULL AND \"LocalBusinessDate\" IS NOT NULL AND ((\"GroupPolicyId\" IS NULL AND \"SessionMinutesSnapshot\" IS NULL AND \"RestMinutesSnapshot\" IS NULL AND \"MaxLearners\" = 1 AND \"EndTime\" = \"StartTime\" + INTERVAL '90 minutes') OR (\"GroupPolicyId\" IS NOT NULL AND \"SessionMinutesSnapshot\" > 0 AND \"RestMinutesSnapshot\" >= 0 AND \"MaxLearners\" BETWEEN 1 AND 4 AND \"EndTime\" = \"StartTime\" + \"SessionMinutesSnapshot\" * INTERVAL '1 minute')))");
                    table.ForeignKey(
                        name: "FK_MentorAvailabilities_MentorGroupPolicies_GroupPolicyId",
                        column: x => x.GroupPolicyId,
                        principalTable: "MentorGroupPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorAvailabilities_MentorScheduleRules_MentorScheduleRule~",
                        column: x => x.MentorScheduleRuleId,
                        principalTable: "MentorScheduleRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorAvailabilities_MentorWeeklyAvailabilities_MentorWeekl~",
                        column: x => x.MentorWeeklyAvailabilityId,
                        principalTable: "MentorWeeklyAvailabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorAvailabilities_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
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
                    QuestionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "SingleChoice"),
                    InteractionDataJson = table.Column<string>(type: "jsonb", nullable: true),
                    AnswerKeyJson = table.Column<string>(type: "jsonb", nullable: true),
                    AudioUrl = table.Column<string>(type: "text", nullable: true),
                    AudioTranscript = table.Column<string>(type: "text", nullable: true),
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
                name: "MentorPayoutAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayoutObligationId = table.Column<long>(type: "bigint", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    ExecutionMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BatchId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LockedBankAccountId = table.Column<long>(type: "bigint", nullable: true),
                    InternalStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderPayoutId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TransferReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EvidenceStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EvidenceNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InitiatedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPayoutAttempts", x => x.Id);
                    table.CheckConstraint("CK_MentorPayoutAttempt_ManualEvidence", "(\"ExecutionMethod\" <> 'ManualBankTransfer' OR \"InternalStatus\" <> 'Succeeded') OR (\"TransferReference\" IS NOT NULL AND \"EvidenceStorageKey\" IS NOT NULL)");
                    table.CheckConstraint("CK_MentorPayoutAttempt_Positive", "\"Amount\" > 0 AND \"AttemptNumber\" >= 1");
                    table.ForeignKey(
                        name: "FK_MentorPayoutAttempts_MentorBankAccounts_LockedBankAccountId",
                        column: x => x.LockedBankAccountId,
                        principalTable: "MentorBankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutAttempts_MentorPayoutObligations_PayoutObligati~",
                        column: x => x.PayoutObligationId,
                        principalTable: "MentorPayoutObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutAttempts_Users_ConfirmedByAdminId",
                        column: x => x.ConfirmedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutAttempts_Users_InitiatedByAdminId",
                        column: x => x.InitiatedByAdminId,
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
                name: "PracticeQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
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
                name: "CohortScheduleRequestEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortScheduleRequestEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CohortScheduleRequestEvents_CohortScheduleRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "CohortScheduleRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortComplaints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    CohortId = table.Column<int>(type: "integer", nullable: true),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RefundReviewRequested = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MentorResponse = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MentorRespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Decision = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DecidedById = table.Column<int>(type: "integer", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortComplaints", x => x.Id);
                    table.CheckConstraint("CK_CohortComplaint_Category", "\"Category\" IN ('TeachingQuality', 'MentorConduct', 'Schedule', 'PaymentEnrollment', 'Other')");
                    table.CheckConstraint("CK_CohortComplaint_Decision", "(\"Status\" NOT IN ('Resolved', 'Rejected') OR (\"Decision\" IS NOT NULL AND length(btrim(\"Decision\")) > 0 AND \"DecidedById\" IS NOT NULL AND \"DecidedAtUtc\" IS NOT NULL))");
                    table.CheckConstraint("CK_CohortComplaint_Message", "length(btrim(\"Message\")) BETWEEN 10 AND 2000");
                    table.CheckConstraint("CK_CohortComplaint_RefundReview", "NOT \"RefundReviewRequested\" OR \"Category\" = 'PaymentEnrollment'");
                    table.CheckConstraint("CK_CohortComplaint_Status", "\"Status\" IN ('Open', 'UnderReview', 'Resolved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_CohortComplaints_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortComplaints_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortComplaints_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortComplaints_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortComplaints_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortMentorReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    CohortId = table.Column<int>(type: "integer", nullable: true),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortMentorReviews", x => x.Id);
                    table.CheckConstraint("CK_CohortMentorReview_Rating", "\"Rating\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_CohortMentorReviews_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortMentorReviews_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortMentorReviews_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortMentorReviews_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortTransferRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    SourceCourseId = table.Column<int>(type: "integer", nullable: false),
                    SourceCohortId = table.Column<int>(type: "integer", nullable: false),
                    RequestedTargetCohortId = table.Column<int>(type: "integer", nullable: true),
                    TargetCohortId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<int>(type: "integer", nullable: true),
                    PolicyVersionSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DestinationCourseId = table.Column<int>(type: "integer", nullable: true),
                    EligibilitySnapshot = table.Column<string>(type: "text", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortTransferRequests", x => x.Id);
                    table.CheckConstraint("CK_CohortTransferRequest_Decision", "(\"Status\" <> 'Approved' OR (\"TargetCohortId\" IS NOT NULL AND \"DestinationCourseId\" IS NOT NULL AND \"ReviewedById\" IS NOT NULL)) AND (\"Status\" <> 'Rejected' OR (\"DecisionNote\" IS NOT NULL AND length(btrim(\"DecisionNote\")) > 0 AND \"ReviewedById\" IS NOT NULL))");
                    table.CheckConstraint("CK_CohortTransferRequest_Status", "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Withdrawn')");
                    table.CheckConstraint("CK_CohortTransferRequest_Target", "\"TargetCohortId\" IS NULL OR \"TargetCohortId\" <> \"SourceCohortId\"");
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_MentorGroupPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "MentorGroupPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_StudyCohorts_RequestedTargetCohortId",
                        column: x => x.RequestedTargetCohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_StudyCohorts_SourceCohortId",
                        column: x => x.SourceCohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_StudyCohorts_TargetCohortId",
                        column: x => x.TargetCohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_StudyCourses_DestinationCourseId",
                        column: x => x.DestinationCourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_StudyCourses_SourceCourseId",
                        column: x => x.SourceCourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequests_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseAuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    At = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseAuditEntries_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseRetakeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    ExamDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ResultPublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EvidenceUrl = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Response = table.Column<string>(type: "text", nullable: true),
                    EligibilitySnapshot = table.Column<string>(type: "text", nullable: true),
                    MentorSupportPlan = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MentorSupportPlanById = table.Column<int>(type: "integer", nullable: true),
                    MentorSupportPlanAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseRetakeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseRetakeRequests_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseScheduleRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    RequestedById = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Response = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseScheduleRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseScheduleRequests_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyCourseHomework",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Submission = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Feedback = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCourseHomework", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCourseHomework_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyCohortSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CohortId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    AvailabilityId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCohortSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCohortSessions_MentorAvailabilities_AvailabilityId",
                        column: x => x.AvailabilityId,
                        principalTable: "MentorAvailabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCohortSessions_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyCourseSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AvailabilityId = table.Column<int>(type: "integer", nullable: true),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MeetingUrl = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConfirmedById = table.Column<int>(type: "integer", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCourseSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyCourseSessions_MentorAvailabilities_AvailabilityId",
                        column: x => x.AvailabilityId,
                        principalTable: "MentorAvailabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyCourseSessions_StudyCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionPlanId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: true),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: true),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AiGradingQuota = table.Column<int>(type: "integer", nullable: false),
                    AiGradingUsedCount = table.Column<int>(type: "integer", nullable: false),
                    DurationDaysSnapshot = table.Column<int>(type: "integer", nullable: true),
                    PlanNameSnapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PlanCategorySnapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PlanRevisionSnapshot = table.Column<int>(type: "integer", nullable: true),
                    MeetingSessionsIncluded = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MeetingSessionsUsed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MentorPracticeOutcomeJson = table.Column<string>(type: "text", nullable: true),
                    MentorReviewPlan = table.Column<string>(type: "character varying(6000)", maxLength: 6000, nullable: true),
                    MentorReviewSessionAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MentorReviewCompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaymentExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PendingMentorAvailabilityId = table.Column<int>(type: "integer", nullable: true),
                    PendingMentorQuestion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                        name: "FK_UserSubscriptions_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_MentorAvailabilities_PendingMentorAvailab~",
                        column: x => x.PendingMentorAvailabilityId,
                        principalTable: "MentorAvailabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
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
                name: "MockQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
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
                name: "CohortComplaintEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComplaintId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortComplaintEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CohortComplaintEvents_CohortComplaints_ComplaintId",
                        column: x => x.ComplaintId,
                        principalTable: "CohortComplaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortTransferRequestEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortTransferRequestEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CohortTransferRequestEvents_CohortTransferRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "CohortTransferRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorCreditGrants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    UserSubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: true),
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
                        name: "FK_MentorCreditGrants_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "MockTestAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PracticeTestAttemptId = table.Column<int>(type: "integer", nullable: false),
                    PracticeQuestionId = table.Column<int>(type: "integer", nullable: false),
                    SelectedOptionId = table.Column<int>(type: "integer", nullable: true),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "MentorBookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    UserSubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    MentorAvailabilityId = table.Column<int>(type: "integer", nullable: true),
                    LearnerId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Question = table.Column<string>(type: "text", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MeetingLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AttendedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PracticeScore = table.Column<int>(type: "integer", nullable: true),
                    PracticeFeedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PracticeFeedbackAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompensationLevelId = table.Column<int>(type: "integer", nullable: true),
                    FrozenHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FrozenLevelCoefficient = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    ScheduledCompensationAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CompensationCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CompensationFormulaVersion = table.Column<int>(type: "integer", nullable: true),
                    CreditGrantId = table.Column<long>(type: "bigint", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledByUserId = table.Column<int>(type: "integer", nullable: true),
                    CancelledByRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExceptionRequested = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ExceptionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExceptionApproved = table.Column<bool>(type: "boolean", nullable: true),
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
                        name: "FK_MentorBookings_CertificateLevels_CompensationLevelId",
                        column: x => x.CompensationLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorBookings_MentorAvailabilities_MentorAvailabilityId",
                        column: x => x.MentorAvailabilityId,
                        principalTable: "MentorAvailabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorBookings_MentorCreditGrants_CreditGrantId",
                        column: x => x.CreditGrantId,
                        principalTable: "MentorCreditGrants",
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
                name: "HomeworkAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: true),
                    CohortId = table.Column<int>(type: "integer", nullable: true),
                    StudyCourseId = table.Column<int>(type: "integer", nullable: true),
                    CohortAssignmentGroupKey = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentScope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Booking1on1"),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: true),
                    AuthoredDefinitionJson = table.Column<string>(type: "text", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AiDraftFeedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AiDraftScore = table.Column<double>(type: "double precision", nullable: true),
                    AiDraftGeneratedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AiDraftUnavailableReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MentorFeedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MentorScore = table.Column<double>(type: "double precision", nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: true),
                    PublishedFeedbackAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PublishedByMentorId = table.Column<int>(type: "integer", nullable: true),
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
                        name: "FK_HomeworkAssignments_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HomeworkAssignments_StudyCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "StudyCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HomeworkAssignments_StudyCourses_StudyCourseId",
                        column: x => x.StudyCourseId,
                        principalTable: "StudyCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "MentorAbuseReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReporterId = table.Column<int>(type: "integer", nullable: false),
                    TargetUserId = table.Column<int>(type: "integer", nullable: false),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AdminNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorAbuseReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorAbuseReports_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MentorAbuseReports_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorAbuseReports_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorAttendanceApprovals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RawMentorDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    RawLearnerDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    ApprovedEligibleMinutes = table.Column<int>(type: "integer", nullable: true),
                    EvidenceSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReviewedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorAttendanceApprovals", x => x.Id);
                    table.CheckConstraint("CK_MentorAttendanceApproval_Minutes", "\"ApprovedEligibleMinutes\" IS NULL OR (\"ApprovedEligibleMinutes\" > 0 AND \"ApprovedEligibleMinutes\" <= 90)");
                    table.CheckConstraint("CK_MentorAttendanceApproval_RawDuration", "\"RawMentorDurationSeconds\" >= 0 AND \"RawLearnerDurationSeconds\" >= 0");
                    table.ForeignKey(
                        name: "FK_MentorAttendanceApprovals_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorAttendanceApprovals_Users_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorAttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: true),
                    CourseSessionId = table.Column<int>(type: "integer", nullable: true),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FirstJoinedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastJoinedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LeftAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReconnectionCount = table.Column<int>(type: "integer", nullable: false),
                    TotalDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorAttendanceRecords", x => x.Id);
                    table.CheckConstraint("CK_MentorAttendanceRecord_Duration", "\"TotalDurationSeconds\" >= 0");
                    table.CheckConstraint("CK_MentorAttendanceRecord_Reconnection", "\"ReconnectionCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_MentorAttendanceRecords_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MentorAttendanceRecords_StudyCourseSessions_CourseSessionId",
                        column: x => x.CourseSessionId,
                        principalTable: "StudyCourseSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorAttendanceRecords_Users_UserId",
                        column: x => x.UserId,
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
                    table.CheckConstraint("CK_MentorBookingReservation_Rest", "\"RestProtectedUntilUtc\" >= \"EndUtc\"");
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
                name: "MentorChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    SenderUserId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorChatMessages_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MentorChatMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
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

            migrationBuilder.CreateTable(
                name: "MentorDisputes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvidenceUrls = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MentorResponse = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MentorRespondedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AdminNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AdminDecision = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsRefundApproved = table.Column<bool>(type: "boolean", nullable: true),
                    CreditRefunded = table.Column<bool>(type: "boolean", nullable: false),
                    RefundOperationKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ResolvedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorDisputes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorDisputes_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorDisputes_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorDisputes_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorDisputes_Users_ResolvedByAdminId",
                        column: x => x.ResolvedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: false),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    ModerationStatus = table.Column<int>(type: "integer", nullable: false),
                    MentorReply = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MentorRepliedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorReviews", x => x.Id);
                    table.CheckConstraint("CK_MentorReview_Rating_Range", "\"Rating\" >= 1 AND \"Rating\" <= 5");
                    table.ForeignKey(
                        name: "FK_MentorReviews_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorReviews_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorReviews_Users_MentorId",
                        column: x => x.MentorId,
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
                name: "HomeworkAssignmentExercises",
                columns: table => new
                {
                    HomeworkAssignmentId = table.Column<int>(type: "integer", nullable: false),
                    PracticeExerciseId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkAssignmentExercises", x => new { x.HomeworkAssignmentId, x.PracticeExerciseId });
                    table.ForeignKey(
                        name: "FK_HomeworkAssignmentExercises_HomeworkAssignments_HomeworkAss~",
                        column: x => x.HomeworkAssignmentId,
                        principalTable: "HomeworkAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkAssignmentExercises_PracticeExercises_PracticeExerc~",
                        column: x => x.PracticeExerciseId,
                        principalTable: "PracticeExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HomeworkAssignmentLessons",
                columns: table => new
                {
                    HomeworkAssignmentId = table.Column<int>(type: "integer", nullable: false),
                    LessonId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkAssignmentLessons", x => new { x.HomeworkAssignmentId, x.LessonId });
                    table.ForeignKey(
                        name: "FK_HomeworkAssignmentLessons_HomeworkAssignments_HomeworkAssig~",
                        column: x => x.HomeworkAssignmentId,
                        principalTable: "HomeworkAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkAssignmentLessons_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
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
                    LessonId = table.Column<int>(type: "integer", nullable: true),
                    TextContent = table.Column<string>(type: "text", nullable: true),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    ScanStorageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PrivateScanReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PrivateAudioReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SelectedOptionsJson = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RevisionCount = table.Column<int>(type: "integer", nullable: false),
                    LastRevisedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    HomeworkAssignmentId = table.Column<int>(type: "integer", nullable: true),
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
                        name: "FK_PracticeExerciseAttempts_HomeworkAssignments_HomeworkAssign~",
                        column: x => x.HomeworkAssignmentId,
                        principalTable: "HomeworkAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "MentorEarningEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    AttendanceApprovalId = table.Column<long>(type: "bigint", nullable: false),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    JlptLevelCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FrozenHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FrozenLevelCoefficient = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    ApprovedEligibleMinutes = table.Column<int>(type: "integer", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FormulaVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PayrollPeriodId = table.Column<long>(type: "bigint", nullable: true),
                    OperationKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorEarningEntries", x => x.Id);
                    table.CheckConstraint("CK_MentorEarningEntry_Positive", "\"FrozenHourlyRate\" > 0 AND \"FrozenLevelCoefficient\" > 0 AND \"ApprovedEligibleMinutes\" > 0 AND \"ApprovedEligibleMinutes\" <= 90 AND \"GrossAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_MentorAttendanceApprovals_AttendanceAp~",
                        column: x => x.AttendanceApprovalId,
                        principalTable: "MentorAttendanceApprovals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_MentorPayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "MentorPayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_Users_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorDisputeHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisputeId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousStatus = table.Column<int>(type: "integer", nullable: true),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorDisputeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorDisputeHistories_MentorDisputes_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "MentorDisputes",
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
                name: "MentorPayrollLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollPeriodId = table.Column<long>(type: "bigint", nullable: false),
                    EarningEntryId = table.Column<long>(type: "bigint", nullable: false),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    FrozenHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FrozenLevelCoefficient = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    ApprovedEligibleMinutes = table.Column<int>(type: "integer", nullable: false),
                    LineAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPayrollLines", x => x.Id);
                    table.CheckConstraint("CK_MentorPayrollLine_Positive", "\"LineAmount\" > 0 AND \"ApprovedEligibleMinutes\" > 0");
                    table.ForeignKey(
                        name: "FK_MentorPayrollLines_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayrollLines_MentorEarningEntries_EarningEntryId",
                        column: x => x.EarningEntryId,
                        principalTable: "MentorEarningEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayrollLines_MentorPayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "MentorPayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CertificateTypes",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "IsActive", "IsDeleted", "ModifiedBy", "ModifiedDate", "Name", "Revision", "SortOrder" },
                values: new object[] { 1, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese-Language Proficiency Test", true, false, null, null, "JLPT", 1, 1 });

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
                table: "KanaCharacters",
                columns: new[] { "Id", "AudioUrl", "Character", "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate", "Romaji", "StrokeCount", "StrokeOrderImageUrl", "Type" },
                values: new object[,]
                {
                    { 1, "https://upload.wikimedia.org/wikipedia/commons/2/22/Ja-a.ogg", "あ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1757), false, null, null, "a", 3, null, "Hiragana" },
                    { 2, "https://upload.wikimedia.org/wikipedia/commons/2/28/Ja-i.ogg", "い", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1766), false, null, null, "i", 2, null, "Hiragana" },
                    { 3, "https://upload.wikimedia.org/wikipedia/commons/a/a2/Ja-u.ogg", "う", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1768), false, null, null, "u", 2, null, "Hiragana" },
                    { 4, "https://upload.wikimedia.org/wikipedia/commons/2/21/Ja-e.ogg", "え", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1770), false, null, null, "e", 2, null, "Hiragana" },
                    { 5, "https://upload.wikimedia.org/wikipedia/commons/d/df/Ja-o.ogg", "お", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1771), false, null, null, "o", 3, null, "Hiragana" },
                    { 6, "https://upload.wikimedia.org/wikipedia/commons/2/2e/Ja-ka.ogg", "か", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1774), false, null, null, "ka", 3, null, "Hiragana" },
                    { 7, "https://upload.wikimedia.org/wikipedia/commons/8/87/Ja-ki.ogg", "き", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1775), false, null, null, "ki", 4, null, "Hiragana" },
                    { 8, "https://upload.wikimedia.org/wikipedia/commons/a/a3/Ja-ku.ogg", "く", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1776), false, null, null, "ku", 1, null, "Hiragana" },
                    { 9, "https://upload.wikimedia.org/wikipedia/commons/b/b3/Ja-ke.ogg", "け", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1830), false, null, null, "ke", 3, null, "Hiragana" },
                    { 10, "https://upload.wikimedia.org/wikipedia/commons/1/18/Ja-ko.ogg", "こ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1832), false, null, null, "ko", 2, null, "Hiragana" },
                    { 11, "https://upload.wikimedia.org/wikipedia/commons/a/a9/Ja-sa.ogg", "さ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1833), false, null, null, "sa", 3, null, "Hiragana" },
                    { 12, "https://upload.wikimedia.org/wikipedia/commons/8/80/Ja-shi.ogg", "し", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1834), false, null, null, "shi", 1, null, "Hiragana" },
                    { 13, "https://upload.wikimedia.org/wikipedia/commons/a/a7/Ja-su.ogg", "す", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1835), false, null, null, "su", 2, null, "Hiragana" },
                    { 14, "https://upload.wikimedia.org/wikipedia/commons/a/a8/Ja-se.ogg", "せ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1836), false, null, null, "se", 3, null, "Hiragana" },
                    { 15, "https://upload.wikimedia.org/wikipedia/commons/0/00/Ja-so.ogg", "そ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1837), false, null, null, "so", 1, null, "Hiragana" },
                    { 16, "https://upload.wikimedia.org/wikipedia/commons/d/d2/Ja-ta.ogg", "た", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1838), false, null, null, "ta", 4, null, "Hiragana" },
                    { 17, "https://upload.wikimedia.org/wikipedia/commons/3/38/Ja-chi.ogg", "ち", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1838), false, null, null, "chi", 2, null, "Hiragana" },
                    { 18, "https://upload.wikimedia.org/wikipedia/commons/3/32/Ja-tsu.ogg", "つ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1840), false, null, null, "tsu", 1, null, "Hiragana" },
                    { 19, "https://upload.wikimedia.org/wikipedia/commons/1/10/Ja-te.ogg", "て", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1841), false, null, null, "te", 1, null, "Hiragana" },
                    { 20, "https://upload.wikimedia.org/wikipedia/commons/a/a5/Ja-to.ogg", "と", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1842), false, null, null, "to", 2, null, "Hiragana" },
                    { 21, "https://upload.wikimedia.org/wikipedia/commons/9/92/Ja-na.ogg", "な", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1843), false, null, null, "na", 4, null, "Hiragana" },
                    { 22, "https://upload.wikimedia.org/wikipedia/commons/b/b7/Ja-ni.ogg", "に", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1844), false, null, null, "ni", 3, null, "Hiragana" },
                    { 23, "https://upload.wikimedia.org/wikipedia/commons/b/b1/Ja-nu.ogg", "ぬ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1845), false, null, null, "nu", 2, null, "Hiragana" },
                    { 24, "https://upload.wikimedia.org/wikipedia/commons/8/86/Ja-ne.ogg", "ね", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1845), false, null, null, "ne", 2, null, "Hiragana" },
                    { 25, "https://upload.wikimedia.org/wikipedia/commons/5/54/Ja-no.ogg", "の", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1846), false, null, null, "no", 1, null, "Hiragana" },
                    { 26, "https://upload.wikimedia.org/wikipedia/commons/0/08/Ja-ha.ogg", "は", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1847), false, null, null, "ha", 3, null, "Hiragana" },
                    { 27, "https://upload.wikimedia.org/wikipedia/commons/9/99/Ja-hi.ogg", "ひ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1848), false, null, null, "hi", 1, null, "Hiragana" },
                    { 28, "https://upload.wikimedia.org/wikipedia/commons/7/70/Ja-fu.ogg", "ふ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1849), false, null, null, "fu", 4, null, "Hiragana" },
                    { 29, "https://upload.wikimedia.org/wikipedia/commons/2/2a/Ja-he.ogg", "へ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1849), false, null, null, "he", 1, null, "Hiragana" },
                    { 30, "https://upload.wikimedia.org/wikipedia/commons/a/a4/Ja-ho.ogg", "ほ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1850), false, null, null, "ho", 4, null, "Hiragana" },
                    { 31, "https://upload.wikimedia.org/wikipedia/commons/0/0d/Ja-ma.ogg", "ま", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1851), false, null, null, "ma", 3, null, "Hiragana" },
                    { 32, "https://upload.wikimedia.org/wikipedia/commons/1/16/Ja-mi.ogg", "み", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1852), false, null, null, "mi", 2, null, "Hiragana" },
                    { 33, "https://upload.wikimedia.org/wikipedia/commons/e/eb/Ja-mu.ogg", "む", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1852), false, null, null, "mu", 3, null, "Hiragana" },
                    { 34, "https://upload.wikimedia.org/wikipedia/commons/8/8b/Ja-me.ogg", "め", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1913), false, null, null, "me", 2, null, "Hiragana" },
                    { 35, "https://upload.wikimedia.org/wikipedia/commons/c/c1/Ja-mo.ogg", "も", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1915), false, null, null, "mo", 3, null, "Hiragana" },
                    { 36, "https://upload.wikimedia.org/wikipedia/commons/e/e8/Ja-ya.ogg", "や", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1915), false, null, null, "ya", 3, null, "Hiragana" },
                    { 37, "https://upload.wikimedia.org/wikipedia/commons/7/7b/Ja-yu.ogg", "ゆ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1916), false, null, null, "yu", 2, null, "Hiragana" },
                    { 38, "https://upload.wikimedia.org/wikipedia/commons/c/c9/Ja-yo.ogg", "よ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1917), false, null, null, "yo", 2, null, "Hiragana" },
                    { 39, "https://upload.wikimedia.org/wikipedia/commons/0/04/Ja-ra.ogg", "ら", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1918), false, null, null, "ra", 2, null, "Hiragana" },
                    { 40, "https://upload.wikimedia.org/wikipedia/commons/7/76/Ja-ri.ogg", "り", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1919), false, null, null, "ri", 2, null, "Hiragana" },
                    { 41, "https://upload.wikimedia.org/wikipedia/commons/5/56/Ja-ru.ogg", "る", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1920), false, null, null, "ru", 1, null, "Hiragana" },
                    { 42, "https://upload.wikimedia.org/wikipedia/commons/5/57/Ja-re.ogg", "れ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1920), false, null, null, "re", 2, null, "Hiragana" },
                    { 43, "https://upload.wikimedia.org/wikipedia/commons/e/ed/Ja-ro.ogg", "ろ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1922), false, null, null, "ro", 1, null, "Hiragana" },
                    { 44, "https://upload.wikimedia.org/wikipedia/commons/f/f2/Ja-wa.ogg", "わ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1922), false, null, null, "wa", 2, null, "Hiragana" },
                    { 45, "https://upload.wikimedia.org/wikipedia/commons/7/77/Ja-wo.ogg", "を", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1923), false, null, null, "wo", 3, null, "Hiragana" },
                    { 46, "https://upload.wikimedia.org/wikipedia/commons/0/0c/Ja-n.ogg", "ん", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1924), false, null, null, "n", 1, null, "Hiragana" },
                    { 47, "https://upload.wikimedia.org/wikipedia/commons/2/22/Ja-a.ogg", "ア", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1955), false, null, null, "a", 2, null, "Katakana" },
                    { 48, "https://upload.wikimedia.org/wikipedia/commons/2/28/Ja-i.ogg", "イ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1958), false, null, null, "i", 2, null, "Katakana" },
                    { 49, "https://upload.wikimedia.org/wikipedia/commons/a/a2/Ja-u.ogg", "ウ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1959), false, null, null, "u", 3, null, "Katakana" },
                    { 50, "https://upload.wikimedia.org/wikipedia/commons/2/21/Ja-e.ogg", "エ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1960), false, null, null, "e", 3, null, "Katakana" },
                    { 51, "https://upload.wikimedia.org/wikipedia/commons/d/df/Ja-o.ogg", "オ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1961), false, null, null, "o", 3, null, "Katakana" },
                    { 52, "https://upload.wikimedia.org/wikipedia/commons/2/2e/Ja-ka.ogg", "カ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1961), false, null, null, "ka", 2, null, "Katakana" },
                    { 53, "https://upload.wikimedia.org/wikipedia/commons/8/87/Ja-ki.ogg", "キ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1962), false, null, null, "ki", 3, null, "Katakana" },
                    { 54, "https://upload.wikimedia.org/wikipedia/commons/a/a3/Ja-ku.ogg", "ク", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1963), false, null, null, "ku", 2, null, "Katakana" },
                    { 55, "https://upload.wikimedia.org/wikipedia/commons/b/b3/Ja-ke.ogg", "ケ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1964), false, null, null, "ke", 3, null, "Katakana" },
                    { 56, "https://upload.wikimedia.org/wikipedia/commons/1/18/Ja-ko.ogg", "コ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2006), false, null, null, "ko", 2, null, "Katakana" },
                    { 57, "https://upload.wikimedia.org/wikipedia/commons/a/a9/Ja-sa.ogg", "サ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2009), false, null, null, "sa", 3, null, "Katakana" },
                    { 58, "https://upload.wikimedia.org/wikipedia/commons/8/80/Ja-shi.ogg", "シ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2010), false, null, null, "shi", 3, null, "Katakana" },
                    { 59, "https://upload.wikimedia.org/wikipedia/commons/a/a7/Ja-su.ogg", "ス", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2011), false, null, null, "su", 2, null, "Katakana" },
                    { 60, "https://upload.wikimedia.org/wikipedia/commons/a/a8/Ja-se.ogg", "セ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2012), false, null, null, "se", 2, null, "Katakana" },
                    { 61, "https://upload.wikimedia.org/wikipedia/commons/0/00/Ja-so.ogg", "ソ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2014), false, null, null, "so", 2, null, "Katakana" },
                    { 62, "https://upload.wikimedia.org/wikipedia/commons/d/d2/Ja-ta.ogg", "タ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2015), false, null, null, "ta", 3, null, "Katakana" },
                    { 63, "https://upload.wikimedia.org/wikipedia/commons/3/38/Ja-chi.ogg", "チ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2016), false, null, null, "chi", 3, null, "Katakana" },
                    { 64, "https://upload.wikimedia.org/wikipedia/commons/3/32/Ja-tsu.ogg", "ツ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2017), false, null, null, "tsu", 3, null, "Katakana" },
                    { 65, "https://upload.wikimedia.org/wikipedia/commons/1/10/Ja-te.ogg", "テ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2019), false, null, null, "te", 3, null, "Katakana" },
                    { 66, "https://upload.wikimedia.org/wikipedia/commons/a/a5/Ja-to.ogg", "ト", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2022), false, null, null, "to", 2, null, "Katakana" },
                    { 67, "https://upload.wikimedia.org/wikipedia/commons/9/92/Ja-na.ogg", "ナ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2023), false, null, null, "na", 2, null, "Katakana" },
                    { 68, "https://upload.wikimedia.org/wikipedia/commons/b/b7/Ja-ni.ogg", "ニ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2024), false, null, null, "ni", 2, null, "Katakana" },
                    { 69, "https://upload.wikimedia.org/wikipedia/commons/b/b1/Ja-nu.ogg", "ヌ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2026), false, null, null, "nu", 2, null, "Katakana" },
                    { 70, "https://upload.wikimedia.org/wikipedia/commons/8/86/Ja-ne.ogg", "ネ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2027), false, null, null, "ne", 4, null, "Katakana" },
                    { 71, "https://upload.wikimedia.org/wikipedia/commons/5/54/Ja-no.ogg", "ノ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2028), false, null, null, "no", 1, null, "Katakana" },
                    { 72, "https://upload.wikimedia.org/wikipedia/commons/0/08/Ja-ha.ogg", "ハ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2029), false, null, null, "ha", 2, null, "Katakana" },
                    { 73, "https://upload.wikimedia.org/wikipedia/commons/9/99/Ja-hi.ogg", "ヒ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2030), false, null, null, "hi", 2, null, "Katakana" },
                    { 74, "https://upload.wikimedia.org/wikipedia/commons/7/70/Ja-fu.ogg", "フ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2032), false, null, null, "fu", 1, null, "Katakana" },
                    { 75, "https://upload.wikimedia.org/wikipedia/commons/2/2a/Ja-he.ogg", "ヘ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2033), false, null, null, "he", 1, null, "Katakana" },
                    { 76, "https://upload.wikimedia.org/wikipedia/commons/a/a4/Ja-ho.ogg", "ホ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2034), false, null, null, "ho", 4, null, "Katakana" },
                    { 77, "https://upload.wikimedia.org/wikipedia/commons/0/0d/Ja-ma.ogg", "マ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2036), false, null, null, "ma", 2, null, "Katakana" },
                    { 78, "https://upload.wikimedia.org/wikipedia/commons/1/16/Ja-mi.ogg", "ミ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2037), false, null, null, "mi", 3, null, "Katakana" },
                    { 79, "https://upload.wikimedia.org/wikipedia/commons/e/eb/Ja-mu.ogg", "ム", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2038), false, null, null, "mu", 2, null, "Katakana" },
                    { 80, "https://upload.wikimedia.org/wikipedia/commons/8/8b/Ja-me.ogg", "メ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2039), false, null, null, "me", 2, null, "Katakana" },
                    { 81, "https://upload.wikimedia.org/wikipedia/commons/c/c1/Ja-mo.ogg", "モ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2086), false, null, null, "mo", 3, null, "Katakana" },
                    { 82, "https://upload.wikimedia.org/wikipedia/commons/e/e8/Ja-ya.ogg", "ヤ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2087), false, null, null, "ya", 2, null, "Katakana" },
                    { 83, "https://upload.wikimedia.org/wikipedia/commons/7/7b/Ja-yu.ogg", "ユ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2088), false, null, null, "yu", 2, null, "Katakana" },
                    { 84, "https://upload.wikimedia.org/wikipedia/commons/c/c9/Ja-yo.ogg", "ヨ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2089), false, null, null, "yo", 3, null, "Katakana" },
                    { 85, "https://upload.wikimedia.org/wikipedia/commons/0/04/Ja-ra.ogg", "ラ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2090), false, null, null, "ra", 2, null, "Katakana" },
                    { 86, "https://upload.wikimedia.org/wikipedia/commons/7/76/Ja-ri.ogg", "リ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2092), false, null, null, "ri", 2, null, "Katakana" },
                    { 87, "https://upload.wikimedia.org/wikipedia/commons/5/56/Ja-ru.ogg", "ル", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2093), false, null, null, "ru", 2, null, "Katakana" },
                    { 88, "https://upload.wikimedia.org/wikipedia/commons/5/57/Ja-re.ogg", "レ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2094), false, null, null, "re", 1, null, "Katakana" },
                    { 89, "https://upload.wikimedia.org/wikipedia/commons/e/ed/Ja-ro.ogg", "ロ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2096), false, null, null, "ro", 3, null, "Katakana" },
                    { 90, "https://upload.wikimedia.org/wikipedia/commons/f/f2/Ja-wa.ogg", "ワ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2097), false, null, null, "wa", 2, null, "Katakana" },
                    { 91, "https://upload.wikimedia.org/wikipedia/commons/7/77/Ja-wo.ogg", "ヲ", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2098), false, null, null, "wo", 3, null, "Katakana" },
                    { 92, "https://upload.wikimedia.org/wikipedia/commons/0/0c/Ja-n.ogg", "ン", null, new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2099), false, null, null, "n", 2, null, "Katakana" }
                });

            migrationBuilder.InsertData(
                table: "Kanjis",
                columns: new[] { "Id", "AudioUrl", "Character", "CreatedBy", "CreatedDate", "ImageUrl", "IsDeleted", "KunYomi", "Meaning", "ModifiedBy", "ModifiedDate", "OnYomi", "SinoVietnamese", "StrokeCount", "StrokeOrderImageUrl", "UserAccountId" },
                values: new object[,]
                {
                    { 910001, null, "日", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, "ひ、か", "Mặt trời; ngày", null, null, "ニチ、ジツ", "Nhật", 4, null, null },
                    { 910002, null, "月", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, "つき", "Mặt trăng; tháng", null, null, "ゲツ、ガツ", "Nguyệt", 4, null, null },
                    { 910003, null, "火", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, "ひ、ほ", "Lửa", null, null, "カ", "Hỏa", 4, null, null },
                    { 910004, null, "水", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, "みず", "Nước", null, null, "スイ", "Thủy", 4, null, null },
                    { 910005, null, "木", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, "き、こ", "Cây; gỗ", null, null, "モク、ボク", "Mộc", 4, null, null },
                    { 910006, null, "金", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, "かね、かな", "Vàng; tiền", null, null, "キン、コン", "Kim", 8, null, null },
                    { 910007, null, "土", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, "つち", "Đất", null, null, "ド、ト", "Thổ", 3, null, null },
                    { 910008, null, "山", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, "やま", "Núi", null, null, "サン", "Sơn", 3, null, null },
                    { 910009, null, "川", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, "かわ", "Sông", null, null, "セン", "Xuyên", 3, null, null },
                    { 910010, null, "人", null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, false, "ひと", "Người", null, null, "ジン、ニン", "Nhân", 2, null, null }
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
                table: "MentorGroupPolicies",
                columns: new[] { "Id", "AllowedSessionsPerWeekCsv", "CancellationLeadHours", "CheckoutHoldMinutes", "CourseDurationWeeks", "CreatedAtUtc", "CreatedByAdminId", "DailySessionCap", "DefaultSessionsPerWeek", "EffectiveFromUtc", "EnrollmentCutoffMinutesBeforeFirstSession", "HomeworkDeadlineDays", "KpiReportingPeriodDays", "LearnerTransferEnabled", "MaxLearners", "MentorReplacementEnabled", "MentorReplacementLeadHours", "MinLearnersToStart", "PlacementAttemptLimit", "PublicationHorizonDays", "RecurringWeekdayPatternsJson", "RemediationCompletionThresholdPercent", "RemediationThresholdComparison", "RequireAdminRemediationReview", "RequireOfficialExamEvidence", "ReschedulingLeadHours", "RestMinutes", "SessionMinutes", "TransferAllowedAfterSourceStart", "Version", "WeeklySessionCap" },
                values: new object[] { 1, "2,3,4", 24, 10, 10, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4, 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0, 7, 30, true, 4, true, 24, 1, 2, 30, "[[1,3,5],[2,4,6]]", 90m, ">", true, true, 24, 15, 90, false, 1, 0 });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "AiGradingQuota", "CertificateLevelId", "CreatedBy", "CreatedDate", "Description", "DurationDays", "IsActive", "IsDeleted", "IsPopular", "IsTrial", "ModifiedBy", "ModifiedDate", "Name", "PlanCategory", "Price", "Revision", "SortOrder" },
                values: new object[,]
                {
                    { 1, 1, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 1 tuần.", 7, true, false, false, false, null, null, "Tự học tiếng Nhật - 1 tuần", "self_study", 1000m, 1, 6 },
                    { 2, 2, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 2 tuần.", 14, true, false, false, false, null, null, "Tự học tiếng Nhật - 2 tuần", "self_study", 1000m, 1, 5 },
                    { 3, 3, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 3 tuần.", 21, true, false, false, false, null, null, "Tự học tiếng Nhật - 3 tuần", "self_study", 2000m, 1, 4 },
                    { 4, 5, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 1 tháng.", 30, true, false, true, false, null, null, "Tự học tiếng Nhật - 1 tháng", "self_study", 2000m, 1, 3 },
                    { 5, 10, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 2 tháng.", 60, true, false, false, false, null, null, "Tự học tiếng Nhật - 2 tháng", "self_study", 3000m, 1, 2 },
                    { 6, 15, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 3 tháng.", 90, true, false, false, false, null, null, "Tự học tiếng Nhật - 3 tháng", "self_study", 3000m, 1, 1 }
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "AiGradingQuota", "CertificateLevelId", "CreatedBy", "CreatedDate", "Description", "DurationDays", "IsActive", "IsDeleted", "IsPopular", "IsTrial", "MeetingSessionCount", "ModifiedBy", "ModifiedDate", "Name", "PlanCategory", "Price", "Revision", "SortOrder" },
                values: new object[] { 7, 0, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Gói demo gồm 12 buổi ôn luyện 1-1 với Mentor, thời hạn 90 ngày. Dữ liệu thanh toán demo được seed để kiểm thử đặt lịch trên frontend.", 90, true, false, false, false, 12, null, null, "Meeting 1-1 với Mentor - 12 buổi", "mentor_legacy", 1000m, 1, 7 });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AssessedCertificateLevelId", "AvatarUrl", "CreatedBy", "CreatedDate", "CurrentStreak", "DailyStudyMinutes", "DailyStudyMinutesMax", "DailyStudyMinutesMin", "DeclaredJlptCertificateCode", "Email", "EmailNotificationsEnabled", "FirebaseUid", "FirstName", "GoogleSubject", "IsDeleted", "IsEmailVerified", "IsPhoneVerified", "LastName", "LastStudyDate", "LearningMode", "LongestStreak", "ModifiedBy", "ModifiedDate", "NotificationPreferencesJson", "PasswordHash", "PasswordSalt", "PendingEmail", "PendingEmailRequestedAtUtc", "PhoneNumber", "PlacementDecision", "Role", "SelfDeclaredCertificateLevelId", "StartingCertificateLevelId", "StudyTimePreference", "SystemNotificationsEnabled", "TargetCertificationLevelId", "TargetExamDate", "TokenVersion" },
                values: new object[,]
                {
                    { 900001, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, null, null, null, "admin@rikipath.local", true, null, "Admin", null, false, true, false, "System", null, "Standard", 0, null, null, null, new byte[] { 91, 215, 217, 54, 239, 206, 181, 216, 143, 33, 82, 211, 9, 70, 197, 173, 197, 0, 72, 71, 59, 150, 46, 114, 96, 137, 52, 174, 137, 29, 196, 61, 50, 254, 249, 101, 114, 92, 149, 32, 55, 127, 205, 126, 216, 147, 197, 192, 248, 173, 158, 92, 104, 42, 195, 107, 150, 15, 3, 74, 103, 104, 3, 229 }, new byte[] { 61, 240, 150, 172, 239, 48, 224, 17, 192, 54, 50, 58, 91, 100, 55, 168, 18, 69, 137, 112, 110, 59, 180, 37, 201, 241, 143, 238, 139, 33, 128, 70, 87, 158, 235, 219, 229, 184, 94, 72, 41, 160, 160, 197, 158, 217, 128, 188, 156, 255, 147, 13, 151, 37, 133, 164, 9, 252, 175, 124, 21, 15, 152, 196 }, null, null, null, "NotTaken", "Admin", null, null, null, true, null, null, 1 },
                    { 900002, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, null, null, null, "mentor1@rikipath.local", true, null, "Mentor", null, false, true, false, "One", null, "Standard", 0, null, null, null, new byte[] { 17, 253, 106, 25, 89, 176, 204, 31, 142, 252, 245, 31, 64, 101, 213, 135, 164, 116, 40, 46, 173, 175, 8, 0, 69, 101, 65, 4, 90, 25, 176, 223, 153, 237, 220, 94, 55, 133, 227, 181, 70, 97, 155, 122, 50, 139, 216, 136, 88, 52, 76, 98, 250, 64, 52, 71, 146, 221, 225, 137, 106, 6, 178, 239 }, new byte[] { 233, 77, 244, 252, 171, 97, 38, 140, 50, 138, 10, 54, 122, 165, 186, 85, 129, 82, 49, 10, 98, 44, 165, 57, 50, 7, 238, 166, 142, 224, 94, 49, 134, 226, 247, 110, 154, 139, 99, 204, 95, 39, 171, 235, 212, 26, 217, 87, 139, 118, 253, 125, 241, 255, 57, 22, 140, 171, 11, 168, 174, 202, 32, 17 }, null, null, null, "NotTaken", "Mentor", null, null, null, true, null, null, 1 },
                    { 900003, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, null, null, null, "mentor2@rikipath.local", true, null, "Mentor", null, false, true, false, "Two", null, "Standard", 0, null, null, null, new byte[] { 93, 130, 109, 171, 238, 187, 245, 15, 202, 89, 247, 220, 95, 192, 246, 138, 5, 75, 250, 28, 208, 28, 51, 79, 127, 28, 214, 237, 93, 221, 108, 26, 152, 66, 73, 65, 11, 147, 154, 102, 113, 49, 51, 24, 51, 197, 248, 254, 22, 98, 87, 249, 250, 104, 131, 74, 16, 18, 179, 200, 3, 3, 79, 47 }, new byte[] { 240, 155, 254, 183, 129, 162, 74, 247, 41, 9, 67, 242, 128, 8, 226, 85, 5, 104, 104, 72, 101, 75, 130, 95, 47, 101, 116, 89, 207, 23, 119, 178, 38, 180, 185, 145, 58, 115, 14, 30, 85, 216, 169, 133, 94, 115, 11, 117, 96, 87, 227, 240, 204, 48, 51, 144, 170, 248, 167, 56, 175, 194, 253, 135 }, null, null, null, "NotTaken", "Mentor", null, null, null, true, null, null, 1 },
                    { 900004, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, null, null, null, "author1@rikipath.local", true, null, "Content", null, false, true, false, "Author One", null, "Standard", 0, null, null, null, new byte[] { 168, 249, 82, 144, 178, 145, 94, 164, 34, 152, 2, 74, 204, 105, 84, 145, 96, 199, 153, 136, 110, 82, 114, 113, 22, 139, 117, 143, 50, 239, 197, 4, 51, 189, 137, 48, 175, 14, 104, 164, 218, 249, 148, 217, 33, 44, 202, 128, 237, 183, 110, 228, 185, 36, 209, 254, 183, 182, 55, 1, 78, 16, 234, 50 }, new byte[] { 19, 216, 84, 143, 3, 184, 50, 39, 209, 137, 149, 82, 224, 206, 6, 240, 237, 242, 172, 27, 55, 92, 242, 101, 149, 76, 104, 38, 176, 43, 40, 8, 102, 202, 146, 224, 62, 43, 147, 132, 2, 158, 81, 170, 29, 103, 76, 248, 101, 105, 213, 205, 32, 51, 59, 39, 221, 11, 135, 250, 193, 8, 78, 219 }, null, null, null, "NotTaken", "ContentAuthor", null, null, null, true, null, null, 1 },
                    { 900005, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, null, null, null, "author2@rikipath.local", true, null, "Content", null, false, true, false, "Author Two", null, "Standard", 0, null, null, null, new byte[] { 189, 121, 9, 1, 230, 137, 43, 239, 79, 56, 195, 196, 66, 116, 42, 1, 137, 55, 54, 249, 55, 231, 79, 224, 182, 182, 199, 133, 100, 39, 138, 5, 223, 215, 48, 46, 60, 214, 49, 88, 42, 186, 232, 116, 237, 89, 55, 144, 4, 78, 215, 231, 237, 220, 96, 225, 202, 97, 61, 165, 246, 28, 126, 198 }, new byte[] { 66, 178, 77, 56, 234, 206, 29, 78, 136, 190, 160, 142, 226, 249, 14, 17, 26, 101, 167, 36, 193, 55, 150, 94, 239, 81, 69, 54, 81, 171, 80, 22, 208, 38, 242, 2, 55, 171, 64, 38, 48, 224, 36, 216, 133, 132, 147, 100, 174, 32, 65, 134, 128, 35, 50, 138, 204, 59, 167, 96, 11, 128, 101, 102 }, null, null, null, "NotTaken", "ContentAuthor", null, null, null, true, null, null, 1 },
                    { 900006, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, null, null, null, "learner.meeting@rikipath.local", true, null, "Meeting", null, false, true, false, "Test Learner", null, "Standard", 0, null, null, null, new byte[] { 9, 206, 219, 58, 155, 234, 107, 145, 121, 174, 134, 51, 96, 253, 64, 216, 157, 114, 92, 36, 123, 206, 67, 217, 207, 166, 54, 71, 254, 251, 156, 200, 185, 110, 198, 135, 221, 146, 243, 40, 237, 229, 16, 133, 163, 161, 156, 79, 138, 103, 85, 25, 114, 81, 99, 182, 25, 73, 212, 198, 212, 192, 54, 129 }, new byte[] { 231, 202, 97, 95, 11, 9, 242, 47, 62, 77, 67, 36, 170, 78, 120, 81, 56, 69, 47, 36, 209, 212, 107, 244, 15, 34, 189, 110, 57, 66, 205, 213, 224, 19, 245, 192, 104, 146, 130, 44, 54, 107, 2, 15, 109, 136, 118, 220, 228, 44, 110, 219, 31, 100, 161, 164, 63, 231, 196, 79, 120, 106, 181, 71 }, null, null, null, "NotTaken", "Learner", null, null, null, true, null, null, 1 },
                    { 900007, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, null, null, null, "learner2.meeting@rikipath.local", true, null, "Linh", null, false, true, false, "Nguyen", null, "Standard", 0, null, null, null, new byte[] { 217, 79, 50, 159, 85, 88, 67, 15, 23, 182, 103, 80, 220, 85, 86, 173, 204, 186, 57, 67, 183, 165, 253, 243, 150, 250, 150, 190, 122, 252, 55, 169, 85, 236, 69, 106, 19, 219, 244, 42, 239, 30, 189, 176, 52, 47, 30, 28, 125, 31, 162, 255, 61, 3, 238, 201, 168, 88, 161, 42, 216, 86, 172, 219 }, new byte[] { 85, 97, 108, 158, 219, 70, 81, 106, 73, 23, 205, 163, 105, 159, 117, 19, 175, 147, 178, 99, 112, 116, 163, 243, 109, 178, 129, 229, 245, 175, 91, 174, 66, 198, 122, 88, 80, 68, 160, 64, 85, 118, 105, 41, 135, 89, 49, 160, 120, 13, 116, 148, 255, 27, 157, 207, 106, 203, 127, 74, 19, 61, 148, 120 }, null, null, null, "NotTaken", "Learner", null, null, null, true, null, null, 1 },
                    { 900008, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, null, null, null, "learner3.meeting@rikipath.local", true, null, "Minh", null, false, true, false, "Tran", null, "Standard", 0, null, null, null, new byte[] { 84, 97, 164, 196, 63, 151, 182, 39, 139, 242, 152, 22, 179, 33, 174, 113, 0, 236, 253, 114, 234, 242, 209, 76, 196, 162, 143, 195, 180, 3, 178, 193, 205, 230, 18, 114, 49, 151, 198, 217, 254, 21, 94, 176, 146, 214, 235, 122, 28, 241, 164, 161, 4, 161, 209, 38, 102, 179, 31, 107, 157, 25, 35, 63 }, new byte[] { 26, 188, 78, 205, 57, 115, 100, 129, 148, 135, 165, 205, 148, 203, 245, 36, 244, 177, 9, 104, 120, 140, 21, 164, 36, 75, 122, 51, 115, 69, 174, 180, 120, 155, 62, 37, 97, 227, 212, 134, 200, 7, 66, 255, 219, 10, 140, 149, 67, 86, 164, 170, 142, 165, 181, 103, 47, 100, 238, 24, 178, 29, 171, 226 }, null, null, null, "NotTaken", "Learner", null, null, null, true, null, null, 1 },
                    { 900009, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, null, null, null, "learner4.meeting@rikipath.local", true, null, "Hoang", null, false, true, false, "Pham", null, "Standard", 0, null, null, null, new byte[] { 42, 30, 236, 129, 234, 192, 63, 84, 148, 163, 154, 246, 107, 147, 189, 95, 33, 141, 196, 67, 181, 51, 157, 136, 93, 228, 237, 165, 164, 49, 4, 146, 87, 84, 9, 47, 28, 59, 33, 130, 37, 63, 43, 229, 8, 178, 123, 74, 61, 159, 235, 68, 217, 51, 225, 29, 201, 104, 44, 152, 53, 255, 199, 28 }, new byte[] { 83, 214, 138, 3, 142, 91, 229, 97, 238, 61, 48, 237, 80, 82, 112, 249, 166, 81, 53, 95, 170, 120, 13, 87, 113, 159, 60, 37, 75, 229, 226, 107, 229, 89, 60, 57, 109, 196, 235, 81, 145, 154, 110, 120, 141, 13, 142, 77, 41, 132, 58, 80, 154, 57, 226, 168, 176, 251, 113, 167, 242, 139, 7, 177 }, null, null, null, "NotTaken", "Learner", null, null, null, true, null, null, 1 }
                });

            migrationBuilder.InsertData(
                table: "CertificateLevels",
                columns: new[] { "Id", "CertificationId", "Code", "CreatedBy", "CreatedDate", "Description", "IsDeleted", "ModifiedBy", "ModifiedDate", "Revision", "SortOrder" },
                values: new object[,]
                {
                    { 1, 1, "N5", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N5", false, null, null, 1, 1 },
                    { 2, 1, "N4", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N4", false, null, null, 1, 2 },
                    { 3, 1, "N3", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N3", false, null, null, 1, 3 },
                    { 4, 1, "N2", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N2", false, null, null, 1, 4 },
                    { 5, 1, "N1", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N1", false, null, null, 1, 5 }
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
                columns: new[] { "Id", "AdminName", "AttendanceTrackingEnabled", "BookedCount", "CancelReason", "CancelRejectedAt", "CancelRejectionReason", "CancelRequestedAt", "ChangeRequestReason", "ChangeRequestedAt", "ChangeResolution", "CreatedBy", "CreatedDate", "EndTime", "GenerationKey", "GroupPolicyId", "IsApproved", "IsDeleted", "LocalBusinessDate", "MaxLearners", "MentorId", "MentorScheduleRuleId", "MentorWeeklyAvailabilityId", "ModifiedBy", "ModifiedDate", "PolicyVersion", "PublishedAt", "RejectedAt", "RejectionReason", "RestMinutesSnapshot", "RoomId", "SessionMinutesSnapshot", "StartTime" },
                values: new object[,]
                {
                    { 990001, "Seed Demo", true, 1, null, null, null, null, null, null, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 2, 10, 0, 0, 0, DateTimeKind.Unspecified), null, null, true, false, null, 4, 900002, null, null, null, null, null, null, null, null, null, new Guid("a1000000-0000-0000-0000-000000990001"), null, new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 990002, "Seed Demo", true, 0, null, null, null, null, null, null, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 3, 14, 0, 0, 0, DateTimeKind.Unspecified), null, null, true, false, null, 4, 900002, null, null, null, null, null, null, null, null, null, new Guid("a1000000-0000-0000-0000-000000990002"), null, new DateTime(2026, 10, 3, 13, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 990003, "Seed Demo", true, 0, null, null, null, null, null, null, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 4, 10, 0, 0, 0, DateTimeKind.Unspecified), null, null, true, false, null, 4, 900003, null, null, null, null, null, null, null, null, null, new Guid("a1000000-0000-0000-0000-000000990003"), null, new DateTime(2026, 10, 4, 9, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 990004, "Seed Demo", true, 0, null, null, null, null, null, null, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 5, 14, 0, 0, 0, DateTimeKind.Unspecified), null, null, true, false, null, 4, 900003, null, null, null, null, null, null, null, null, null, new Guid("a1000000-0000-0000-0000-000000990004"), null, new DateTime(2026, 10, 5, 13, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.InsertData(
                table: "MentorProfiles",
                columns: new[] { "MentorId", "Bio", "CreatedBy", "CreatedDate", "Expertise", "IntroductionVideoUrl", "IsDeleted", "JlptLevel", "ModifiedBy", "ModifiedDate", "RejectionReason", "ReviewNotes", "ReviewedAt", "ReviewedByAdminName", "SubmittedAt", "VerificationDocumentUrl", "VerificationStatus", "Workplace", "YearsOfExperience" },
                values: new object[,]
                {
                    { 900002, "Chuyên gia tiếng Nhật với 8 năm kinh nghiệm giảng dạy JLPT N1, N2 và tiếng Nhật doanh nghiệp.", null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Luyện thi JLPT N1-N2, Tiếng Nhật thương mại, Phỏng vấn xin việc", null, false, "N1", null, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Admin System", new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), null, "Approved", "Riki Nihongo Academy", 8 },
                    { 900003, "Giảng viên tiếng Nhật bản xứ, chuyên sâu về phát âm và giao tiếp Kaiwa thực tế.", null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Giao tiếp Kaiwa tự nhiên, Luyện phát âm chuẩn Tokyo, JLPT N3-N2", null, false, "N1", null, null, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Admin System", new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), null, "Approved", "Riki Nihongo Academy", 5 }
                });

            migrationBuilder.InsertData(
                table: "UserSubscriptions",
                columns: new[] { "Id", "AiGradingQuota", "AiGradingUsedCount", "AmountPaid", "CertificateLevelId", "CreatedBy", "CreatedDate", "DurationDaysSnapshot", "EndDate", "IsDeleted", "MeetingSessionsIncluded", "MeetingSessionsUsed", "MentorId", "MentorPracticeOutcomeJson", "MentorReviewCompletedAt", "MentorReviewPlan", "MentorReviewSessionAt", "ModifiedBy", "ModifiedDate", "PaymentExpiresAt", "PaymentStatus", "PaymentTransactionId", "PendingMentorAvailabilityId", "PendingMentorQuestion", "PlanCategorySnapshot", "PlanNameSnapshot", "PlanRevisionSnapshot", "PurchasedAt", "StartDate", "SubscriptionPlanId", "UserId" },
                values: new object[,]
                {
                    { 990001, 0, 0, 459000m, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Unspecified), false, 12, 1, null, null, null, null, null, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS", null, null, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, 900006 },
                    { 990002, 0, 0, 459000m, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Unspecified), false, 12, 1, null, null, null, null, null, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS-2", null, null, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, 900007 },
                    { 990003, 0, 0, 459000m, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Unspecified), false, 12, 1, null, null, null, null, null, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS-3", null, null, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, 900008 },
                    { 990004, 0, 0, 459000m, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Unspecified), false, 12, 1, null, null, null, null, null, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS-4", null, null, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, 900009 }
                });

            migrationBuilder.InsertData(
                table: "CertificateSections",
                columns: new[] { "Id", "CertificationLevelId", "Code", "CreatedBy", "CreatedDate", "Description", "DurationMinutes", "IsDeleted", "MaxScore", "ModifiedBy", "ModifiedDate", "Name", "PassingScore", "SortOrder" },
                values: new object[,]
                {
                    { 1, 1, "N5_VOCAB", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5285), "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N5", null, false, 0, null, null, "Kiến thức ngôn ngữ (Từ vựng)", 0, 0 },
                    { 2, 1, "N5_GRAMMAR_READING", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5292), "Phần thi kiểm tra cấu trúc ngữ pháp và đọc hiểu đoạn văn ngắn cấp độ N5", null, false, 0, null, null, "Kiến thức ngôn ngữ (Ngữ pháp) & Đọc hiểu", 0, 0 },
                    { 3, 1, "N5_LISTENING", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5294), "Phần thi nghe hiểu tình huống giao tiếp hàng ngày cấp độ N5", null, false, 0, null, null, "Nghe hiểu", 0, 0 },
                    { 4, 2, "N4_VOCAB", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5295), "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N4", null, false, 0, null, null, "Kiến thức ngôn ngữ (Từ vựng)", 0, 0 },
                    { 5, 2, "N4_GRAMMAR", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5296), "Phần thi kiểm tra cấu trúc ngữ pháp và sắp xếp câu cấp độ N4", null, false, 0, null, null, "Kiến thức ngôn ngữ (Ngữ pháp)", 0, 0 },
                    { 6, 2, "N4_READING", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5297), "Phần thi đọc hiểu văn bản, thông báo và biểu mẫu cấp độ N4", null, false, 0, null, null, "Đọc hiểu", 0, 0 },
                    { 7, 2, "N4_LISTENING", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5298), "Phần thi nghe hiểu hội thoại và tình huống thực tế cấp độ N4", null, false, 0, null, null, "Nghe hiểu", 0, 0 }
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
                columns: new[] { "Id", "AttendedAt", "CancellationReason", "CancelledAtUtc", "CancelledByRole", "CancelledByUserId", "CompensationCurrency", "CompensationFormulaVersion", "CompensationLevelId", "CompletedAt", "CreatedBy", "CreatedDate", "CreditGrantId", "ExceptionApproved", "ExceptionReason", "FrozenHourlyRate", "FrozenLevelCoefficient", "IsDeleted", "LearnerId", "MeetingLink", "MentorAvailabilityId", "ModifiedBy", "ModifiedDate", "PracticeFeedback", "PracticeFeedbackAt", "PracticeScore", "Question", "ScheduledAt", "ScheduledCompensationAmount", "Status", "UserSubscriptionId" },
                values: new object[,]
                {
                    { 990001, null, null, null, null, null, null, null, null, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, null, false, null, "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", 990001, null, null, null, null, null, "Meeting demo để kiểm thử phòng họp trên frontend.", new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified), null, "Assigned", 990001 },
                    { 990002, null, null, null, null, null, null, null, null, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, null, false, null, "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", 990001, null, null, null, null, null, "Em muốn nhờ Mentor giải đáp phần ngữ pháp N3 bài 5.", new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified), null, "Assigned", 990002 },
                    { 990003, null, null, null, null, null, null, null, null, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, null, false, null, "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", 990001, null, null, null, null, null, "Hỏi về cách phân biệt giữa ~てくる và ~ていく.", new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified), null, "Assigned", 990003 },
                    { 990004, null, null, null, null, null, null, null, null, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, null, false, null, "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001", 990001, null, null, null, null, null, "Tư vấn phương pháp luyện nghe JLPT N3 hiệu quả.", new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified), null, "Assigned", 990004 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_CreatedAtUtc",
                table: "AdminAuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_EntityType_EntityId",
                table: "AdminAuditLogs",
                columns: new[] { "EntityType", "EntityId" });

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
                name: "IX_CohortComplaintEvents_ComplaintId_Id",
                table: "CohortComplaintEvents",
                columns: new[] { "ComplaintId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_CohortId",
                table: "CohortComplaints",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_CourseId",
                table: "CohortComplaints",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_DecidedById",
                table: "CohortComplaints",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_LearnerId_IdempotencyKey",
                table: "CohortComplaints",
                columns: new[] { "LearnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_MentorId_Status_CreatedAtUtc",
                table: "CohortComplaints",
                columns: new[] { "MentorId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortComplaints_Status_CreatedAtUtc",
                table: "CohortComplaints",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortEnrollmentEntitlements_EntitlementReference",
                table: "CohortEnrollmentEntitlements",
                column: "EntitlementReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortEnrollmentEntitlements_LearnerId",
                table: "CohortEnrollmentEntitlements",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortEnrollmentEntitlements_PlanId",
                table: "CohortEnrollmentEntitlements",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortEnrollmentEntitlements_StudyCohortId_LearnerId",
                table: "CohortEnrollmentEntitlements",
                columns: new[] { "StudyCohortId", "LearnerId" },
                unique: true,
                filter: "\"Status\" = 'Active' AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CohortMentorReviews_CohortId",
                table: "CohortMentorReviews",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortMentorReviews_CourseId_MentorId",
                table: "CohortMentorReviews",
                columns: new[] { "CourseId", "MentorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortMentorReviews_LearnerId",
                table: "CohortMentorReviews",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortMentorReviews_MentorId_IsHidden_CreatedAtUtc",
                table: "CohortMentorReviews",
                columns: new[] { "MentorId", "IsHidden", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequestEvents_RequestId_Id",
                table: "CohortScheduleRequestEvents",
                columns: new[] { "RequestId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequests_CohortId_Id",
                table: "CohortScheduleRequests",
                columns: new[] { "CohortId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequests_PendingCohort",
                table: "CohortScheduleRequests",
                column: "CohortId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequests_RequestedById_IdempotencyKey",
                table: "CohortScheduleRequests",
                columns: new[] { "RequestedById", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequests_ReviewedById",
                table: "CohortScheduleRequests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_CohortScheduleRequests_Status_CreatedAtUtc",
                table: "CohortScheduleRequests",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequestEvents_RequestId_Id",
                table: "CohortTransferRequestEvents",
                columns: new[] { "RequestId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_DestinationCourseId",
                table: "CohortTransferRequests",
                column: "DestinationCourseId",
                unique: true,
                filter: "\"DestinationCourseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_LearnerId_IdempotencyKey",
                table: "CohortTransferRequests",
                columns: new[] { "LearnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_PendingSource",
                table: "CohortTransferRequests",
                column: "SourceCourseId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_PolicyId",
                table: "CohortTransferRequests",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_RequestedTargetCohortId",
                table: "CohortTransferRequests",
                column: "RequestedTargetCohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_ReviewedById",
                table: "CohortTransferRequests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_SourceCohortId",
                table: "CohortTransferRequests",
                column: "SourceCohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_Status_CreatedAtUtc",
                table: "CohortTransferRequests",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CohortTransferRequests_TargetCohortId",
                table: "CohortTransferRequests",
                column: "TargetCohortId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentAiReviews_ContentType_ContentId_ReviewedAt",
                table: "ContentAiReviews",
                columns: new[] { "ContentType", "ContentId", "ReviewedAt" });

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
                name: "IX_CourseAuditEntries_CourseId",
                table: "CourseAuditEntries",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseRetakeRequests_CourseId",
                table: "CourseRetakeRequests",
                column: "CourseId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_CourseScheduleRequests_CourseId_SessionId",
                table: "CourseScheduleRequests",
                columns: new[] { "CourseId", "SessionId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

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
                name: "IX_HomeworkAssignmentExercises_PracticeExerciseId",
                table: "HomeworkAssignmentExercises",
                column: "PracticeExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignmentLessons_LessonId",
                table: "HomeworkAssignmentLessons",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_CohortId_LearnerId",
                table: "HomeworkAssignments",
                columns: new[] { "CohortId", "LearnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_LearnerId_DueAt",
                table: "HomeworkAssignments",
                columns: new[] { "LearnerId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_MentorBookingId",
                table: "HomeworkAssignments",
                column: "MentorBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_MentorId",
                table: "HomeworkAssignments",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_StudyCourseId",
                table: "HomeworkAssignments",
                column: "StudyCourseId");

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
                name: "IX_JlptExamSchedules_SourceKey",
                table: "JlptExamSchedules",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JlptLevelPayCoefficients_CertificateLevelId_EffectiveFromUtc",
                table: "JlptLevelPayCoefficients",
                columns: new[] { "CertificateLevelId", "EffectiveFromUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JlptLevelPayCoefficients_ConfiguredByAdminId",
                table: "JlptLevelPayCoefficients",
                column: "ConfiguredByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_KanaCharacters_Character_Type",
                table: "KanaCharacters",
                columns: new[] { "Character", "Type" },
                unique: true);

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
                name: "IX_LearnerFeedback_LearnerId_TargetType_TargetId",
                table: "LearnerFeedback",
                columns: new[] { "LearnerId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnerFeedback_UpdatedAtUtc_Id",
                table: "LearnerFeedback",
                columns: new[] { "UpdatedAtUtc", "Id" });

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
                name: "IX_Lessons_TopicId",
                table: "Lessons",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_UserAccountId",
                table: "Lessons",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonVocabularies_VocabularyEntryId",
                table: "LessonVocabularies",
                column: "VocabularyEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAbuseReports_MentorBookingId",
                table: "MentorAbuseReports",
                column: "MentorBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAbuseReports_ReporterId",
                table: "MentorAbuseReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAbuseReports_Status",
                table: "MentorAbuseReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAbuseReports_TargetUserId",
                table: "MentorAbuseReports",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceApprovals_MentorBookingId",
                table: "MentorAttendanceApprovals",
                column: "MentorBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceApprovals_ReviewedByAdminId",
                table: "MentorAttendanceApprovals",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_Booking_User",
                table: "MentorAttendanceRecords",
                columns: new[] { "MentorBookingId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_CourseSessionId",
                table: "MentorAttendanceRecords",
                column: "CourseSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_RoomId",
                table: "MentorAttendanceRecords",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_RoomId_UserId",
                table: "MentorAttendanceRecords",
                columns: new[] { "RoomId", "UserId" },
                unique: true,
                filter: "\"MentorBookingId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_Status",
                table: "MentorAttendanceRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceRecords_UserId",
                table: "MentorAttendanceRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_GenerationKey",
                table: "MentorAvailabilities",
                column: "GenerationKey",
                unique: true,
                filter: "\"GenerationKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_GroupPolicyId",
                table: "MentorAvailabilities",
                column: "GroupPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_MentorId_StartTime_EndTime",
                table: "MentorAvailabilities",
                columns: new[] { "MentorId", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_MentorScheduleRuleId",
                table: "MentorAvailabilities",
                column: "MentorScheduleRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_MentorWeeklyAvailabilityId",
                table: "MentorAvailabilities",
                column: "MentorWeeklyAvailabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAvailabilities_RoomId",
                table: "MentorAvailabilities",
                column: "RoomId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBankAccounts_MentorUserId_AccountVersion",
                table: "MentorBankAccounts",
                columns: new[] { "MentorUserId", "AccountVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBankAccounts_MentorUserId_Current",
                table: "MentorBankAccounts",
                column: "MentorUserId",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBankAccounts_VerifiedByAdminId",
                table: "MentorBankAccounts",
                column: "VerifiedByAdminId");

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
                name: "IX_MentorBookings_CompensationLevelId",
                table: "MentorBookings",
                column: "CompensationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_CreditGrantId",
                table: "MentorBookings",
                column: "CreditGrantId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_MentorAvailabilityId_LearnerId",
                table: "MentorBookings",
                columns: new[] { "MentorAvailabilityId", "LearnerId" },
                unique: true,
                filter: "\"MentorAvailabilityId\" IS NOT NULL AND \"LearnerId\" IS NOT NULL AND NOT \"IsDeleted\" AND \"Status\" <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_MentorAvailabilityId_UserSubscriptionId",
                table: "MentorBookings",
                columns: new[] { "MentorAvailabilityId", "UserSubscriptionId" },
                unique: true,
                filter: "NOT \"IsDeleted\" AND \"Status\" <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_UserSubscriptionId_Status",
                table: "MentorBookings",
                columns: new[] { "UserSubscriptionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookingTerms_MentorAvailabilityId",
                table: "MentorBookingTerms",
                column: "MentorAvailabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorCalendarConnections_MentorId",
                table: "MentorCalendarConnections",
                column: "MentorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorCalendarConnections_StateHash",
                table: "MentorCalendarConnections",
                column: "StateHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorChatMessages_MentorBookingId_Id",
                table: "MentorChatMessages",
                columns: new[] { "MentorBookingId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorChatMessages_MentorBookingId_ReadAt_SenderUserId",
                table: "MentorChatMessages",
                columns: new[] { "MentorBookingId", "ReadAt", "SenderUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorChatMessages_SenderUserId",
                table: "MentorChatMessages",
                column: "SenderUserId");

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
                name: "IX_MentorCreditGrants_CertificateLevelId",
                table: "MentorCreditGrants",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorCreditGrants_LearnerId_CertificateLevelId_ExpiresAtUtc",
                table: "MentorCreditGrants",
                columns: new[] { "LearnerId", "CertificateLevelId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorCreditGrants_LearnerId_ExpiresAtUtc",
                table: "MentorCreditGrants",
                columns: new[] { "LearnerId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorCreditGrants_UserSubscriptionId",
                table: "MentorCreditGrants",
                column: "UserSubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputeHistories_DisputeId",
                table: "MentorDisputeHistories",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputes_LearnerId",
                table: "MentorDisputes",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputes_MentorBookingId",
                table: "MentorDisputes",
                column: "MentorBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputes_MentorId",
                table: "MentorDisputes",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputes_ResolvedByAdminId",
                table: "MentorDisputes",
                column: "ResolvedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorDisputes_Status",
                table: "MentorDisputes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_AttendanceApprovalId",
                table: "MentorEarningEntries",
                column: "AttendanceApprovalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_CertificateLevelId",
                table: "MentorEarningEntries",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_CreatedByAdminId",
                table: "MentorEarningEntries",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_MentorBookingId",
                table: "MentorEarningEntries",
                column: "MentorBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_MentorUserId_Status",
                table: "MentorEarningEntries",
                columns: new[] { "MentorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_OperationKey",
                table: "MentorEarningEntries",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_PayrollPeriodId",
                table: "MentorEarningEntries",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_CreatedByAdminId",
                table: "MentorGroupPolicies",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_EffectiveFromUtc",
                table: "MentorGroupPolicies",
                column: "EffectiveFromUtc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorGroupPolicies_Version",
                table: "MentorGroupPolicies",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorHourlyRates_ConfiguredByAdminId",
                table: "MentorHourlyRates",
                column: "ConfiguredByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorHourlyRates_MentorUserId_EffectiveFromUtc",
                table: "MentorHourlyRates",
                columns: new[] { "MentorUserId", "EffectiveFromUtc" },
                unique: true);

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
                name: "IX_MentorPayoutAttempts_ConfirmedByAdminId",
                table: "MentorPayoutAttempts",
                column: "ConfirmedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_IdempotencyKey",
                table: "MentorPayoutAttempts",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_InitiatedByAdminId",
                table: "MentorPayoutAttempts",
                column: "InitiatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_LockedBankAccountId",
                table: "MentorPayoutAttempts",
                column: "LockedBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_OneSucceededPerObligation",
                table: "MentorPayoutAttempts",
                column: "PayoutObligationId",
                unique: true,
                filter: "\"InternalStatus\" = 'Succeeded'");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_PayoutObligationId_AttemptNumber",
                table: "MentorPayoutAttempts",
                columns: new[] { "PayoutObligationId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_SucceededTransferReference",
                table: "MentorPayoutAttempts",
                column: "TransferReference",
                unique: true,
                filter: "\"TransferReference\" IS NOT NULL AND \"InternalStatus\" = 'Succeeded'");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutObligations_ApprovedByAdminId",
                table: "MentorPayoutObligations",
                column: "ApprovedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutObligations_LockedBankAccountId",
                table: "MentorPayoutObligations",
                column: "LockedBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutObligations_MentorUserId",
                table: "MentorPayoutObligations",
                column: "MentorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutObligations_OrganizationPayoutAccountId",
                table: "MentorPayoutObligations",
                column: "OrganizationPayoutAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutObligations_PayrollPeriodId",
                table: "MentorPayoutObligations",
                column: "PayrollPeriodId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollAdjustments_ApprovedByAdminId",
                table: "MentorPayrollAdjustments",
                column: "ApprovedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollAdjustments_OperationKey",
                table: "MentorPayrollAdjustments",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollAdjustments_PayrollPeriodId",
                table: "MentorPayrollAdjustments",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollLines_EarningEntryId",
                table: "MentorPayrollLines",
                column: "EarningEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollLines_MentorBookingId",
                table: "MentorPayrollLines",
                column: "MentorBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollLines_PayrollPeriodId",
                table: "MentorPayrollLines",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollPeriods_ApprovedByAdminId",
                table: "MentorPayrollPeriods",
                column: "ApprovedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollPeriods_MentorUserId_Year_Month_SequenceNumber~",
                table: "MentorPayrollPeriods",
                columns: new[] { "MentorUserId", "Year", "Month", "SequenceNumber", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollPeriods_PreparedByAdminId",
                table: "MentorPayrollPeriods",
                column: "PreparedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorReviews_LearnerId",
                table: "MentorReviews",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorReviews_Mentor_Public_Status",
                table: "MentorReviews",
                columns: new[] { "MentorId", "IsPublic", "ModerationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorReviews_MentorBookingId",
                table: "MentorReviews",
                column: "MentorBookingId",
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

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingQualifications_CertificateLevelId",
                table: "MentorTeachingQualifications",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingQualifications_MentorUserId_CertificateLevelId",
                table: "MentorTeachingQualifications",
                columns: new[] { "MentorUserId", "CertificateLevelId" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_MentorTeachingQualifications_VerifiedByAdminId",
                table: "MentorTeachingQualifications",
                column: "VerifiedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorWeeklyAvailabilities_MentorId_Date_Period",
                table: "MentorWeeklyAvailabilities",
                columns: new[] { "MentorId", "Date", "Period" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

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
                name: "IX_MockTestAttempts_UserId_AttemptKind",
                table: "MockTestAttempts",
                columns: new[] { "UserId", "AttemptKind" },
                unique: true,
                filter: "\"AttemptKind\" = 1 AND \"IsCompleted\" = false AND \"IsDeleted\" = false");

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
                name: "IX_Notifications_IdempotencyKey",
                table: "Notifications",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPayoutAccounts_ChannelCode",
                table: "OrganizationPayoutAccounts",
                column: "ChannelCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPayoutAccounts_ConfiguredByAdminId",
                table: "OrganizationPayoutAccounts",
                column: "ConfiguredByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExerciseAttempts_HomeworkAssignmentId_PracticeExerc~",
                table: "PracticeExerciseAttempts",
                columns: new[] { "HomeworkAssignmentId", "PracticeExerciseId" },
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
                name: "IX_RecommendedLearningPaths_UserId",
                table: "RecommendedLearningPaths",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_RecommendedLearningPaths_UserId_GeneratedAt",
                table: "RecommendedLearningPaths",
                columns: new[] { "UserId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecommendedLearningPaths_UserId_Version",
                table: "RecommendedLearningPaths",
                columns: new[] { "UserId", "Version" },
                unique: true);

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
                name: "IX_StudyCohortMentorAssignments_AssignedById",
                table: "StudyCohortMentorAssignments",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortMentorAssignments_CohortId_Id",
                table: "StudyCohortMentorAssignments",
                columns: new[] { "CohortId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortMentorAssignments_IdempotencyKey",
                table: "StudyCohortMentorAssignments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortMentorAssignments_MentorId",
                table: "StudyCohortMentorAssignments",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortMentorAssignments_PolicyId",
                table: "StudyCohortMentorAssignments",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortMentorAssignments_PreviousMentorId",
                table: "StudyCohortMentorAssignments",
                column: "PreviousMentorId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohorts_GroupPolicyId",
                table: "StudyCohorts",
                column: "GroupPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohorts_MentorId",
                table: "StudyCohorts",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohorts_PlanId",
                table: "StudyCohorts",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortSessions_AvailabilityId",
                table: "StudyCohortSessions",
                column: "AvailabilityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCohortSessions_CohortId_Number",
                table: "StudyCohortSessions",
                columns: new[] { "CohortId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourseHomework_CourseId",
                table: "StudyCourseHomework",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCoursePlans_CertificateLevelId",
                table: "StudyCoursePlans",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_CertificateLevelId",
                table: "StudyCourses",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_Cohort_ActiveLearner",
                table: "StudyCourses",
                columns: new[] { "CohortId", "LearnerId" },
                unique: true,
                filter: "\"CohortId\" IS NOT NULL AND \"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_CohortId",
                table: "StudyCourses",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_GroupPolicyId",
                table: "StudyCourses",
                column: "GroupPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_LearnerId_PurchaseKey",
                table: "StudyCourses",
                columns: new[] { "LearnerId", "PurchaseKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_MentorId",
                table: "StudyCourses",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_PlanId",
                table: "StudyCourses",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_ProviderPaymentReference",
                table: "StudyCourses",
                column: "ProviderPaymentReference",
                unique: true,
                filter: "\"ProviderPaymentReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_RetakeOfId",
                table: "StudyCourses",
                column: "RetakeOfId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourses_TransferredFromCourseId",
                table: "StudyCourses",
                column: "TransferredFromCourseId",
                unique: true,
                filter: "\"TransferredFromCourseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourseSessions_AvailabilityId",
                table: "StudyCourseSessions",
                column: "AvailabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyCourseSessions_CourseId_Number_Attempt",
                table: "StudyCourseSessions",
                columns: new[] { "CourseId", "Number", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_CertificateLevelId",
                table: "SubscriptionPlans",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDeviceTokens_FcmToken",
                table: "UserDeviceTokens",
                column: "FcmToken");

            migrationBuilder.CreateIndex(
                name: "IX_UserDeviceTokens_UserId_DeviceIdentifier",
                table: "UserDeviceTokens",
                columns: new[] { "UserId", "DeviceIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDeviceTokens_UserId_IsActive",
                table: "UserDeviceTokens",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_AssessedCertificateLevelId",
                table: "Users",
                column: "AssessedCertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleSubject",
                table: "Users",
                column: "GoogleSubject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_SelfDeclaredCertificateLevelId",
                table: "Users",
                column: "SelfDeclaredCertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_StartingCertificateLevelId",
                table: "Users",
                column: "StartingCertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TargetCertificationLevelId",
                table: "Users",
                column: "TargetCertificationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_CertificateLevelId",
                table: "UserSubscriptions",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_MentorId",
                table: "UserSubscriptions",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PendingMentorAvailabilityId",
                table: "UserSubscriptions",
                column: "PendingMentorAvailabilityId");

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
                name: "AdminAuditLogs");

            migrationBuilder.DropTable(
                name: "AiCreditTopUps");

            migrationBuilder.DropTable(
                name: "CohortComplaintEvents");

            migrationBuilder.DropTable(
                name: "CohortEnrollmentEntitlements");

            migrationBuilder.DropTable(
                name: "CohortMentorReviews");

            migrationBuilder.DropTable(
                name: "CohortScheduleRequestEvents");

            migrationBuilder.DropTable(
                name: "CohortTransferRequestEvents");

            migrationBuilder.DropTable(
                name: "ContentAiReviews");

            migrationBuilder.DropTable(
                name: "ContentApprovalRequests");

            migrationBuilder.DropTable(
                name: "ContentAudioTranscriptions");

            migrationBuilder.DropTable(
                name: "ContentLevelMappings");

            migrationBuilder.DropTable(
                name: "CourseAuditEntries");

            migrationBuilder.DropTable(
                name: "CourseRetakeRequests");

            migrationBuilder.DropTable(
                name: "CourseScheduleRequests");

            migrationBuilder.DropTable(
                name: "EmailVerifications");

            migrationBuilder.DropTable(
                name: "FeatureSubscriptionPlan");

            migrationBuilder.DropTable(
                name: "GradingResults");

            migrationBuilder.DropTable(
                name: "HomeworkAssignmentExercises");

            migrationBuilder.DropTable(
                name: "HomeworkAssignmentLessons");

            migrationBuilder.DropTable(
                name: "JlptExamSchedules");

            migrationBuilder.DropTable(
                name: "JlptLevelPayCoefficients");

            migrationBuilder.DropTable(
                name: "KanaWritingPracticeHistories");

            migrationBuilder.DropTable(
                name: "LearnerEssays");

            migrationBuilder.DropTable(
                name: "LearnerFeedback");

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
                name: "MentorAbuseReports");

            migrationBuilder.DropTable(
                name: "MentorAttendanceRecords");

            migrationBuilder.DropTable(
                name: "MentorBookingDayGuards");

            migrationBuilder.DropTable(
                name: "MentorBookingIntents");

            migrationBuilder.DropTable(
                name: "MentorBookingReservations");

            migrationBuilder.DropTable(
                name: "MentorBookingTerms");

            migrationBuilder.DropTable(
                name: "MentorCalendarConnections");

            migrationBuilder.DropTable(
                name: "MentorChatMessages");

            migrationBuilder.DropTable(
                name: "MentorCreditEntries");

            migrationBuilder.DropTable(
                name: "MentorDisputeHistories");

            migrationBuilder.DropTable(
                name: "MentorHourlyRates");

            migrationBuilder.DropTable(
                name: "MentorOutboxEvents");

            migrationBuilder.DropTable(
                name: "MentorPayoutAttempts");

            migrationBuilder.DropTable(
                name: "MentorPayrollAdjustments");

            migrationBuilder.DropTable(
                name: "MentorPayrollLines");

            migrationBuilder.DropTable(
                name: "MentorProfiles");

            migrationBuilder.DropTable(
                name: "MentorReviews");

            migrationBuilder.DropTable(
                name: "MentorTeachingQualifications");

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
                name: "StudyCohortMentorAssignments");

            migrationBuilder.DropTable(
                name: "StudyCohortSessions");

            migrationBuilder.DropTable(
                name: "StudyCourseHomework");

            migrationBuilder.DropTable(
                name: "UserDeviceTokens");

            migrationBuilder.DropTable(
                name: "CohortComplaints");

            migrationBuilder.DropTable(
                name: "CohortScheduleRequests");

            migrationBuilder.DropTable(
                name: "CohortTransferRequests");

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
                name: "StudyCourseSessions");

            migrationBuilder.DropTable(
                name: "MentorDisputes");

            migrationBuilder.DropTable(
                name: "MentorPayoutObligations");

            migrationBuilder.DropTable(
                name: "MentorEarningEntries");

            migrationBuilder.DropTable(
                name: "MockQuestionOptions");

            migrationBuilder.DropTable(
                name: "MockTestAttempts");

            migrationBuilder.DropTable(
                name: "ReviewCards");

            migrationBuilder.DropTable(
                name: "KanaCharacters");

            migrationBuilder.DropTable(
                name: "HomeworkAssignments");

            migrationBuilder.DropTable(
                name: "PracticeQuestions");

            migrationBuilder.DropTable(
                name: "MentorBankAccounts");

            migrationBuilder.DropTable(
                name: "OrganizationPayoutAccounts");

            migrationBuilder.DropTable(
                name: "MentorAttendanceApprovals");

            migrationBuilder.DropTable(
                name: "MentorPayrollPeriods");

            migrationBuilder.DropTable(
                name: "MockQuestions");

            migrationBuilder.DropTable(
                name: "GrammarPatterns");

            migrationBuilder.DropTable(
                name: "LearnerNoteEntries");

            migrationBuilder.DropTable(
                name: "StudyCourses");

            migrationBuilder.DropTable(
                name: "PracticeExercises");

            migrationBuilder.DropTable(
                name: "MentorBookings");

            migrationBuilder.DropTable(
                name: "MockTestSections");

            migrationBuilder.DropTable(
                name: "Kanjis");

            migrationBuilder.DropTable(
                name: "LearnerNotes");

            migrationBuilder.DropTable(
                name: "Vocabularies");

            migrationBuilder.DropTable(
                name: "StudyCohorts");

            migrationBuilder.DropTable(
                name: "Lessons");

            migrationBuilder.DropTable(
                name: "MentorCreditGrants");

            migrationBuilder.DropTable(
                name: "CertificateSections");

            migrationBuilder.DropTable(
                name: "MockTests");

            migrationBuilder.DropTable(
                name: "StudyCoursePlans");

            migrationBuilder.DropTable(
                name: "LanguageSkills");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "MentorAvailabilities");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "MentorGroupPolicies");

            migrationBuilder.DropTable(
                name: "MentorScheduleRules");

            migrationBuilder.DropTable(
                name: "MentorWeeklyAvailabilities");

            migrationBuilder.DropTable(
                name: "MentorSchedules");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "CertificateLevels");

            migrationBuilder.DropTable(
                name: "CertificateTypes");
        }
    }
}
