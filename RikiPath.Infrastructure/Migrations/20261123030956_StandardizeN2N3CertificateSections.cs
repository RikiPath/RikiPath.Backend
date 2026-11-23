using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeN2N3CertificateSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $section_guard$
                BEGIN
                    IF (SELECT count(*) FROM "CertificateLevels" l JOIN "CertificateTypes" t ON t."Id" = l."CertificationId"
                        WHERE t."Name" = 'JLPT' AND l."Code" IN ('N3', 'N2') AND NOT l."IsDeleted") <> 2 THEN
                        RAISE EXCEPTION 'Expected exactly one active JLPT N3 and N2 level';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "CertificateSections" WHERE NOT "IsDeleted"
                        GROUP BY "CertificationLevelId", "Code" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Duplicate active CertificateSection codes';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "CertificateSections" s JOIN "CertificateLevels" l
                        ON l."Id" = s."CertificationLevelId" WHERE l."Code" IN ('N3', 'N2')
                        AND s."Code" IN ('N3_VOCAB','N3_GRAMMAR','N3_READING','N3_LISTENING',
                                         'N2_VOCAB','N2_GRAMMAR','N2_READING','N2_LISTENING') AND s."IsDeleted") THEN
                        RAISE EXCEPTION 'A canonical N3/N2 section exists but is deleted; review it before migration';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "MockTestSections" m JOIN "CertificateSections" s
                        ON s."Id" = m."CertificationSectionId" LEFT JOIN "LanguageSkills" k ON k."Id" = m."SkillId"
                        WHERE s."Code" IN ('N3_GENERAL','N2_GENERAL')
                        AND (k."Name" IS NULL OR k."Name" NOT IN ('Vocabulary','Grammar','Reading','Listening'))) THEN
                        RAISE EXCEPTION 'A GENERAL MockTestSection has an unmappable skill';
                    END IF;
                END $section_guard$;

                WITH desired(level_code, suffix, section_name, sort_order) AS (VALUES
                    ('N3','VOCAB','Kiến thức ngôn ngữ (Từ vựng)',1),
                    ('N3','GRAMMAR','Kiến thức ngôn ngữ (Ngữ pháp)',2),
                    ('N3','READING','Đọc hiểu',3),
                    ('N3','LISTENING','Nghe hiểu',4),
                    ('N2','VOCAB','Kiến thức ngôn ngữ (Từ vựng)',1),
                    ('N2','GRAMMAR','Kiến thức ngôn ngữ (Ngữ pháp)',2),
                    ('N2','READING','Đọc hiểu',3),
                    ('N2','LISTENING','Nghe hiểu',4))
                INSERT INTO "CertificateSections"
                    ("CertificationLevelId", "Code", "Name", "SortOrder", "MaxScore", "PassingScore", "IsDeleted", "CreatedDate")
                SELECT l."Id", d.level_code || '_' || d.suffix, d.section_name, d.sort_order,
                    0, 0, false, now() AT TIME ZONE 'UTC'
                FROM desired d JOIN "CertificateLevels" l ON l."Code" = d.level_code
                JOIN "CertificateTypes" t ON t."Id" = l."CertificationId" AND t."Name" = 'JLPT'
                WHERE NOT l."IsDeleted" AND NOT EXISTS (
                    SELECT 1 FROM "CertificateSections" existing
                    WHERE existing."CertificationLevelId" = l."Id" AND existing."Code" = d.level_code || '_' || d.suffix);

                UPDATE "MockTestSections" m SET "CertificationSectionId" = target."Id"
                FROM "CertificateSections" old, "CertificateSections" target,
                     "CertificateLevels" level, "LanguageSkills" skill
                WHERE old."Id" = m."CertificationSectionId"
                  AND level."Id" = old."CertificationLevelId"
                  AND skill."Id" = m."SkillId"
                  AND old."Code" = level."Code" || '_GENERAL'
                  AND level."Code" IN ('N3', 'N2')
                  AND target."CertificationLevelId" = level."Id" AND NOT target."IsDeleted"
                  AND target."Code" = level."Code" || '_' || CASE skill."Name"
                      WHEN 'Vocabulary' THEN 'VOCAB' WHEN 'Grammar' THEN 'GRAMMAR'
                      WHEN 'Reading' THEN 'READING' WHEN 'Listening' THEN 'LISTENING' END;

                DO $section_guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "MockTestSections" m JOIN "CertificateSections" s
                        ON s."Id" = m."CertificationSectionId"
                        WHERE s."Code" IN ('N3_GENERAL','N2_GENERAL')) THEN
                        RAISE EXCEPTION 'GENERAL sections still have Mock Test references';
                    END IF;
                END $section_guard$;

                UPDATE "CertificateSections" s SET "IsDeleted" = true
                FROM "CertificateLevels" l WHERE l."Id" = s."CertificationLevelId"
                    AND l."Code" IN ('N3','N2') AND s."Code" = l."Code" || '_GENERAL' AND NOT s."IsDeleted";

                WITH ordering(code, position) AS (VALUES
                    ('N5_VOCAB',1),('N5_GRAMMAR_READING',2),('N5_LISTENING',3),
                    ('N4_VOCAB',1),('N4_GRAMMAR',2),('N4_READING',3),('N4_LISTENING',4),
                    ('N3_VOCAB',1),('N3_GRAMMAR',2),('N3_READING',3),('N3_LISTENING',4),
                    ('N2_VOCAB',1),('N2_GRAMMAR',2),('N2_READING',3),('N2_LISTENING',4))
                UPDATE "CertificateSections" s SET "SortOrder" = o.position
                FROM ordering o WHERE s."Code" = o.code AND NOT s."IsDeleted" AND s."SortOrder" = 0;
                """);

            migrationBuilder.DropIndex(
                name: "IX_CertificateSections_CertificationLevelId_Code",
                table: "CertificateSections");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateSections_CertificationLevelId_Code",
                table: "CertificateSections",
                columns: new[] { "CertificationLevelId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CertificateSections_CertificationLevelId_Code",
                table: "CertificateSections");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateSections_CertificationLevelId_Code",
                table: "CertificateSections",
                columns: new[] { "CertificationLevelId", "Code" });
        }
    }
}
