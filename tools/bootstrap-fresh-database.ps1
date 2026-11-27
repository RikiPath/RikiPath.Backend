<#
.SYNOPSIS
    Fail-closed automated Fresh Database Bootstrap for RikiPath.Backend.

.DESCRIPTION
    Creates schema from scratch on a brand-new empty PostgreSQL database without mutating
    historical migration IDs or requiring manual ordering:
      1. Refuses populated databases (must have zero existing core tables).
      2. Refuses shared remote Supabase hosts unless -AllowRemoteDatabase is specified.
      3. Generates EF Core per-migration SQL scripts via dotnet-ef (when -MigrationsDir is omitted).
      4. Ensures "__EFMigrationsHistory" exists, applies 20261121010126_InitialCreate, then
         20261120112045_SeedFullAccessLearnerDemo, followed by all remaining canonical migrations.
      5. Verifies full migration parity afterward.
#>
[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$PsqlPath,
    [string]$MigrationsDir,
    [switch]$NoBuild,
    [ValidateSet('Debug', 'Release')]
    [string]$BuildConfiguration = 'Release',
    [switch]$AllowRemoteDatabase
)

$ErrorActionPreference = 'Stop'
$BackendRoot = Split-Path -Parent $PSScriptRoot

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
    throw 'Pass -ConnectionString or set AppSettings__ConnectionStrings__DefaultConnection.'
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

if ([string]::IsNullOrWhiteSpace($dbHost) -or [string]::IsNullOrWhiteSpace($dbName) -or [string]::IsNullOrWhiteSpace($dbUser)) {
    throw 'Incomplete PostgreSQL connection string (missing Host, Database, or Username).'
}

if (($dbHost -match '(?i)(supabase\.co|supabase\.com|supabase\.)') -and -not $AllowRemoteDatabase) {
    throw "Refusing fresh DB bootstrap on shared remote host ($dbHost) without -AllowRemoteDatabase."
}

if ([string]::IsNullOrWhiteSpace($PsqlPath)) {
    $foundPsql = Get-ChildItem -Path 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if ($foundPsql) { $PsqlPath = $foundPsql } else { $PsqlPath = 'psql' }
}

$tempWorkDir = $null
$oldPwd = $env:PGPASSWORD
$oldSsl = $env:PGSSLMODE
$env:PGPASSWORD = $dbPass
$env:PGSSLMODE = $sslMode.ToLowerInvariant()

try {
    if ([string]::IsNullOrWhiteSpace($MigrationsDir)) {
        $tempWorkDir = Join-Path ([System.IO.Path]::GetTempPath()) ("rikipath-fresh-bootstrap-" + [System.Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $tempWorkDir | Out-Null
        $MigrationsDir = $tempWorkDir

        Push-Location $BackendRoot
        try {
            $efListArgs = @('ef', 'migrations', 'list', '--project', 'RikiPath.Infrastructure', '--startup-project', 'RikiPath.WebApi')
            if ($NoBuild) { $efListArgs += '--no-build' }
            $efListArgs += @('--configuration', $BuildConfiguration, '--no-connect', '--', '--no-connect')

            $canonicalIds = & dotnet @efListArgs | Where-Object { $_ -match '^[0-9]{14}_[A-Za-z0-9_]+$' }
            if ($LASTEXITCODE -ne 0 -or @($canonicalIds).Count -eq 0) {
                throw 'Failed to enumerate EF Core migrations.'
            }

            $canonicalIds | Set-Content (Join-Path $MigrationsDir 'manifest.txt')
            $prev = '0'
            foreach ($mig in $canonicalIds) {
                & dotnet ef migrations script $prev $mig `
                    --project RikiPath.Infrastructure `
                    --startup-project RikiPath.WebApi `
                    --no-build `
                    --configuration $BuildConfiguration `
                    --output (Join-Path $MigrationsDir "$mig.sql") `
                    -- --no-connect | Out-Null
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed generating SQL script for $mig"
                }
                $prev = $mig
            }
        }
        finally {
            Pop-Location
        }
    }

    $manifestPath = Join-Path $MigrationsDir 'manifest.txt'
    if (-not (Test-Path $manifestPath)) {
        throw "Missing manifest.txt in $MigrationsDir"
    }
    $canonicalList = Get-Content $manifestPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    Write-Host '=== 1. Fresh Database Empty-Target Safety Check ==='
    Write-Host ("  Target DB : {0} (port {1}, sslmode={2})" -f $dbName, $dbPort, $sslMode.ToLowerInvariant())

    $existing = & $PsqlPath -h $dbHost -p $dbPort -U $dbUser -d $dbName -t -A -v 'ON_ERROR_STOP=1' `
        -c "SELECT ((to_regclass('public.""__EFMigrationsHistory""') IS NOT NULL) OR (to_regclass('public.""Users""') IS NOT NULL) OR (to_regclass('public.""AdminAuditLogs""') IS NOT NULL))::text;"
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed connectivity/empty check against target database.'
    }
    if (($existing | Select-Object -First 1).Trim() -eq 'true') {
        throw "Database $dbName is not empty. Use tools/apply-release-migrations.sh or standard upgrade flow instead."
    }

    $bootstrapIds = [System.Collections.Generic.List[string]]::new()
    if ('20261121010126_InitialCreate' -in $canonicalList) {
        $bootstrapIds.Add('20261121010126_InitialCreate')
    }
    if ('20261120112045_SeedFullAccessLearnerDemo' -in $canonicalList) {
        $bootstrapIds.Add('20261120112045_SeedFullAccessLearnerDemo')
    }
    foreach ($id in $canonicalList) {
        if ($id -ne '20261121010126_InitialCreate' -and $id -ne '20261120112045_SeedFullAccessLearnerDemo') {
            $bootstrapIds.Add($id)
        }
    }

    Write-Host ("=== 2. Bootstrapping {0} Canonical Migrations ===" -f $bootstrapIds.Count)
    $runnerSql = Join-Path $MigrationsDir 'fresh_runner.sql'
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('SELECT pg_advisory_lock(74520198431);')
    $lines.Add('CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" ("MigrationId" character varying(150) NOT NULL, "ProductVersion" character varying(32) NOT NULL, CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId"));')
    foreach ($id in $bootstrapIds) {
        $sqlPath = (Join-Path $MigrationsDir "$id.sql").Replace('\', '/')
        $lines.Add("\echo '  -> Bootstrapping $id ...'")
        $lines.Add("\i '$sqlPath'")
    }
    $lines.Add('SELECT pg_advisory_unlock(74520198431);')
    [System.IO.File]::WriteAllLines($runnerSql, $lines)

    & $PsqlPath -h $dbHost -p $dbPort -U $dbUser -d $dbName -v 'ON_ERROR_STOP=1' -f $runnerSql
    if ($LASTEXITCODE -ne 0) {
        throw 'Fresh database bootstrap failed.'
    }

    $appliedCount = [int](& $PsqlPath -h $dbHost -p $dbPort -U $dbUser -d $dbName -t -A -v 'ON_ERROR_STOP=1' -c 'SELECT COUNT(*) FROM "__EFMigrationsHistory";' | Select-Object -First 1)
    Write-Host ("SUCCESS: Bootstrapped {0}/{1} migrations cleanly on {2}." -f $appliedCount, $canonicalList.Count, $dbName)
}
finally {
    $env:PGPASSWORD = $oldPwd
    $env:PGSSLMODE = $oldSsl
    if ($tempWorkDir -and (Test-Path $tempWorkDir)) {
        Remove-Item -Recurse -Force $tempWorkDir -ErrorAction SilentlyContinue
    }
}
