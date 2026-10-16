# Shared implementation; compatible with Windows PowerShell 5.1.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:ExpectedRepo = 'KHUOCHONGTRAN123/CatDom-LevelGenerator'

function Invoke-Git {
    param([string[]]$Arguments)
    $preference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $output = @(& git -c core.quotePath=false @Arguments 2>&1)
    $code = $LASTEXITCODE
    $ErrorActionPreference = $preference
    if ($code -ne 0) { throw "git $($Arguments -join ' ') failed ($code): $($output -join [Environment]::NewLine)" }
    return $output
}

function Initialize-Workflow {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Git is required.' }
    $script:RepoRoot = [string](Invoke-Git @('-C', $PSScriptRoot, 'rev-parse', '--show-toplevel'))
    $script:RepoRoot = [IO.Path]::GetFullPath($script:RepoRoot)
    Set-Location -LiteralPath $script:RepoRoot
    $remote = [string](Invoke-Git @('remote', 'get-url', 'origin'))
    if ($remote -notmatch '^https://github\.com/KHUOCHONGTRAN123/CatDom-LevelGenerator(?:\.git)?/?$' -and
        $remote -notmatch '^git@github\.com:KHUOCHONGTRAN123/CatDom-LevelGenerator(?:\.git)?$') {
        throw "Unexpected origin: $remote. Expected $script:ExpectedRepo."
    }
    $pushRemotes = @(Invoke-Git @('remote', 'get-url', '--push', '--all', 'origin'))
    if ($pushRemotes.Count -ne 1 -or [string]$pushRemotes[0] -ne $remote) { throw 'Multiple or separate push URLs are unsupported; inspect origin configuration.' }
    if (@(Invoke-Git @('config', '--get-all', 'remote.origin.url')).Count -ne 1) { throw 'Multiple origin URLs are unsupported.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK 10.0.x is required.' }
    $sdks = @(& dotnet --list-sdks)
    if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^10\.0\.')) { throw '.NET SDK 10.0.x is required.' }
    foreach ($path in @('Solver.slnx', 'LevelGenerator.Tests', 'Verification')) {
        if (-not (Test-Path -LiteralPath $path)) { throw "Missing project path: $path" }
    }
    $script:Branch = [string](Invoke-Git @('symbolic-ref', '--short', 'HEAD'))
}

function Find-Gh {
    $command = Get-Command gh -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    foreach ($path in @("$env:ProgramFiles\GitHub CLI\gh.exe", "$env:LOCALAPPDATA\Programs\GitHub CLI\gh.exe")) {
        if (Test-Path -LiteralPath $path) { return $path }
    }
    throw 'GitHub CLI is missing. Install GitHub CLI, then run: gh auth login --hostname github.com --git-protocol https --web'
}

function Assert-Gh {
    $script:Gh = Find-Gh
    # Suppress auth output, which may contain credential metadata.
    & $script:Gh auth status --hostname github.com *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Run: gh auth login --hostname github.com --git-protocol https --web' }
}

function Assert-SafePath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path -match '[\x00-\x1f"\\:]' -or
        $Path.StartsWith('/') -or $Path -match '(^|/)\.\.(/|$)' -or
        $Path -match '(^|/)\.git(?:[ .]|/|$)' -or $Path -match '(^|/)\.(?:/|$)' -or
        $Path -match '(^|/)[^/]*[ .](?:/|$)') { throw "Unsafe or unsupported path: $Path" }
    $full = [IO.Path]::GetFullPath((Join-Path $script:RepoRoot $Path))
    if (-not $full.StartsWith($script:RepoRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes repository: $Path"
    }
    $cursor = $full
    while ($cursor.Length -gt $script:RepoRoot.Length) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -Force -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked paths are unsupported: $Path" }
        }
        $cursor = Split-Path -Parent $cursor
    }
}

function Assert-PublishPath {
    param([string]$Path)
    Assert-SafePath $Path
    if ($Path -match '(^|/)(generated|bin|obj|temp|tmp|artifacts|\.vs|\.idea|node_modules)(/|$)' -or
        $Path -match '(?i)(^|/)(\.env(?:\..*)?|credentials[^/]*|secrets[^/]*|id_rsa|id_ed25519|\.npmrc|\.netrc)$' -or
        $Path -match '(?i)\.(pem|key|pfx|p12|p8|tmp|temp|log|zip|7z|bak)$') {
        throw "Excluded output or credential file: $Path"
    }
}

function Assert-NoSecrets {
    param([string]$Text, [string]$Path)
    if ($Text -match '(?i)(-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|gh[pousr]_[a-z0-9]{20,}|github_pat_[a-z0-9_]{20,}|AKIA[0-9A-Z]{16}|sk-[a-z0-9_-]{20,}|(?:password|api[_-]?key|client[_-]?secret|access[_-]?token)\s*[=:]\s*["'']?[^\s"'';]{8,})') {
        throw "Possible secret in $Path. Remove it and review before publishing."
    }
}

function Get-PatchPaths {
    param([string]$Patch)
    $raw = [IO.File]::ReadAllText($Patch)
    if ($raw -notmatch '(?m)^diff --git ' -or $raw -notmatch '(?m)^@@ ') { throw 'Expected a unified Git text diff with hunks.' }
    if ($raw -match '(?m)^(GIT binary patch|Binary files |rename |copy |similarity index |dissimilarity index |old mode |new mode )' -or
        $raw -match '(?m)^(?:new file mode|deleted file mode) (?!100644\r?$)' -or
        $raw -match '(?m)^index .* (?!100644\r?$)\d{6}\r?$') { throw 'Binary, rename, copy, symlink, submodule, and mode changes are unsupported.' }
    # Validate every header as well as Git-parsed paths; never enable --unsafe-paths.
    foreach ($line in ($raw -split '\r?\n')) {
        if ($line -match '^(---|\+\+\+) (.*)$') {
            $p = $Matches[2]
            if ($p -eq '/dev/null') { continue }
            if ($p -notmatch '^[ab]/(.+)$') { throw "Unsupported patch header: $line" }
            Assert-SafePath $Matches[1]
        }
    }
    $paths = @()
    foreach ($line in @(Invoke-Git @('apply', '--numstat', '--', $Patch))) {
        if ([string]$line -notmatch '^\d+\t\d+\t(.+)$') { throw "Unsupported patch entry: $line" }
        $p = $Matches[1]; Assert-SafePath $p; $paths += $p
    }
    if ($paths.Count -eq 0) { throw 'Patch has no text changes.' }
    return @($paths | Select-Object -Unique)
}

function Show-ValidationPlan {
    Write-Host 'dotnet restore Solver.slnx'
    Write-Host 'dotnet build Solver.slnx -c Release --no-restore -m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false'
    Write-Host 'dotnet run --project LevelGenerator.Tests -c Release --no-build --no-restore'
    Write-Host 'dotnet Verification/bin/Release/net10.0/Solver.Verification.dll v3-countdown-oracle-tests .'
}

function Invoke-Validation {
    $folder = Join-Path $script:RepoRoot ('artifacts/chatgpt-workflow/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $steps = @(
        @{ Name = 'RESTORE'; Args = @('restore', 'Solver.slnx') },
        @{ Name = 'BUILD'; Args = @('build', 'Solver.slnx', '-c', 'Release', '--no-restore', '-m:1', '/p:BuildInParallel=false', '/p:UseSharedCompilation=false') },
        @{ Name = 'GENERATOR TESTS'; Args = @('run', '--project', 'LevelGenerator.Tests', '-c', 'Release', '--no-build', '--no-restore') },
        @{ Name = 'INDEPENDENT ORACLE'; Args = @('Verification/bin/Release/net10.0/Solver.Verification.dll', 'v3-countdown-oracle-tests', '.') }
    )
    $ok = $true; $canBuild = $true
    foreach ($step in $steps) {
        $log = Join-Path $folder ($step.Name.Replace(' ', '-') + '.log')
        if (-not $canBuild) { Write-Host "$($step.Name): FAIL (prerequisite failed; not run)"; continue }
        Write-Host "Running: dotnet $($step.Args -join ' ')"
        $oldPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & dotnet @($step.Args) > $log 2>&1
        $code = $LASTEXITCODE
        $ErrorActionPreference = $oldPreference
        Add-Content -LiteralPath $log -Value "`nEXIT CODE: $code"
        $result = 'PASS'; if ($code -ne 0) { $result = 'FAIL'; $ok = $false }
        Write-Host "$($step.Name): $result (exit $code); log: $log"
        if ($code -ne 0 -and $step.Name -in @('RESTORE', 'BUILD')) { $canBuild = $false }
    }
    return $ok
}
