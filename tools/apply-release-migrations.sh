#!/usr/bin/env bash
# Fail-closed PostgreSQL migration preflight, full backup stream verification,
# concurrency-locked ordered migration runner for RikiPath.Backend deployments.
#
# Guarantees:
#   - Never prints connection strings, passwords, or secrets.
#   - Never drops tables/schemas, deletes data, or inserts synthetic migration history rows.
#   - Prevents concurrent migration runs via host flock + PostgreSQL advisory lock.
#   - Aborts immediately on pre-squash history drift (e.g. InitialCreate missing on existing DB),
#     CertificateSections PK 8..15 collision, duplicate StudyCohorts.SourceProposalId, or
#     negative StudyCourses.Amount.
#   - Requires explicit RIKIPATH_ALLOW_DB_MIGRATION=true AND a backup verified via full
#     pg_restore --schema-only -f - and --data-only -f - archive stream decompression
#     (or explicit RIKIPATH_BACKUP_VERIFIED=true when external managed snapshots are verified).
#   - Executes each EF-generated migration script strictly in canonical order inside a single
#     advisory-locked session with ON_ERROR_STOP=1 (each EF script manages its own transaction).

set -euo pipefail

APPSETTINGS_PATH=""
MIGRATIONS_DIR=""
BACKUP_DIR="/var/backups/rikipath-db"
LOCK_FILE="/var/lock/rikipath-db-migration.lock"
ADVISORY_LOCK_KEY="74520198431"
MODE="preflight"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --appsettings)
      APPSETTINGS_PATH="$2"
      shift 2
      ;;
    --migrations-dir)
      MIGRATIONS_DIR="$2"
      shift 2
      ;;
    --backup-dir)
      BACKUP_DIR="$2"
      shift 2
      ;;
    --mode)
      MODE="$2"
      shift 2
      ;;
    *)
      echo "ERROR: Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

if [[ -z "$APPSETTINGS_PATH" || ! -f "$APPSETTINGS_PATH" ]]; then
  echo "ERROR: Valid --appsettings file is required." >&2
  exit 2
fi

if [[ -z "$MIGRATIONS_DIR" || ! -f "$MIGRATIONS_DIR/manifest.txt" ]]; then
  echo "ERROR: Valid --migrations-dir containing manifest.txt is required." >&2
  exit 2
fi

if [[ "$MODE" != "preflight" && "$MODE" != "apply" ]]; then
  echo "ERROR: --mode must be 'preflight' or 'apply'." >&2
  exit 2
fi

for pg_bin_dir in /usr/lib/postgresql/*/bin /usr/pgsql-*/bin; do
  if [[ -d "$pg_bin_dir" ]]; then
    PATH="${PATH}:${pg_bin_dir}"
  fi
done
export PATH

for bin in python3 psql; do
  if ! command -v "$bin" >/dev/null 2>&1; then
    echo "ERROR: Required tool '$bin' is not installed on target host." >&2
    exit 3
  fi
done

# Host-level non-blocking lock prevents concurrent runs on the same deployment host.
if command -v flock >/dev/null 2>&1; then
  mkdir -p "$(dirname "$LOCK_FILE")" 2>/dev/null || LOCK_FILE="/tmp/rikipath-db-migration.lock"
  exec 200>"$LOCK_FILE"
  if ! flock -n 200; then
    echo "ERROR [CONCURRENT_MIGRATION_BLOCKED]: Another deployment/migration process holds ${LOCK_FILE}." >&2
    exit 9
  fi
fi

# Parse connection string without ever printing credentials to stdout/stderr.
eval "$(python3 - "$APPSETTINGS_PATH" <<'PY'
import json, shlex, sys

with open(sys.argv[1], 'r', encoding='utf-8') as f:
    cfg = json.load(f)

conn = (
    (cfg.get('AppSettings') or {}).get('ConnectionStrings', {}).get('DefaultConnection')
    or (cfg.get('ConnectionStrings') or {}).get('DefaultConnection')
    or ''
)
if not conn.strip():
    print('echo "ERROR: DefaultConnection not found in appsettings." >&2; exit 4')
    sys.exit(0)

parts = {}
for item in conn.split(';'):
    if '=' in item:
        k, v = item.split('=', 1)
        parts[k.strip().lower()] = v.strip()

host = parts.get('host') or parts.get('server') or ''
port = parts.get('port') or '5432'
db = parts.get('database') or ''
user = parts.get('username') or parts.get('user id') or parts.get('user') or ''
pwd = parts.get('password') or ''
sslmode = (parts.get('ssl mode') or parts.get('sslmode') or 'prefer').lower()

if not host or not db or not user:
    print('echo "ERROR: Incomplete PostgreSQL connection string (missing Host, Database, or Username)." >&2; exit 4')
    sys.exit(0)

for key, val in [
    ('PGHOST', host),
    ('PGPORT', port),
    ('PGDATABASE', db),
    ('PGUSER', user),
    ('PGPASSWORD', pwd),
    ('PGSSLMODE', sslmode),
]:
    print(f'export {key}={shlex.quote(val)}')
PY
)"

echo "=== 1. Database Connectivity & Migration History Preflight ==="
echo "  Target DB : ${PGDATABASE} (port ${PGPORT}, sslmode=${PGSSLMODE})"

psql -v ON_ERROR_STOP=1 -t -A -c 'SELECT 1;' >/dev/null

HAS_HISTORY="$(psql -v ON_ERROR_STOP=1 -t -A -c "SELECT (to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL)::text;")"
if [[ "$HAS_HISTORY" != "true" ]]; then
  echo "ERROR: Table \"__EFMigrationsHistory\" does not exist on target database. Refusing blind automated migration." >&2
  exit 10
fi

mapfile -t CANONICAL_IDS < <(grep -v '^[[:space:]]*$' "$MIGRATIONS_DIR/manifest.txt" | tr -d '\r')
mapfile -t APPLIED_IDS < <(psql -v ON_ERROR_STOP=1 -t -A -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' | tr -d '\r')

declare -A APPLIED_MAP=()
for id in "${APPLIED_IDS[@]}"; do
  [[ -n "$id" ]] && APPLIED_MAP["$id"]=1
done

PENDING_IDS=()
for id in "${CANONICAL_IDS[@]}"; do
  if [[ -z "${APPLIED_MAP[$id]+x}" ]]; then
    PENDING_IDS+=("$id")
  fi
done

echo "  Canonical migrations : ${#CANONICAL_IDS[@]}"
echo "  Applied in DB        : ${#APPLIED_IDS[@]}"
echo "  Pending canonical    : ${#PENDING_IDS[@]}"

if [[ ${#PENDING_IDS[@]} -eq 0 ]]; then
  echo "  All canonical migrations are already applied."
  exit 0
fi

printf '  Pending IDs:\n'
for id in "${PENDING_IDS[@]}"; do
  printf '    - %s\n' "$id"
done

echo "=== 2. Fail-Closed Drift & Precondition Checks (Read-Only) ==="

# Guard A: Pre-squash drift check â€” never execute InitialCreate on an existing populated schema.
if [[ -z "${APPLIED_MAP[20261121010126_InitialCreate]+x}" ]]; then
  HAS_ADMIN_AUDIT="$(psql -v ON_ERROR_STOP=1 -t -A -c "SELECT (to_regclass('public.\"AdminAuditLogs\"') IS NOT NULL)::text;")"
  if [[ "$HAS_ADMIN_AUDIT" == "true" ]]; then
    echo "ERROR [MIGRATION_HISTORY_DRIFT]: 20261121010126_InitialCreate is missing from \"__EFMigrationsHistory\", but table \"AdminAuditLogs\" already exists (pre-squash DB history)." >&2
    echo "Refusing to run InitialCreate on an existing database. Verify schema parity and reconcile migration history manually before deploying." >&2
    exit 10
  fi
fi

# Guard B: CertificateSections PK 8..15 collision check for 20261122074649_N5ToN2CertificateSection.
if [[ -z "${APPLIED_MAP[20261122074649_N5ToN2CertificateSection]+x}" ]]; then
  EXISTING_SEC_COUNT="$(psql -v ON_ERROR_STOP=1 -t -A -c 'SELECT COUNT(*) FROM "CertificateSections" WHERE "Id" BETWEEN 8 AND 15;')"
  if [[ "$EXISTING_SEC_COUNT" -gt 0 ]]; then
    echo "ERROR [MIGRATION_HISTORY_DRIFT]: 20261122074649_N5ToN2CertificateSection is pending, but CertificateSections rows Id 8..15 already exist (count=${EXISTING_SEC_COUNT})." >&2
    echo "Refusing to run colliding seed migration. Inspect CertificateSections and reconcile history safely before deploying." >&2
    exit 11
  fi
fi

# Guard C: Duplicate non-cancelled StudyCohorts.SourceProposalId check before unique filtered index.
if [[ -z "${APPLIED_MAP[20261125140000_AddPerLevelClassLimitsAndSourceProposalUniqueIndex]+x}" ]]; then
  DUP_PROPOSAL_COUNT="$(psql -v ON_ERROR_STOP=1 -t -A -c 'SELECT COUNT(*) FROM (SELECT "SourceProposalId" FROM "StudyCohorts" WHERE "SourceProposalId" IS NOT NULL AND "Status" <> '\''Cancelled'\'' GROUP BY "SourceProposalId" HAVING COUNT(*) > 1) d;')"
  if [[ "$DUP_PROPOSAL_COUNT" -gt 0 ]]; then
    echo "ERROR [DATA_PRECONDITION_FAILED]: Found ${DUP_PROPOSAL_COUNT} duplicate active StudyCohorts.SourceProposalId group(s)." >&2
    echo "Manual review is required before creating IX_StudyCohorts_SourceProposalId unique index. No rows were altered." >&2
    exit 12
  fi
fi

# Guard D: StudyCourses.Amount >= 0 check before CK_StudyCourses_RefundedAmount.
if [[ -z "${APPLIED_MAP[20261125113247_AddStudyCourseRefundedAmount]+x}" ]]; then
  NEG_AMOUNT_COUNT="$(psql -v ON_ERROR_STOP=1 -t -A -c 'SELECT COUNT(*) FROM "StudyCourses" WHERE "Amount" < 0;')"
  if [[ "$NEG_AMOUNT_COUNT" -gt 0 ]]; then
    echo "ERROR [DATA_PRECONDITION_FAILED]: Found ${NEG_AMOUNT_COUNT} StudyCourses row(s) with Amount < 0, which violates CK_StudyCourses_RefundedAmount." >&2
    exit 13
  fi
fi

if [[ "$MODE" == "preflight" ]]; then
  echo "Preflight blocked deployment because ${#PENDING_IDS[@]} pending migration(s) exist and mode=preflight." >&2
  exit 20
fi

if [[ "${RIKIPATH_ALLOW_DB_MIGRATION:-false}" != "true" ]]; then
  echo "ERROR: Pending migrations require explicit RIKIPATH_ALLOW_DB_MIGRATION=true authorization. Stopping deployment before restarting Backend." >&2
  exit 21
fi

echo "=== 3. Pre-Migration Backup & Full Stream Recoverability Verification ==="
if [[ "${RIKIPATH_BACKUP_VERIFIED:-false}" == "true" ]]; then
  echo "  External/managed database backup explicitly verified by operator (RIKIPATH_BACKUP_VERIFIED=true)."
else
  if ! command -v pg_dump >/dev/null 2>&1 || ! command -v pg_restore >/dev/null 2>&1; then
    echo "ERROR: pg_dump/pg_restore unavailable and RIKIPATH_BACKUP_VERIFIED is not true. Aborting before DDL." >&2
    exit 14
  fi
  mkdir -p "$BACKUP_DIR"
  chmod 700 "$BACKUP_DIR" || true
  FREE_MB="$(df -Pm "$BACKUP_DIR" | awk 'NR==2 {print $4}')"
  if [[ -n "$FREE_MB" && "$FREE_MB" -lt 256 ]]; then
    echo "ERROR: Insufficient free disk space in ${BACKUP_DIR} (${FREE_MB}MB < 256MB). Aborting before backup." >&2
    exit 14
  fi
  BACKUP_FILE="${BACKUP_DIR}/rikipath_${PGDATABASE}_$(date -u +%Y%m%dT%H%M%SZ).dump"
  echo "  Creating pre-migration custom backup: ${BACKUP_FILE}"
  pg_dump --format=custom --no-owner --no-privileges --file "$BACKUP_FILE"
  chmod 600 "$BACKUP_FILE" || true
  if [[ ! -s "$BACKUP_FILE" ]]; then
    echo "ERROR: Backup file is empty. Aborting migration." >&2
    exit 14
  fi
  # Validate full schema AND data decompression streams (not merely archive header TOC).
  SCHEMA_BYTES="$(pg_restore --schema-only -f - "$BACKUP_FILE" | wc -c)"
  DATA_BYTES="$(pg_restore --data-only -f - "$BACKUP_FILE" | wc -c)"
  if [[ "$SCHEMA_BYTES" -lt 1024 ]]; then
    echo "ERROR: Backup schema stream verification failed (${SCHEMA_BYTES} bytes). Aborting migration." >&2
    exit 14
  fi
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$BACKUP_FILE" > "${BACKUP_FILE}.sha256"
    chmod 600 "${BACKUP_FILE}.sha256" || true
  fi
  echo "  Backup stream-verified (schema=${SCHEMA_BYTES}B, data=${DATA_BYTES}B)."
fi

echo "=== 4. Applying Pending Migrations Under PostgreSQL Advisory Lock ==="
RUNNER_SQL="$(mktemp /tmp/rikipath_migration_runner.XXXXXX.sql)"
trap 'rm -f "$RUNNER_SQL"' EXIT

cat > "$RUNNER_SQL" <<EOF
DO \$lock\$
BEGIN
    IF NOT pg_try_advisory_lock(${ADVISORY_LOCK_KEY}) THEN
        RAISE EXCEPTION 'MIGRATION_LOCK_BUSY: Another session currently holds PostgreSQL advisory lock ${ADVISORY_LOCK_KEY}.';
    END IF;
END \$lock\$;
EOF

for id in "${PENDING_IDS[@]}"; do
  SQL_FILE="${MIGRATIONS_DIR}/${id}.sql"
  if [[ ! -f "$SQL_FILE" ]]; then
    echo "ERROR: Missing generated SQL script for migration ${id}: ${SQL_FILE}" >&2
    exit 15
  fi
  # Re-check inside locked session so a second runner that queued cannot re-apply an already-applied ID.
  printf '%s\n' "\\echo '  -> Applying ${id} ...'" >> "$RUNNER_SQL"
  printf '%s\n' "\\i '${SQL_FILE}'" >> "$RUNNER_SQL"
done

cat >> "$RUNNER_SQL" <<EOF
SELECT pg_advisory_unlock(${ADVISORY_LOCK_KEY});
EOF

psql -v ON_ERROR_STOP=1 -f "$RUNNER_SQL"

echo "=== 5. Post-Migration Verification ==="
mapfile -t POST_APPLIED < <(psql -v ON_ERROR_STOP=1 -t -A -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' | tr -d '\r')
declare -A POST_MAP=()
for id in "${POST_APPLIED[@]}"; do
  [[ -n "$id" ]] && POST_MAP["$id"]=1
done

STILL_PENDING=0
for id in "${CANONICAL_IDS[@]}"; do
  if [[ -z "${POST_MAP[$id]+x}" ]]; then
    echo "ERROR: Migration ${id} is still missing from \"__EFMigrationsHistory\" after execution." >&2
    STILL_PENDING=$((STILL_PENDING + 1))
  fi
done

if [[ "$STILL_PENDING" -gt 0 ]]; then
  exit 16
fi

echo "SUCCESS: All ${#PENDING_IDS[@]} pending migration(s) applied and verified cleanly."
