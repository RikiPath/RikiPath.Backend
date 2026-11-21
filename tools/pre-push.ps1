<#
.SYNOPSIS
    Selective mandatory pre-push gate for RikiPath.Backend.

.DESCRIPTION
    Reads Git's pre-push ref updates from stdin, blocks dirty/direct-main pushes, rejects
    tracked test or obvious secret files, and runs only the expensive gates relevant to the
    commits being pushed. It never changes a database and never runs dotnet test.
#>
[CmdletBinding()]
param(
    [string]$RemoteName = 'origin',
    [string]$RemoteUrl = ''
)

$ErrorActionPreference = 'Stop'
$BackendRoot = Split-Path -Parent $PSScriptRoot
$ZeroSha = '0' * 40

function Invoke-GitLines {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = @(& git @Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }

    return @($output | ForEach-Object { "$_" } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Invoke-RequiredCommand {
    param(
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][scriptblock]$Command
    )

    Write-Host "=== $Label ==="
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE. Push blocked."
    }
}

Push-Location $BackendRoot
try {
    Write-Host '=== RikiPath.Backend mandatory pre-push gate ==='
    $branch = (Invoke-GitLines @('branch', '--show-current') | Select-Object -First 1)
    Write-Host "Branch : $branch"
    Write-Host "Remote : $RemoteName"

    if ($branch -eq 'main') {
        throw 'Direct pushes to main are prohibited. Push a feature branch and use a protected pull request.'
    }

    $workingTree = Invoke-GitLines @('status', '--porcelain=v1', '--untracked-files=normal')
    if ($workingTree.Count -gt 0) {
        Write-Host ($workingTree -join [Environment]::NewLine)
        throw 'Working tree is not clean. The hook refuses to verify a build that differs from the commits being pushed.'
    }

    $updates = [System.Collections.Generic.List[object]]::new()
    while (($line = [Console]::In.ReadLine()) -ne $null) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split '\s+'
        if ($parts.Count -ne 4) { throw "Unexpected pre-push input: $line" }
        $updates.Add([pscustomobject]@{
            LocalRef = $parts[0]
            LocalSha = $parts[1]
            RemoteRef = $parts[2]
            RemoteSha = $parts[3]
        })
    }

    $changed = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($update in $updates) {
        if ($update.LocalSha -eq $ZeroSha) { continue }

        if ($update.RemoteSha -ne $ZeroSha) {
            $baseSha = $update.RemoteSha
        } else {
            $baseSha = @(& git merge-base $update.LocalSha origin/main 2>$null) | Select-Object -First 1
            if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($baseSha)) {
                $baseSha = Invoke-GitLines @('rev-list', '--max-parents=0', $update.LocalSha) | Select-Object -First 1
            }
        }

        foreach ($path in (Invoke-GitLines @('diff', '--name-only', '--diff-filter=ACMRT', $baseSha, $update.LocalSha))) {
            [void]$changed.Add($path.Replace('\', '/'))
        }
    }

    if ($updates.Count -eq 0) {
        $baseSha = @(& git merge-base HEAD origin/main 2>$null) | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($baseSha)) {
            foreach ($path in (Invoke-GitLines @('diff', '--name-only', '--diff-filter=ACMRT', $baseSha, 'HEAD'))) {
                [void]$changed.Add($path.Replace('\', '/'))
            }
        }
    }

    $changedFiles = @($changed | Sort-Object)
    if ($changedFiles.Count -eq 0) {
        Write-Host 'No pushed file changes detected. Gate passed.'
        exit 0
    }

    Write-Host "Changed files: $($changedFiles.Count)"

    $testFiles = @($changedFiles | Where-Object {
        $_ -match '(^|/)(tests?|[^/]+\.tests?)(/|$)' -or
        $_ -match '(^|/)[^/]+Tests\.cs$' -or
        $_ -match '\.Tests?\.csproj$'
    })
    if ($testFiles.Count -gt 0) {
        throw "Backend test files/projects are permanently prohibited: $($testFiles -join ', ')"
    }

    $secretFiles = @($changedFiles | Where-Object {
        $name = [IO.Path]::GetFileName($_)
        ($name -match '^appsettings(\..+)?\.json$' -and $name -notmatch '(?i)(example|sample|template)') -or
        $name -match '^(?i:\.env|secrets?.*\.json|.*\.pgpass|.*\.(dump|bak))$'
    })
    if ($secretFiles.Count -gt 0) {
        throw "Potential secret-bearing files may not be pushed: $($secretFiles -join ', ')"
    }

    $buildRelevant = @($changedFiles | Where-Object {
        $_ -match '\.(cs|csproj|slnx|props|targets)$' -or
        $_ -match '(^|/)(global\.json|Directory\.Packages\.props)$'
    })
    $modelRelevant = @($changedFiles | Where-Object {
        $_ -match '^RikiPath\.Domain/Entities/.+\.cs$' -or
        $_ -match '^RikiPath\.Infrastructure/(AppDbContext\.cs|Configuration/.+\.cs|Migrations/.+\.cs)$' -or
        $_ -match '^RikiPath\.Infrastructure/RikiPath\.Infrastructure\.csproj$'
    })
    $apiRelevant = @($changedFiles | Where-Object {
        $_ -match '^RikiPath\.WebApi/(Controllers|Hubs|Middleware|Swagger)/' -or
        $_ -eq 'RikiPath.WebApi/Program.cs' -or
        $_ -match '^RikiPath\.Application/(Requests|Responses|Services|IServices|IRepositories)/' -or
        $_ -match '^RikiPath\.Infrastructure/Repositories/'
    })

    if ($buildRelevant.Count -eq 0) {
        Write-Host 'Documentation/tooling-only push: compilation and migration gates are not required.'
        exit 0
    }

    Invoke-RequiredCommand 'Build (Release)' {
        & dotnet build RikiPath.Backend.slnx -c Release
    }
    Invoke-RequiredCommand 'Security analyzers (error severity)' {
        & dotnet format analyzers RikiPath.Backend.slnx --verify-no-changes --severity error --no-restore
    }

    if ($modelRelevant.Count -gt 0) {
        Invoke-RequiredCommand 'Migration source, model drift, and approved local/QA readiness' {
            & (Join-Path $PSScriptRoot 'verify-migration-readiness.ps1') -NoBuild -RequireLocalOrQa
        }
    } else {
        Write-Host 'Migration: NOT REQUIRED (no EF model/migration paths changed).'
    }

    if ($apiRelevant.Count -gt 0) {
        Write-Host 'API: PARTIAL — build/analyzers passed; confirm targeted live endpoint verification in the task report.'
    } else {
        Write-Host 'API: NOT RUN — no API-contract paths changed.'
    }

    Write-Host 'Code: PASS'
    Write-Host 'Build: PASS'
    Write-Host 'Security: PASS (error-severity analyzer gate)'
    Write-Host 'Git: push gate passed; CI and branch protection remain mandatory.'
    Write-Host 'Shared Supabase: UNTOUCHED'
}
catch {
    Write-Error $_
    Write-Host 'Code: FAIL'
    Write-Host 'Git: PUSH BLOCKED'
    exit 1
}
finally {
    Pop-Location
}
