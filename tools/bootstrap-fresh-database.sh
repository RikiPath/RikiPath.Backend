#!/usr/bin/env bash
# Fail-closed, automated Fresh Database Bootstrap for RikiPath.Backend.
#
# Why this script exists:
#   Historical migration 20261120112045_SeedFullAccessLearnerDemo carries a timestamp
#   earlier than the Nov 21 squash migration 20261121010126_InitialCreate.
#   Existing databases already record their migration history in "__EFMigrationsHistory"
#   and must be upgraded via standard reviewed EF Core migration procedures. For a brand-new
#   empty local or isolated Docker PostgreSQL database, this script automatically orders:
#     1) Ensure "__EFMigrationsHistory" exists
#     2) Apply 20261121010126_InitialCreate
#     3) Apply 20261120112045_SeedFullAccessLearnerDemo
#     4) Apply all remaining canonical migrations in strict MigrationId order
#   without modifying historical Migration IDs or tampering with migration history.
#
# Safety guarantees:
#   - Refuses populated databases (aborts if "__EFMigrationsHistory", "Users", or "AdminAuditLogs" exists).
#   - Refuses shared remote Supabase hosts unless RIKIPATH_ALLOW_REMOTE_DB_MIGRATION=true.
#   - Never prints connection strings, passwords, or secrets.

set -euo pipefail

APPSETTINGS_PATH=""
MIGRATIONS_DIR=""
ALLOW_REMOTE="${RIKIPATH_ALLOW_REMOTE_DB_MIGRATION:-false}"
ADVISORY_LOCK_KEY="74520198431"
BUILD_CONFIG="Release"
NO_BUILD="false"

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
    --configuration)
      BUILD_CONFIG="$2"
      shift 2
      ;;
    --no-build)
      NO_BUILD="true"
      shift
      ;;
    --allow-remote-database)
      ALLOW_REMOTE="true"
      shift
      ;;
    *)
      echo "ERROR: Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

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

if [[ -n "$APPSETTINGS_PATH" ]]; then
  if [[ ! -f "$APPSETTINGS_PATH" ]]; then
    echo "ERROR: --appsettings file not found: $APPSETTINGS_PATH" >&2
    exit 2
  fi
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
    print('echo "ERROR: Incomplete PostgreSQL connection string." >&2; exit 4')
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
fi

if [[ -z "${PGHOST:-}" || -z "${PGDATABASE:-}" || -z "${PGUSER:-}" ]]; then
  echo "ERROR: Provide --appsettings or PGHOST/PGDATABASE/PGUSER environment variables." >&2
  exit 2
fi

if [[ "$PGHOST" =~ (supabase\.co|supabase\.com|supabase\.) && "$ALLOW_REMOTE" != "true" ]]; then
  echo "ERROR: Refusing fresh DB bootstrap on shared remote host (${PGHOST}) without --allow-remote-database." >&2
  exit 5
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
WORK_DIR=""
cleanup() {
  if [[ -n "$WORK_DIR" && -d "$WORK_DIR" ]]; then
    rm -rf "$WORK_DIR"
  fi
}
trap cleanup EXIT

if [[ -z "$MIGRATIONS_DIR" ]]; then
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "ERROR: dotnet CLI is required when --migrations-dir is not provided." >&2
    exit 3
  fi
  WORK_DIR="$(mktemp -d /tmp/rikipath-fresh-bootstrap.XXXXXX)"
  MIGRATIONS_DIR="$WORK_DIR"
  pushd "$BACKEND_ROOT" >/dev/null
  EF_BUILD_FLAG=()
  if [[ "$NO_BUILD" == "true" ]]; then
    EF_BUILD_FLAG=(--no-build)
  fi
  mapfile -t CANONICAL_IDS < <(
    dotnet ef migrations list \
      --project RikiPath.Infrastructure \
      --startup-project RikiPath.WebApi \
      "${EF_BUILD_FLAG[@]}" \
      --configuration "$BUILD_CONFIG" \
      --no-connect \
      -- --no-connect | grep -E '^[0-9]{14}_[A-Za-z0-9_]+$'
  )
  if [[ ${#CANONICAL_IDS[@]} -eq 0 ]]; then
    echo "ERROR: No EF migrations discovered." >&2
    exit 1
  fi
  printf '%s\n' "${CANONICAL_IDS[@]}" > "${MIGRATIONS_DIR}/manifest.txt"
  PREV="0"
  for MIG in "${CANONICAL_IDS[@]}"; do
    dotnet ef migrations script "$PREV" "$MIG" \
      --project RikiPath.Infrastructure \
      --startup-project RikiPath.WebApi \
      --no-build \
      --configuration "$BUILD_CONFIG" \
      --output "${MIGRATIONS_DIR}/${MIG}.sql" \
      -- --no-connect >/dev/null
    PREV="$MIG"
  done
  popd >/dev/null
fi

if [[ ! -f "$MIGRATIONS_DIR/manifest.txt" ]]; then
  echo "ERROR: manifest.txt missing in $MIGRATIONS_DIR" >&2
  exit 2
fi

mapfile -t CANONICAL_IDS < <(grep -v '^[[:space:]]*$' "$MIGRATIONS_DIR/manifest.txt" | tr -d '\r')

echo "=== 1. Fresh Database Empty-Target Safety Check ==="
echo "  Target DB : ${PGDATABASE} (port ${PGPORT:-5432}, sslmode=${PGSSLMODE:-prefer})"
psql -v ON_ERROR_STOP=1 -t -A -c 'SELECT 1;' >/dev/null

EXISTING_CORE="$(psql -v ON_ERROR_STOP=1 -t -A -c "SELECT ((to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL) OR (to_regclass('public.\"Users\"') IS NOT NULL) OR (to_regclass('public.\"AdminAuditLogs\"') IS NOT NULL))::text;")"
if [[ "$EXISTING_CORE" == "true" ]]; then
  echo "ERROR [TARGET_NOT_EMPTY]: Database ${PGDATABASE} is not empty. Refusing fresh bootstrap on an existing database." >&2
  exit 10
fi

BOOTSTRAP_IDS=()
HAS_INITIAL="false"
HAS_PRE_SQUASH_SEED="false"
for id in "${CANONICAL_IDS[@]}"; do
  if [[ "$id" == "20261121010126_InitialCreate" ]]; then HAS_INITIAL="true"; fi
  if [[ "$id" == "20261120112045_SeedFullAccessLearnerDemo" ]]; then HAS_PRE_SQUASH_SEED="true"; fi
done

if [[ "$HAS_INITIAL" == "true" ]]; then
  BOOTSTRAP_IDS+=("20261121010126_InitialCreate")
fi
if [[ "$HAS_PRE_SQUASH_SEED" == "true" ]]; then
  BOOTSTRAP_IDS+=("20261120112045_SeedFullAccessLearnerDemo")
fi
for id in "${CANONICAL_IDS[@]}"; do
  if [[ "$id" == "20261121010126_InitialCreate" || "$id" == "20261120112045_SeedFullAccessLearnerDemo" ]]; then
    continue
  fi
  BOOTSTRAP_IDS+=("$id")
done

echo "=== 2. Applying ${#BOOTSTRAP_IDS[@]} Migrations to Fresh Database ==="
RUNNER_SQL="$(mktemp /tmp/rikipath_fresh_runner.XXXXXX.sql)"
trap 'rm -f "$RUNNER_SQL"; cleanup' EXIT

cat > "$RUNNER_SQL" <<EOF
DO \$lock\$
BEGIN
    IF NOT pg_try_advisory_lock(${ADVISORY_LOCK_KEY}) THEN
        RAISE EXCEPTION 'MIGRATION_LOCK_BUSY: Another session holds advisory lock ${ADVISORY_LOCK_KEY}.';
    END IF;
END \$lock\$;
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);
EOF

for id in "${BOOTSTRAP_IDS[@]}"; do
  SQL_FILE="${MIGRATIONS_DIR}/${id}.sql"
  if [[ ! -f "$SQL_FILE" ]]; then
    echo "ERROR: Missing SQL file: ${SQL_FILE}" >&2
    exit 15
  fi
  printf '%s\n' "\\echo '  -> Bootstrapping ${id} ...'" >> "$RUNNER_SQL"
  printf '%s\n' "\\i '${SQL_FILE}'" >> "$RUNNER_SQL"
done

cat >> "$RUNNER_SQL" <<EOF
SELECT pg_advisory_unlock(${ADVISORY_LOCK_KEY});
EOF

psql -v ON_ERROR_STOP=1 -f "$RUNNER_SQL"

APPLIED_COUNT="$(psql -v ON_ERROR_STOP=1 -t -A -c 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')"
echo "=== 3. Fresh Bootstrap Verification ==="
echo "  Applied migrations: ${APPLIED_COUNT}/${#CANONICAL_IDS[@]}"
if [[ "$APPLIED_COUNT" -ne "${#CANONICAL_IDS[@]}" ]]; then
  echo "ERROR: Migration count mismatch after fresh bootstrap." >&2
  exit 16
fi

echo "SUCCESS: Fresh database ${PGDATABASE} bootstrapped cleanly (${APPLIED_COUNT} migrations)."
