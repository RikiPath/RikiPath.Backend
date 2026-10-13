using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentApprovalRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GrammarPatterns_Users_ContentAuthorId",
                table: "GrammarPatterns");

            migrationBuilder.DropForeignKey(
                name: "FK_Kanjis_Users_ContentAuthorId",
                table: "Kanjis");

            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_Users_ContentAuthorId",
                table: "Lessons");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Users_MentorId",
                table: "Notes");

            migrationBuilder.DropForeignKey(
                name: "FK_PracticeExercises_Users_ContentAuthorId",
                table: "PracticeExercises");

            migrationBuilder.DropForeignKey(
                name: "FK_Vocabularies_Users_ContentAuthorId",
                table: "Vocabularies");

            migrationBuilder.DropIndex(
                name: "IX_Vocabularies_ContentAuthorId",
                table: "Vocabularies");

            migrationBuilder.DropIndex(
                name: "IX_PracticeExercises_ContentAuthorId",
                table: "PracticeExercises");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_ContentAuthorId",
                table: "Lessons");

            migrationBuilder.DropIndex(
                name: "IX_Kanjis_ContentAuthorId",
                table: "Kanjis");

            migrationBuilder.DropIndex(
                name: "IX_GrammarPatterns_ContentAuthorId",
                table: "GrammarPatterns");

            migrationBuilder.DropColumn(
                name: "ContentAuthorId",
                table: "Vocabularies");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Vocabularies");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "Vocabularies");

            migrationBuilder.DropColumn(
                name: "ReviewedByName",
                table: "Vocabularies");

            migrationBuilder.DropColumn(
                name: "ReviewedDate",
                table: "Vocabularies");

            migrationBuilder.DropColumn(
                name: "ContentAuthorId",
                table: "PracticeExercises");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "PracticeExercises");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "PracticeExercises");

            migrationBuilder.DropColumn(
                name: "ReviewedByName",
                table: "PracticeExercises");

            migrationBuilder.DropColumn(
                name: "ReviewedDate",
                table: "PracticeExercises");

            migrationBuilder.DropColumn(
                name: "AnswerText",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "ContentAuthorId",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "ReviewedByName",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "ReviewedDate",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "ContentAuthorId",
                table: "Kanjis");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Kanjis");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "Kanjis");

            migrationBuilder.DropColumn(
                name: "ReviewedByName",
                table: "Kanjis");

            migrationBuilder.DropColumn(
                name: "ReviewedDate",
                table: "Kanjis");

            migrationBuilder.DropColumn(
                name: "ContentAuthorId",
                table: "GrammarPatterns");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "GrammarPatterns");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "GrammarPatterns");

            migrationBuilder.DropColumn(
                name: "ReviewedByName",
                table: "GrammarPatterns");

            migrationBuilder.DropColumn(
                name: "ReviewedDate",
                table: "GrammarPatterns");

            migrationBuilder.RenameColumn(
                name: "MentorId",
                table: "Notes",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "MeetingNotes",
                table: "Notes",
                newName: "Content");

            migrationBuilder.RenameIndex(
                name: "IX_Notes_MentorId",
                table: "Notes",
                newName: "IX_Notes_UserId");

            migrationBuilder.AddColumn<int>(
                name: "UserAccountId",
                table: "Vocabularies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorRole",
                table: "Notes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UserAccountId",
                table: "Lessons",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserAccountId",
                table: "Kanjis",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserAccountId",
                table: "GrammarPatterns",
                type: "integer",
                nullable: true);

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

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910001,
                column: "UserAccountId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910002,
                column: "UserAccountId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910003,
                column: "UserAccountId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910004,
                column: "UserAccountId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910005,
                column: "UserAccountId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910006,
                column: "UserAccountId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910007,
                column: "UserAccountId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910008,
                column: "UserAccountId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910009,
                column: "UserAccountId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910010,
                column: "UserAccountId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Vocabularies_UserAccountId",
                table: "Vocabularies",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_UserAccountId",
                table: "Lessons",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Kanjis_UserAccountId",
                table: "Kanjis",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPatterns_UserAccountId",
                table: "GrammarPatterns",
                column: "UserAccountId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_GrammarPatterns_Users_UserAccountId",
                table: "GrammarPatterns",
                column: "UserAccountId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Kanjis_Users_UserAccountId",
                table: "Kanjis",
                column: "UserAccountId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_Users_UserAccountId",
                table: "Lessons",
                column: "UserAccountId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Users_UserId",
                table: "Notes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vocabularies_Users_UserAccountId",
                table: "Vocabularies",
                column: "UserAccountId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GrammarPatterns_Users_UserAccountId",
                table: "GrammarPatterns");

            migrationBuilder.DropForeignKey(
                name: "FK_Kanjis_Users_UserAccountId",
                table: "Kanjis");

            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_Users_UserAccountId",
                table: "Lessons");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Users_UserId",
                table: "Notes");

            migrationBuilder.DropForeignKey(
                name: "FK_Vocabularies_Users_UserAccountId",
                table: "Vocabularies");

            migrationBuilder.DropTable(
                name: "ContentApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_Vocabularies_UserAccountId",
                table: "Vocabularies");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_UserAccountId",
                table: "Lessons");

            migrationBuilder.DropIndex(
                name: "IX_Kanjis_UserAccountId",
                table: "Kanjis");

            migrationBuilder.DropIndex(
                name: "IX_GrammarPatterns_UserAccountId",
                table: "GrammarPatterns");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "Vocabularies");

            migrationBuilder.DropColumn(
                name: "AuthorRole",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "Kanjis");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "GrammarPatterns");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Notes",
                newName: "MentorId");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "Notes",
                newName: "MeetingNotes");

            migrationBuilder.RenameIndex(
                name: "IX_Notes_UserId",
                table: "Notes",
                newName: "IX_Notes_MentorId");

            migrationBuilder.AddColumn<int>(
                name: "ContentAuthorId",
                table: "Vocabularies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Vocabularies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "Vocabularies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByName",
                table: "Vocabularies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedDate",
                table: "Vocabularies",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContentAuthorId",
                table: "PracticeExercises",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "PracticeExercises",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "PracticeExercises",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByName",
                table: "PracticeExercises",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedDate",
                table: "PracticeExercises",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnswerText",
                table: "Notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContentAuthorId",
                table: "Lessons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "Lessons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Lessons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "Lessons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByName",
                table: "Lessons",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedDate",
                table: "Lessons",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "Lessons",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ContentAuthorId",
                table: "Kanjis",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Kanjis",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "Kanjis",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByName",
                table: "Kanjis",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedDate",
                table: "Kanjis",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContentAuthorId",
                table: "GrammarPatterns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "GrammarPatterns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "GrammarPatterns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByName",
                table: "GrammarPatterns",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedDate",
                table: "GrammarPatterns",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910001,
                columns: new[] { "ContentAuthorId", "IsApproved", "ReviewNote", "ReviewedByName", "ReviewedDate" },
                values: new object[] { 900004, true, null, null, null });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910002,
                columns: new[] { "ContentAuthorId", "IsApproved", "ReviewNote", "ReviewedByName", "ReviewedDate" },
                values: new object[] { 900004, true, null, null, null });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910003,
                columns: new[] { "ContentAuthorId", "IsApproved", "ReviewNote", "ReviewedByName", "ReviewedDate" },
                values: new object[] { 900004, true, null, null, null });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910004,
                columns: new[] { "ContentAuthorId", "IsApproved", "ReviewNote", "ReviewedByName", "ReviewedDate" },
                values: new object[] { 900004, true, null, null, null });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910005,
                columns: new[] { "ContentAuthorId", "IsApproved", "ReviewNote", "ReviewedByName", "ReviewedDate" },
                values: new object[] { 900004, true, null, null, null });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910006,
                columns: new[] { "ContentAuthorId", "IsApproved", "ReviewNote", "ReviewedByName", "ReviewedDate" },
                values: new object[] { 900004, true, null, null, null });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910007,
                columns: new[] { "ContentAuthorId", "IsApproved", "ReviewNote", "ReviewedByName", "ReviewedDate" },
                values: new object[] { 900004, true, null, null, null });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910008,
                columns: new[] { "ContentAuthorId", "IsApproved", "ReviewNote", "ReviewedByName", "ReviewedDate" },
                values: new object[] { 900004, true, null, null, null });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910009,
                columns: new[] { "ContentAuthorId", "IsApproved", "ReviewNote", "ReviewedByName", "ReviewedDate" },
                values: new object[] { 900004, true, null, null, null });

            migrationBuilder.UpdateData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910010,
                columns: new[] { "ContentAuthorId", "IsApproved", "ReviewNote", "ReviewedByName", "ReviewedDate" },
                values: new object[] { 900004, true, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Vocabularies_ContentAuthorId",
                table: "Vocabularies",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeExercises_ContentAuthorId",
                table: "PracticeExercises",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_ContentAuthorId",
                table: "Lessons",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Kanjis_ContentAuthorId",
                table: "Kanjis",
                column: "ContentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPatterns_ContentAuthorId",
                table: "GrammarPatterns",
                column: "ContentAuthorId");

            migrationBuilder.AddForeignKey(
                name: "FK_GrammarPatterns_Users_ContentAuthorId",
                table: "GrammarPatterns",
                column: "ContentAuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Kanjis_Users_ContentAuthorId",
                table: "Kanjis",
                column: "ContentAuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_Users_ContentAuthorId",
                table: "Lessons",
                column: "ContentAuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Users_MentorId",
                table: "Notes",
                column: "MentorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeExercises_Users_ContentAuthorId",
                table: "PracticeExercises",
                column: "ContentAuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vocabularies_Users_ContentAuthorId",
                table: "Vocabularies",
                column: "ContentAuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
