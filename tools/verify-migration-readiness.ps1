<#
.SYNOPSIS
    Strict READ-ONLY EF Core & PostgreSQL migration readiness preflight for RikiPath.Backend.

.DESCRIPTION
    Reuses existing EF Core CLI tooling and read-only SQL checks (`BEGIN READ ONLY; ... ROLLBACK;`)
    to detect:
      1. Duplicate migration IDs or missing .Designer.cs / .cs pairs on disk.
      2. ModelSnapshot vs AppDbContext drift (`migrations has-pending-model-changes`).
      3. Target database host & remote-shared-host guard status (never prints credentials).
      4. "__EFMigrationsHistory" divergence (pending canonical migrations, missing-source IDs,
         audited cross-branch IDs vs unexpected unknown IDs).
      5. Live database readiness checks (btree_gist, MentorReviews, Group Learning base tables,
         active cohort duplicate conflicts, invalid credit/compensation FKs).

    NEVER executes DDL, DML, CREATE EXTENSION, or `dotnet ef database update`.
#>
[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$PsqlPath,
    [switch]$SkipDatabaseCheck,
    [switch]$NoBuild,
    [ValidateSet('Debug', 'Release')]
    [string]$BuildConfiguration = 'Release',
    [switch]$RequireSharedRemote,
    [string]$ExpectedHost,
    [string]$ExpectedDatabase,
    [string]$ExpectedUsername
)

$ErrorActionPreference = 'Stop'
$BackendRoot = Split-Path -Parent $PSScriptRoot
$MigrationsDir = Join-Path $BackendRoot 'RikiPath.Infrastructure\Migrations'
$SqlDir = Join-Path $BackendRoot 'RikiPath.Infrastructure\Sql'

$AuditedExternalMigrations = @(
    '20261116061024_RetireLegacyStudyCourseModel',
    '20261116143915_AddMentorAuthoredHomework',
    '20261117121238_AddLearnerFeedback',
    '20261118054924_AddContentAudioTranscriptions',
    '20261118091131_AddMockQuestionAudioTranscript',
    '20261118100842_AddImportedContentImages'
)

Write-Host '=== 1. Local Migration Source Integrity ==='
$migrationFiles = Get-ChildItem -Path $MigrationsDir -Filter '*.cs' |
    Where-Object { $_.Name -notlike '*.Designer.cs' -and $_.Name -ne 'AppDbContextModelSnapshot.cs' } |
    Sort-Object Name
$orphanDesigners = Get-ChildItem -Path $MigrationsDir -Filter '*.Designer.cs' |
    Where-Object { -not (Test-Path ($_.FullName.Substring(0, $_.FullName.Length - '.Designer.cs'.Length) + '.cs')) }

$sourceIds = [System.Collections.Generic.List[string]]::new()
$missingSourcePairs = [System.Collections.Generic.List[string]]::new()

foreach ($od in $orphanDesigners) {
    $missingSourcePairs.Add("$($od.Name) (orphan designer)")
}

foreach ($m in $migrationFiles) {
    $designerPath = $m.FullName.Substring(0, $m.FullName.Length - '.cs'.Length) + '.Designer.cs'
    $text = Get-Content -Raw -Path $m.FullName
    if (Test-Path $designerPath) {
        $text += "`n" + (Get-Content -Raw -Path $designerPath)
    }
    if ($text -match '\[DbContext\(typeof\(AppDbContext\)\)\]' -and $text -match '\[Migration\("([^"]+)"\)\]') {
        $sourceIds.Add($Matches[1])
    } else {
        $missingSourcePairs.Add("$($m.Name) (missing [Migration]/[DbContext] attribute)")
    }
}

$duplicates = $sourceIds | Group-Object | Where-Object { $_.Count -gt 1 } | Select-Object -ExpandProperty Name
Write-Host ("  Canonical migration files : {0}" -f $sourceIds.Count)
Write-Host ("  Missing/invalid pairs     : {0}" -f $missingSourcePairs.Count)
Write-Host ("  Duplicate migration IDs   : {0}" -f @($duplicates).Count)

if ($missingSourcePairs.Count -gt 0 -or @($duplicates).Count -gt 0) {
    throw "Migration source integrity failure."
}

Write-Host '=== 2. ModelSnapshot vs AppDbContext Drift Check ==='
Push-Location $BackendRoot
try {
    $efArgs = @('ef', 'migrations', 'has-pending-model-changes', '--project', 'RikiPath.Infrastructure', '--startup-project', 'RikiPath.WebApi')
    if ($NoBuild) { $efArgs += '--no-build' }
    $efArgs += @('--configuration', $BuildConfiguration)
    $efArgs += @('--', '--no-connect')
    & dotnet @efArgs
    if ($LASTEXITCODE -ne 0) {
        throw "ModelSnapshot drift detected or dotnet ef failed (exit $LASTEXITCODE)."
    }
}
finally {
    Pop-Location
}

if ($SkipDatabaseCheck) {
    Write-Host 'Database check skipped (-SkipDatabaseCheck).'
    exit 0
}

Write-Host '=== 3. Target Database Identity (Masked, Read-Only) ==='
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    foreach ($k in @(
        'AppSettings__ConnectionStrings__DefaultConnection',
        'AppSettings:ConnectionStrings:DefaultConnection',
        'ConnectionStrings__DefaultConnection',
        'ConnectionStrings:DefaultConnection'
    )) {
        $val = [Environment]::GetEnvironmentVariable($k)
        if (-not [string]::IsNullOrWhiteSpace($val)) {
            $ConnectionString = $val
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $envName = $env:ASPNETCORE_ENVIRONMENT
    if ([string]::IsNullOrWhiteSpace($envName)) { $envName = 'Development' }
    foreach ($cfgName in @("appsettings.$envName.json", 'appsettings.json')) {
        $cfgPath = Join-Path $BackendRoot "RikiPath.WebApi\$cfgName"
        if (Test-Path $cfgPath) {
            $json = Get-Content -Raw -Path $cfgPath | ConvertFrom-Json
            $cand = $json.AppSettings.ConnectionStrings.DefaultConnection
            if ([string]::IsNullOrWhiteSpace($cand)) { $cand = $json.ConnectionStrings.DefaultConnection }
            if (-not [string]::IsNullOrWhiteSpace($cand) -and $cand -notmatch 'rikipath_design_only|Host=invalid') {
                $ConnectionString = $cand
                break
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw 'No target database connection string resolved. Pass -ConnectionString or set AppSettings__ConnectionStrings__DefaultConnection (or use -SkipDatabaseCheck).'
}

$pairs = @{}
foreach ($part in ($ConnectionString -split ';')) {
    if ($part -match '^\s*([^=]+?)\s*=\s*(.+?)\s*$') {
        $pairs[$Matches[1].ToLowerInvariant()] = $Matches[2]
    }
}

$dbHost = $pairs['host']
$dbPort = if ($pairs['port']) { $pairs['port'] } else { '5432' }
$dbName = $pairs['database']
$dbUser = if ($pairs['username']) { $pairs['username'] } elseif ($pairs['user id']) { $pairs['user id'] } else { $pairs['user'] }
$dbPass = $pairs['password']
$sslMode = if ($pairs['ssl mode']) { $pairs['ssl mode'] } elseif ($pairs['sslmode']) { $pairs['sslmode'] } else { 'prefer' }

$isSharedRemote = $dbHost -match '(?i)(supabase\.co|supabase\.com|supabase\.)'
Write-Host ("  Resolved Host     : {0}:{1}" -f $dbHost, $dbPort)
Write-Host ("  Resolved Database : {0}" -f $dbName)
Write-Host ("  Shared Remote Host: {0}" -f $isSharedRemote)
if ($RequireSharedRemote) {
    if ([string]::IsNullOrWhiteSpace($ExpectedHost) -or
        [string]::IsNullOrWhiteSpace($ExpectedDatabase) -or
        [string]::IsNullOrWhiteSpace($ExpectedUsername)) {
        throw 'Release gate requires separately configured expected host, database, and username.'
    }
    if (-not $isSharedRemote -or
        $dbHost -ine $ExpectedHost -or
        $dbName -ine $ExpectedDatabase -or
        $dbUser -ine $ExpectedUsername) {
        throw 'Release gate database identity mismatch; refusing to check or deploy an unintended target.'
    }
}
if ($isSharedRemote) {
    Write-Warning 'Targeting shared remote PostgreSQL host. Mutations remain blocked unless RIKIPATH_ALLOW_REMOTE_DB_MIGRATION=true.'
}

if ([string]::IsNullOrWhiteSpace($PsqlPath)) {
    $foundPsql = Get-ChildItem -Path 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if ($foundPsql) { $PsqlPath = $foundPsql } else { $PsqlPath = 'psql' }
}

$oldPwd = $env:PGPASSWORD
$oldSsl = $env:PGSSLMODE
$env:PGPASSWORD = $dbPass
$env:PGSSLMODE = $sslMode.ToLowerInvariant()

function Invoke-ReadOnlySql([string]$SqlText) {
    $wrapped = "BEGIN READ ONLY;`n$($SqlText.Trim().TrimEnd(';'));`nROLLBACK;"
    $out = $wrapped | & $PsqlPath -h $dbHost -p $dbPort -U $dbUser -d $dbName -t -A -F '|' -v 'ON_ERROR_STOP=1' 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Read-only preflight query failed: $out"
    }
    return ($out | ForEach-Object { "$_" } | Where-Object { $_ -and $_ -ne 'BEGIN' -and $_ -ne 'ROLLBACK' })
}

try {
    Write-Host '=== 4. Migration History & Schema Readiness (Read-Only) ==='
    $applied = Invoke-ReadOnlySql 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId"'
    $pending = @($sourceIds | Where-Object { $_ -notin $applied })
    $missingSource = @($applied | Where-Object { $_ -notin $sourceIds })
    $auditedExtra = @($missingSource | Where-Object { $_ -in $AuditedExternalMigrations })
    $unexpectedExtra = @($missingSource | Where-Object { $_ -notin $AuditedExternalMigrations })

    Write-Host ("  Applied in DB             : {0}" -f @($applied).Count)
    Write-Host ("  Pending canonical         : {0}" -f $pending.Count)
    Write-Host ("  Audited cross-branch      : {0}" -f $auditedExtra.Count)
    Write-Host ("  UNEXPECTED history rows   : {0}" -f $unexpectedExtra.Count)

    if ($unexpectedExtra.Count -gt 0) {
        Write-Warning ("Unexpected DB migration IDs: {0}" -f ($unexpectedExtra -join ', '))
    }

    $feedbackParity = $true
    if ('20261117121238_AddLearnerFeedback' -in $applied) {
        $feedbackCheck = Invoke-ReadOnlySql @'
SELECT
    (to_regclass('public."LearnerFeedback"') IS NOT NULL)::text,
    ((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='LearnerFeedback') = 9)::text,
    ((SELECT COUNT(*) FROM pg_constraint WHERE conrelid=to_regclass('public."LearnerFeedback"') AND contype IN ('p','f','c') AND convalidated) = 3)::text,
    ((SELECT COUNT(*) FROM pg_indexes WHERE schemaname='public' AND tablename='LearnerFeedback') = 3)::text,
    (COALESCE((SELECT relrowsecurity FROM pg_class WHERE oid=to_regclass('public."LearnerFeedback"')), false))::text,
    (COALESCE((SELECT relrowsecurity FROM pg_class WHERE oid=to_regclass('public."MentorReviews"')), false))::text
'@
        $feedbackParts = ($feedbackCheck | Select-Object -First 1) -split '\|'
        $feedbackParity = $feedbackParts.Count -eq 6 -and @($feedbackParts | Where-Object { $_ -ne 'true' }).Count -eq 0
        Write-Host ("  LearnerFeedback parity     : {0}" -f $feedbackParity)
        if (-not $feedbackParity) { Write-Warning 'Audited AddLearnerFeedback history ID does not match expected schema/RLS.' }
    }

    $checks = Invoke-ReadOnlySql @'
SELECT
    (SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'btree_gist'))::text,
    (SELECT to_regclass('public."MentorProfiles"') IS NOT NULL)::text,
    (SELECT to_regclass('public."MentorReviews"') IS NOT NULL)::text,
    (SELECT to_regclass('public."StudyCohorts"') IS NOT NULL AND to_regclass('public."StudyCourses"') IS NOT NULL)::text,
    (SELECT COUNT(*) FROM "StudyCohorts" WHERE "Capacity" <= 0)::text,
    (SELECT COUNT(*) FROM (SELECT "CohortId","LearnerId" FROM "StudyCourses" WHERE "CohortId" IS NOT NULL AND "Status"='Active' GROUP BY 1,2 HAVING COUNT(*)>1) s)::text,
    (SELECT COUNT(*) FROM pg_constraint con JOIN pg_class cls ON cls.oid = con.conrelid JOIN pg_namespace ns ON ns.oid = cls.relnamespace WHERE ns.nspname = 'public' AND con.contype = 'f' AND NOT con.convalidated)::text,
    ((SELECT COUNT(*) FROM "MentorBookings" b WHERE b."CreditGrantId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "MentorCreditGrants" g WHERE g."Id" = b."CreditGrantId")) + (SELECT COUNT(*) FROM "MentorCreditEntries" e WHERE e."CreditGrantId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "MentorCreditGrants" g WHERE g."Id" = e."CreditGrantId")))::text
'@
    $partsOut = ($checks | Select-Object -First 1) -split '\|'
    Write-Host ("  btree_gist installed      : {0}" -f $partsOut[0])
    Write-Host ("  MentorProfiles present    : {0}" -f $partsOut[1])
    Write-Host ("  MentorReviews present     : {0}" -f $partsOut[2])
    Write-Host ("  Group base tables intact  : {0}" -f $partsOut[3])
    Write-Host ("  Invalid Cohort capacity   : {0}" -f $partsOut[4])
    Write-Host ("  Dup active Cohort enroll  : {0}" -f $partsOut[5])
    Write-Host ("  Unvalidated (NOT VALID) FK: {0}" -f $partsOut[6])
    Write-Host ("  Orphan CreditGrantId refs : {0}" -f $partsOut[7])

    $reconciledTables = @(
        'MentorBookingDayGuards', 'MentorBookingIntents', 'MentorBookingReservations',
        'MentorBookingTerms', 'MentorSchedules', 'MentorScheduleRules',
        'JlptLevelPayCoefficients', 'MentorHourlyRates', 'MentorTeachingQualifications',
        'MentorAttendanceApprovals', 'MentorBankAccounts', 'MentorPayrollPeriods',
        'OrganizationPayoutAccounts', 'MentorEarningEntries', 'MentorPayrollAdjustments',
        'MentorPayoutObligations', 'MentorPayrollLines', 'MentorPayoutAttempts'
    )
    $tableNames = ($reconciledTables | ForEach-Object { "'" + $_.Replace("'", "''") + "'" }) -join ','
    $presentTables = Invoke-ReadOnlySql "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE' AND table_name IN ($tableNames)"
    $presentCount = [int]($presentTables | Select-Object -First 1)
    Write-Host ("  Reconciled tables present : {0}/{1}" -f $presentCount, $reconciledTables.Count)

    if ($unexpectedExtra.Count -gt 0 -or $pending.Count -gt 0 -or -not $feedbackParity -or
        $partsOut[0] -ne 'true' -or $partsOut[1] -ne 'true' -or $partsOut[2] -ne 'true' -or
        $partsOut[3] -ne 'true' -or [int]$partsOut[4] -gt 0 -or
        [int]$partsOut[5] -gt 0 -or [int]$partsOut[6] -gt 0 -or
        [int]$partsOut[7] -gt 0 -or $presentCount -ne $reconciledTables.Count) {
        Write-Warning 'Release preflight BLOCKED: pending migrations or extension, table, constraint, or history gaps remain.'
        exit 2
    }
}
finally {
    $env:PGPASSWORD = $oldPwd
    $env:PGSSLMODE = $oldSsl
}

Write-Host 'Preflight PASSED (read-only checks complete).'
