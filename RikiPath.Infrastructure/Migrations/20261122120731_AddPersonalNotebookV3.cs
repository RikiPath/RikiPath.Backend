using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalNotebookV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearnerNoteEntries_VocabularyListId",
                table: "LearnerNoteEntries");

            migrationBuilder.AddColumn<int>(
                name: "FolderId",
                table: "LearnerNotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "LearnerNotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "LearnerNotes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "LearnerNotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContextSnippet",
                table: "LearnerNoteEntries",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExampleMeaning",
                table: "LearnerNoteEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExampleSentence",
                table: "LearnerNoteEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GrammarPatternId",
                table: "LearnerNoteEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HighlightColor",
                table: "LearnerNoteEntries",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "LearnerNoteEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ItemKind",
                table: "LearnerNoteEntries",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "manual");

            migrationBuilder.AddColumn<int>(
                name: "NotebookPageId",
                table: "LearnerNoteEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "LearnerNoteEntries",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "LearnerNoteEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceId",
                table: "LearnerNoteEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePath",
                table: "LearnerNoteEntries",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceTitle",
                table: "LearnerNoteEntries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "LearnerNoteEntries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "LearnerNoteEntries",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateTable(
                name: "NotebookFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotebookFolders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotebookPages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LearnerNoteId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{\"type\":\"doc\",\"version\":1,\"blocks\":[]}'::jsonb"),
                    ContentPlainText = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    ContentFormatVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ChecklistTotalCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ChecklistCompletedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    FavoritedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SourceId = table.Column<int>(type: "integer", nullable: true),
                    SourcePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SourceTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastAutosavedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotebookPages_LearnerNotes_LearnerNoteId",
                        column: x => x.LearnerNoteId,
                        principalTable: "LearnerNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotebookPages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearnerNotes_FolderId",
                table: "LearnerNotes",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerNotes_UserId_FolderId_IsDeleted_SortOrder",
                table: "LearnerNotes",
                columns: new[] { "UserId", "FolderId", "IsDeleted", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LearnerNotes_UserId_IsDefault",
                table: "LearnerNotes",
                column: "UserId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerNoteEntries_GrammarPatternId",
                table: "LearnerNoteEntries",
                column: "GrammarPatternId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerNoteEntries_NotebookPageId_IsDeleted_SortOrder",
                table: "LearnerNoteEntries",
                columns: new[] { "NotebookPageId", "IsDeleted", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LearnerNoteEntries_VocabularyListId_IsDeleted",
                table: "LearnerNoteEntries",
                columns: new[] { "VocabularyListId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_NotebookFolders_UserId_IsDefault",
                table: "NotebookFolders",
                column: "UserId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookFolders_UserId_IsDeleted_SortOrder",
                table: "NotebookFolders",
                columns: new[] { "UserId", "IsDeleted", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_NotebookFolders_UserId_NormalizedName",
                table: "NotebookFolders",
                columns: new[] { "UserId", "NormalizedName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookPages_LearnerNoteId_IsDeleted_SortOrder",
                table: "NotebookPages",
                columns: new[] { "LearnerNoteId", "IsDeleted", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_NotebookPages_UserId_IsDefault",
                table: "NotebookPages",
                column: "UserId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookPages_UserId_IsDeleted_IsFavorite",
                table: "NotebookPages",
                columns: new[] { "UserId", "IsDeleted", "IsFavorite" });

            migrationBuilder.AddForeignKey(
                name: "FK_LearnerNoteEntries_GrammarPatterns_GrammarPatternId",
                table: "LearnerNoteEntries",
                column: "GrammarPatternId",
                principalTable: "GrammarPatterns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LearnerNoteEntries_NotebookPages_NotebookPageId",
                table: "LearnerNoteEntries",
                column: "NotebookPageId",
                principalTable: "NotebookPages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LearnerNotes_NotebookFolders_FolderId",
                table: "LearnerNotes",
                column: "FolderId",
                principalTable: "NotebookFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Idempotent zero-loss backfill from legacy 2-level LearnerNotes/LearnerNoteEntries into V3 Folder -> File -> Page -> SavedItem hierarchy.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    rec RECORD;
                    ere RECORD;
                    v_match text[];
                    v_meta jsonb;
                    v_folder_name text;
                    v_norm_folder text;
                    v_file_name text;
                    v_clean_desc text;
                    v_body text;
                    v_blocks jsonb;
                    v_doc jsonb;
                    v_folder_id int;
                    v_page_id int;
                    v_has_default_folder boolean;
                    v_has_default_file boolean;
                    v_has_default_page boolean;
                    v_fav boolean;
                    v_src_path text;
                    v_src_title text;
                    v_kind text;
                    v_color text;
                    v_clean_note text;
                    v_tags jsonb;
                    v_chk_total int;
                    v_chk_done int;
                BEGIN
                    FOR rec IN
                        SELECT "Id", "UserId", "Name", "Description", "FolderId", "CreatedDate", "ModifiedDate"
                        FROM "LearnerNotes"
                        WHERE NOT "IsDeleted"
                        ORDER BY "Id"
                    LOOP
                        v_meta := '{}'::jsonb;
                        v_match := regexp_match(COALESCE(rec."Description", ''), '<!--rp-nb:(\{[\s\S]*?\})-->');
                        IF v_match IS NOT NULL AND array_length(v_match, 1) >= 1 THEN
                            BEGIN
                                v_meta := v_match[1]::jsonb;
                            EXCEPTION WHEN OTHERS THEN
                                v_meta := '{}'::jsonb;
                            END;
                        END IF;

                        v_folder_name := NULLIF(BTRIM(COALESCE(v_meta ->> 'folder', '')), '');
                        IF v_folder_name IS NULL AND POSITION(' :: ' IN COALESCE(rec."Name", '')) > 0 THEN
                            v_folder_name := NULLIF(BTRIM(split_part(rec."Name", ' :: ', 1)), '');
                        END IF;
                        IF v_folder_name IS NULL THEN
                            v_folder_name := 'Sổ tay chung';
                        END IF;
                        v_folder_name := LEFT(v_folder_name, 120);
                        v_norm_folder := UPPER(v_folder_name);

                        IF POSITION(' :: ' IN COALESCE(rec."Name", '')) > 0 THEN
                            v_file_name := NULLIF(BTRIM(SUBSTRING(rec."Name" FROM POSITION(' :: ' IN rec."Name") + 4)), '');
                        ELSE
                            v_file_name := NULLIF(BTRIM(COALESCE(rec."Name", '')), '');
                        END IF;
                        IF v_file_name IS NULL THEN
                            v_file_name := 'Ghi chú chung';
                        END IF;
                        v_file_name := LEFT(v_file_name, 200);

                        v_clean_desc := NULLIF(BTRIM(regexp_replace(COALESCE(rec."Description", ''), '<!--rp-nb:[\s\S]*?-->', '', 'g')), '');

                        SELECT "Id" INTO v_folder_id
                        FROM "NotebookFolders"
                        WHERE "UserId" = rec."UserId" AND "NormalizedName" = v_norm_folder AND NOT "IsDeleted"
                        LIMIT 1;

                        IF v_folder_id IS NULL THEN
                            SELECT EXISTS (
                                SELECT 1 FROM "NotebookFolders"
                                WHERE "UserId" = rec."UserId" AND "IsDefault" AND NOT "IsDeleted"
                            ) INTO v_has_default_folder;

                            INSERT INTO "NotebookFolders" (
                                "UserId", "Name", "NormalizedName", "SortOrder", "IsDefault", "Revision", "CreatedDate", "ModifiedDate", "IsDeleted"
                            )
                            VALUES (
                                rec."UserId",
                                v_folder_name,
                                v_norm_folder,
                                0,
                                (NOT v_has_default_folder AND v_folder_name = 'Sổ tay chung'),
                                1,
                                COALESCE(rec."CreatedDate", NOW() AT TIME ZONE 'UTC'),
                                COALESCE(rec."ModifiedDate", NOW() AT TIME ZONE 'UTC'),
                                false
                            )
                            RETURNING "Id" INTO v_folder_id;
                        END IF;

                        IF rec."FolderId" IS NULL THEN
                            SELECT EXISTS (
                                SELECT 1 FROM "LearnerNotes"
                                WHERE "UserId" = rec."UserId" AND "IsDefault" AND NOT "IsDeleted"
                            ) INTO v_has_default_file;

                            UPDATE "LearnerNotes"
                            SET "FolderId" = v_folder_id,
                                "Name" = v_file_name,
                                "Description" = v_clean_desc,
                                "IsDefault" = ("IsDefault" OR (NOT v_has_default_file AND v_file_name IN ('Ghi chú nhanh', 'Ghi chú tổng quan'))),
                                "ModifiedDate" = COALESCE("ModifiedDate", "CreatedDate", NOW() AT TIME ZONE 'UTC')
                            WHERE "Id" = rec."Id";
                        END IF;

                        SELECT "Id" INTO v_page_id
                        FROM "NotebookPages"
                        WHERE "LearnerNoteId" = rec."Id" AND NOT "IsDeleted"
                        ORDER BY "Id"
                        LIMIT 1;

                        IF v_page_id IS NULL THEN
                            v_body := COALESCE(NULLIF(BTRIM(COALESCE(v_meta ->> 'body', '')), ''), COALESCE(v_clean_desc, ''), '');
                            IF v_body = '' THEN
                                v_blocks := '[]'::jsonb;
                            ELSE
                                SELECT COALESCE(jsonb_agg(
                                    jsonb_build_object(
                                        'id', 'mig_' || line_idx::text,
                                        'type', CASE
                                            WHEN line_txt LIKE '### %' THEN 'heading'
                                            WHEN line_txt LIKE '## %' THEN 'heading'
                                            WHEN line_txt LIKE '# %' THEN 'heading'
                                            WHEN line_txt LIKE '- [x] %' OR line_txt LIKE '- [X] %' THEN 'checklist_item'
                                            WHEN line_txt LIKE '- [ ] %' THEN 'checklist_item'
                                            WHEN line_txt LIKE '- %' OR line_txt LIKE '* %' THEN 'bullet_item'
                                            ELSE 'paragraph'
                                        END,
                                        'runs', CASE
                                            WHEN BTRIM(line_txt) = '' THEN '[]'::jsonb
                                            ELSE jsonb_build_array(jsonb_build_object(
                                                'text', CASE
                                                    WHEN line_txt LIKE '### %' THEN SUBSTRING(line_txt FROM 5)
                                                    WHEN line_txt LIKE '## %' THEN SUBSTRING(line_txt FROM 4)
                                                    WHEN line_txt LIKE '# %' THEN SUBSTRING(line_txt FROM 3)
                                                    WHEN line_txt LIKE '- [x] %' OR line_txt LIKE '- [X] %' OR line_txt LIKE '- [ ] %' THEN SUBSTRING(line_txt FROM 7)
                                                    WHEN line_txt LIKE '- %' OR line_txt LIKE '* %' THEN SUBSTRING(line_txt FROM 3)
                                                    ELSE line_txt
                                                END
                                            ))
                                        END
                                    )
                                    || CASE
                                        WHEN line_txt LIKE '### %' THEN '{"level":3}'::jsonb
                                        WHEN line_txt LIKE '## %' THEN '{"level":2}'::jsonb
                                        WHEN line_txt LIKE '# %' THEN '{"level":1}'::jsonb
                                        WHEN line_txt LIKE '- [x] %' OR line_txt LIKE '- [X] %' THEN '{"checked":true}'::jsonb
                                        WHEN line_txt LIKE '- [ ] %' THEN '{"checked":false}'::jsonb
                                        ELSE '{}'::jsonb
                                    END
                                ), '[]'::jsonb)
                                INTO v_blocks
                                FROM regexp_split_to_table(REPLACE(REPLACE(v_body, E'\r\n', E'\n'), E'\r', E'\n'), E'\n') WITH ORDINALITY AS t(line_txt, line_idx);
                            END IF;

                            v_doc := jsonb_build_object('type', 'doc', 'version', 1, 'blocks', v_blocks);
                            SELECT
                                COUNT(*) FILTER (WHERE b ->> 'type' = 'checklist_item'),
                                COUNT(*) FILTER (WHERE b ->> 'type' = 'checklist_item' AND COALESCE((b ->> 'checked')::boolean, false))
                            INTO v_chk_total, v_chk_done
                            FROM jsonb_array_elements(v_blocks) AS b;

                            BEGIN
                                v_fav := COALESCE((v_meta ->> 'favorite')::boolean, false);
                            EXCEPTION WHEN OTHERS THEN
                                v_fav := false;
                            END;
                            v_src_path := NULLIF(LEFT(COALESCE(v_meta ->> 'sourcePath', ''), 300), '');
                            v_src_title := NULLIF(LEFT(COALESCE(v_meta ->> 'sourceTitle', ''), 200), '');

                            SELECT EXISTS (
                                SELECT 1 FROM "NotebookPages"
                                WHERE "UserId" = rec."UserId" AND "IsDefault" AND NOT "IsDeleted"
                            ) INTO v_has_default_page;

                            INSERT INTO "NotebookPages" (
                                "UserId", "LearnerNoteId", "Title", "ContentJson", "ContentPlainText",
                                "ContentFormatVersion", "ChecklistTotalCount", "ChecklistCompletedCount",
                                "IsFavorite", "FavoritedAtUtc", "IsDefault", "SourcePath", "SourceTitle",
                                "SortOrder", "Revision", "CreatedDate", "ModifiedDate", "IsDeleted"
                            )
                            VALUES (
                                rec."UserId",
                                rec."Id",
                                v_file_name,
                                v_doc,
                                v_body,
                                1,
                                COALESCE(v_chk_total, 0),
                                COALESCE(v_chk_done, 0),
                                v_fav,
                                CASE WHEN v_fav THEN COALESCE(rec."ModifiedDate", rec."CreatedDate", NOW() AT TIME ZONE 'UTC') ELSE NULL END,
                                (NOT v_has_default_page AND v_folder_name = 'Sổ tay chung'),
                                v_src_path,
                                v_src_title,
                                1,
                                1,
                                COALESCE(rec."CreatedDate", NOW() AT TIME ZONE 'UTC'),
                                COALESCE(rec."ModifiedDate", rec."CreatedDate", NOW() AT TIME ZONE 'UTC'),
                                false
                            )
                            RETURNING "Id" INTO v_page_id;
                        END IF;
                    END LOOP;

                    FOR ere IN
                        SELECT "Id", "VocabularyListId", "VocabularyEntryId", "KanjiEntryId", "Note"
                        FROM "LearnerNoteEntries"
                        WHERE NOT "IsDeleted" AND "NotebookPageId" IS NULL
                        ORDER BY "Id"
                    LOOP
                        SELECT "Id" INTO v_page_id
                        FROM "NotebookPages"
                        WHERE "LearnerNoteId" = ere."VocabularyListId" AND NOT "IsDeleted"
                        ORDER BY "Id"
                        LIMIT 1;

                        v_meta := '{}'::jsonb;
                        v_match := regexp_match(COALESCE(ere."Note", ''), '<!--rp-entry:(\{[\s\S]*?\})-->');
                        IF v_match IS NOT NULL AND array_length(v_match, 1) >= 1 THEN
                            BEGIN
                                v_meta := v_match[1]::jsonb;
                            EXCEPTION WHEN OTHERS THEN
                                v_meta := '{}'::jsonb;
                            END;
                        END IF;

                        v_kind := CASE
                            WHEN ere."VocabularyEntryId" IS NOT NULL THEN 'vocabulary'
                            WHEN ere."KanjiEntryId" IS NOT NULL THEN 'kanji'
                            WHEN LOWER(COALESCE(v_meta ->> 'kind', '')) IN ('vocabulary', 'kanji', 'grammar', 'highlight', 'note', 'manual')
                                THEN LOWER(v_meta ->> 'kind')
                            ELSE 'manual'
                        END;

                        v_color := CASE
                            WHEN LOWER(COALESCE(v_meta ->> 'color', '')) IN ('yellow', 'mint', 'sakura')
                                THEN LOWER(v_meta ->> 'color')
                            ELSE NULL
                        END;

                        v_src_path := NULLIF(LEFT(COALESCE(v_meta ->> 'sourcePath', ''), 300), '');
                        v_src_title := NULLIF(LEFT(COALESCE(v_meta ->> 'sourceTitle', ''), 200), '');
                        v_fav := false;
                        BEGIN
                            v_fav := COALESCE((v_meta ->> 'favorite')::boolean, false);
                        EXCEPTION WHEN OTHERS THEN
                            v_fav := false;
                        END;

                        v_tags := CASE
                            WHEN jsonb_typeof(v_meta -> 'tags') = 'array' THEN v_meta -> 'tags'
                            ELSE '[]'::jsonb
                        END;

                        v_clean_note := NULLIF(BTRIM(regexp_replace(COALESCE(ere."Note", ''), '<!--rp-entry:[\s\S]*?-->', '', 'g')), '');

                        UPDATE "LearnerNoteEntries"
                        SET "NotebookPageId" = v_page_id,
                            "ItemKind" = v_kind,
                            "HighlightColor" = v_color,
                            "SourcePath" = COALESCE("SourcePath", v_src_path),
                            "SourceTitle" = COALESCE("SourceTitle", v_src_title),
                            "ExampleSentence" = COALESCE("ExampleSentence", NULLIF(LEFT(COALESCE(v_meta ->> 'example', ''), 500), '')),
                            "ExampleMeaning" = COALESCE("ExampleMeaning", NULLIF(LEFT(COALESCE(v_meta ->> 'exampleMeaning', ''), 500), '')),
                            "TagsJson" = v_tags,
                            "IsFavorite" = v_fav,
                            "Note" = v_clean_note,
                            "ModifiedDate" = COALESCE("ModifiedDate", "CreatedDate", NOW() AT TIME ZONE 'UTC')
                        WHERE "Id" = ere."Id";
                    END LOOP;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearnerNoteEntries_GrammarPatterns_GrammarPatternId",
                table: "LearnerNoteEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_LearnerNoteEntries_NotebookPages_NotebookPageId",
                table: "LearnerNoteEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_LearnerNotes_NotebookFolders_FolderId",
                table: "LearnerNotes");

            migrationBuilder.DropTable(
                name: "NotebookFolders");

            migrationBuilder.DropTable(
                name: "NotebookPages");

            migrationBuilder.DropIndex(
                name: "IX_LearnerNotes_FolderId",
                table: "LearnerNotes");

            migrationBuilder.DropIndex(
                name: "IX_LearnerNotes_UserId_FolderId_IsDeleted_SortOrder",
                table: "LearnerNotes");

            migrationBuilder.DropIndex(
                name: "IX_LearnerNotes_UserId_IsDefault",
                table: "LearnerNotes");

            migrationBuilder.DropIndex(
                name: "IX_LearnerNoteEntries_GrammarPatternId",
                table: "LearnerNoteEntries");

            migrationBuilder.DropIndex(
                name: "IX_LearnerNoteEntries_NotebookPageId_IsDeleted_SortOrder",
                table: "LearnerNoteEntries");

            migrationBuilder.DropIndex(
                name: "IX_LearnerNoteEntries_VocabularyListId_IsDeleted",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "LearnerNotes");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "LearnerNotes");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "LearnerNotes");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "LearnerNotes");

            migrationBuilder.DropColumn(
                name: "ContextSnippet",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "ExampleMeaning",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "ExampleSentence",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "GrammarPatternId",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "HighlightColor",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "ItemKind",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "NotebookPageId",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "SourcePath",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "SourceTitle",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "LearnerNoteEntries");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "LearnerNoteEntries");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerNoteEntries_VocabularyListId",
                table: "LearnerNoteEntries",
                column: "VocabularyListId");
        }
    }
}
