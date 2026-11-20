using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceCatalogDataIntegrityAndOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "CertificateTypes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "CertificateLevels",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "CertificateLevels",
                keyColumn: "Id",
                keyValue: 1,
                column: "Revision",
                value: 1);

            migrationBuilder.UpdateData(
                table: "CertificateLevels",
                keyColumn: "Id",
                keyValue: 2,
                column: "Revision",
                value: 1);

            migrationBuilder.UpdateData(
                table: "CertificateLevels",
                keyColumn: "Id",
                keyValue: 3,
                column: "Revision",
                value: 1);

            migrationBuilder.UpdateData(
                table: "CertificateLevels",
                keyColumn: "Id",
                keyValue: 4,
                column: "Revision",
                value: 1);

            migrationBuilder.UpdateData(
                table: "CertificateLevels",
                keyColumn: "Id",
                keyValue: 5,
                column: "Revision",
                value: 1);

            migrationBuilder.UpdateData(
                table: "CertificateTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "Revision",
                value: 1);

            migrationBuilder.Sql("""
                DO $catalog_repair$
                DECLARE
                    dup_cert text;
                    dup_level text;
                BEGIN
                    -- Trim leading/trailing whitespace safely if no collision results
                    UPDATE "CertificateTypes"
                    SET "Name" = BTRIM("Name")
                    WHERE "Name" <> BTRIM("Name");

                    UPDATE "CertificateLevels"
                    SET "Code" = UPPER(BTRIM("Code"))
                    WHERE "Code" <> UPPER(BTRIM("Code"));

                    -- Fail closed if normalized duplicates already exist
                    SELECT string_agg(LOWER("Name"), ', ')
                    INTO dup_cert
                    FROM "CertificateTypes"
                    GROUP BY LOWER("Name")
                    HAVING COUNT(*) > 1;
                    IF dup_cert IS NOT NULL THEN
                        RAISE EXCEPTION 'Aborted migration: duplicate normalized CertificateType names detected (%). Resolve ambiguity before enforcing uniqueness.', dup_cert;
                    END IF;

                    SELECT string_agg(format('(%s, %s)', "CertificationId", UPPER("Code")), ', ')
                    INTO dup_level
                    FROM "CertificateLevels"
                    GROUP BY "CertificationId", UPPER("Code")
                    HAVING COUNT(*) > 1;
                    IF dup_level IS NOT NULL THEN
                        RAISE EXCEPTION 'Aborted migration: duplicate normalized CertificateLevel codes detected (%). Resolve ambiguity before enforcing uniqueness.', dup_level;
                    END IF;

                    -- Resequence CertificateTypes continuously to 1..N while preserving IDs and relative order
                    IF EXISTS (
                        SELECT 1
                        FROM (
                            SELECT "Id", "SortOrder",
                                   ROW_NUMBER() OVER (ORDER BY "SortOrder" ASC, "Id" ASC) AS expected_order
                            FROM "CertificateTypes"
                        ) s
                        WHERE s."SortOrder" <> s.expected_order OR s."SortOrder" < 1
                    ) THEN
                        WITH ranked AS (
                            SELECT "Id",
                                   ROW_NUMBER() OVER (ORDER BY "SortOrder" ASC, "Id" ASC) AS new_order
                            FROM "CertificateTypes"
                        )
                        UPDATE "CertificateTypes" ct
                        SET "SortOrder" = ranked.new_order,
                            "ModifiedDate" = NOW() AT TIME ZONE 'UTC'
                        FROM ranked
                        WHERE ct."Id" = ranked."Id"
                          AND ct."SortOrder" IS DISTINCT FROM ranked.new_order;
                    END IF;

                    -- Resequence CertificateLevels per CertificationId continuously to 1..N while preserving IDs and relative order
                    IF EXISTS (
                        SELECT 1
                        FROM (
                            SELECT "Id", "SortOrder",
                                   ROW_NUMBER() OVER (PARTITION BY "CertificationId" ORDER BY "SortOrder" ASC, "Id" ASC) AS expected_order
                            FROM "CertificateLevels"
                        ) s
                        WHERE s."SortOrder" <> s.expected_order OR s."SortOrder" < 1
                    ) THEN
                        WITH ranked AS (
                            SELECT "Id",
                                   ROW_NUMBER() OVER (PARTITION BY "CertificationId" ORDER BY "SortOrder" ASC, "Id" ASC) AS new_order
                            FROM "CertificateLevels"
                        )
                        UPDATE "CertificateLevels" cl
                        SET "SortOrder" = ranked.new_order,
                            "ModifiedDate" = NOW() AT TIME ZONE 'UTC'
                        FROM ranked
                        WHERE cl."Id" = ranked."Id"
                          AND cl."SortOrder" IS DISTINCT FROM ranked.new_order;
                    END IF;

                    -- Safely repair any seeded CertificateSections still carrying SortOrder <= 0
                    IF EXISTS (SELECT 1 FROM "CertificateSections" WHERE "SortOrder" <= 0) THEN
                        WITH level_base AS (
                            SELECT "CertificationLevelId", COALESCE(MAX(CASE WHEN "SortOrder" > 0 THEN "SortOrder" ELSE 0 END), 0) AS max_pos
                            FROM "CertificateSections"
                            GROUP BY "CertificationLevelId"
                        ),
                        to_fix AS (
                            SELECT cs."Id",
                                   lb.max_pos + ROW_NUMBER() OVER (PARTITION BY cs."CertificationLevelId" ORDER BY cs."Id" ASC) AS new_order
                            FROM "CertificateSections" cs
                            JOIN level_base lb ON lb."CertificationLevelId" = cs."CertificationLevelId"
                            WHERE cs."SortOrder" <= 0
                        )
                        UPDATE "CertificateSections" cs
                        SET "SortOrder" = to_fix.new_order,
                            "ModifiedDate" = NOW() AT TIME ZONE 'UTC'
                        FROM to_fix
                        WHERE cs."Id" = to_fix."Id";
                    END IF;
                END $catalog_repair$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CertificateTypes_SortOrder",
                table: "CertificateTypes",
                sql: "\"SortOrder\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CertificateLevels_SortOrder",
                table: "CertificateLevels",
                sql: "\"SortOrder\" >= 1");

            migrationBuilder.Sql("""
                DO $catalog_constraints$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conrelid = 'public."CertificateTypes"'::regclass
                          AND conname = 'UQ_CertificateTypes_SortOrder'
                    ) THEN
                        ALTER TABLE "CertificateTypes"
                            ADD CONSTRAINT "UQ_CertificateTypes_SortOrder" UNIQUE ("SortOrder") DEFERRABLE INITIALLY DEFERRED;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conrelid = 'public."CertificateLevels"'::regclass
                          AND conname = 'UQ_CertificateLevels_CertificationId_SortOrder'
                    ) THEN
                        ALTER TABLE "CertificateLevels"
                            ADD CONSTRAINT "UQ_CertificateLevels_CertificationId_SortOrder" UNIQUE ("CertificationId", "SortOrder") DEFERRABLE INITIALLY DEFERRED;
                    END IF;
                END $catalog_constraints$;

                CREATE UNIQUE INDEX IF NOT EXISTS "UQ_CertificateTypes_NormalizedName"
                    ON "CertificateTypes" (LOWER(BTRIM("Name")));

                CREATE UNIQUE INDEX IF NOT EXISTS "UQ_CertificateLevels_CertificationId_NormalizedCode"
                    ON "CertificateLevels" ("CertificationId", UPPER(BTRIM("Code")));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "UQ_CertificateTypes_NormalizedName";
                DROP INDEX IF EXISTS "UQ_CertificateLevels_CertificationId_NormalizedCode";
                ALTER TABLE "CertificateTypes" DROP CONSTRAINT IF EXISTS "UQ_CertificateTypes_SortOrder";
                ALTER TABLE "CertificateLevels" DROP CONSTRAINT IF EXISTS "UQ_CertificateLevels_CertificationId_SortOrder";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_CertificateTypes_SortOrder",
                table: "CertificateTypes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CertificateLevels_SortOrder",
                table: "CertificateLevels");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "CertificateTypes");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "CertificateLevels");
        }
    }
}
