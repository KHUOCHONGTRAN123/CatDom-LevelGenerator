[CmdletBinding()]
param([Parameter(Position=0)][string]$PatchPath, [string]$BranchName, [switch]$DryRun, [switch]$Help)
if ($Help) {
    Write-Host 'Apply-ChatGPT-Patch.ps1 [-PatchPath <file.patch>] [-BranchName feature/name] [-DryRun] [-Help]'
    Write-Host 'Example: .\Apply-ChatGPT-Patch.ps1 "C:\Downloads\milestone.patch" -DryRun'
    exit 0
}
. "$PSScriptRoot\Workflow.Common.ps1"
$original = Get-Location
$oldOptionalLocks = $env:GIT_OPTIONAL_LOCKS
$env:GIT_OPTIONAL_LOCKS = '0'
$applied = $false
try {
    Initialize-Workflow
    if (@(Invoke-Git @('status', '--porcelain', '--untracked-files=all')).Count) { throw 'Working tree is dirty (including staged/untracked files). Commit or stash your work first.' }
    if (-not $PatchPath) { $PatchPath = Read-Host 'Patch path' }
    $PatchPath = $PatchPath.Trim('"')
    # Resolve relative input against the caller, not the repository.
    if (-not [IO.Path]::IsPathRooted($PatchPath)) { $PatchPath = Join-Path $original $PatchPath }
    $PatchPath = (Resolve-Path -LiteralPath $PatchPath).ProviderPath
    if (-not (Test-Path -LiteralPath $PatchPath -PathType Leaf)) { throw 'Patch file does not exist.' }
    $paths = @(Get-PatchPaths $PatchPath)
    Write-Host "Files to change ($($paths.Count)):"; $paths | ForEach-Object { Write-Host "  $_" }
    $automation = @($paths | Where-Object { $_ -match '^(?i:tools/chatgpt-workflow/|\.github/workflows/)' })
    if ($automation.Count -and -not $DryRun) {
        if ((Read-Host 'Patch changes automation/CI. Type ALLOW to permit these changes') -cne 'ALLOW') { throw 'Automation changes were not confirmed.' }
    }
    if (-not $BranchName) {
        $suggested = 'feature/chatgpt-m5-1-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
        if ($DryRun) { $BranchName = $suggested } else {
            $BranchName = Read-Host "Feature branch [$suggested]"
            if (-not $BranchName) { $BranchName = $suggested }
        }
    }
    if (-not $BranchName.StartsWith('feature/')) { throw 'Use a feature/* branch; main/develop cannot be modified.' }
    Invoke-Git @('check-ref-format', '--branch', $BranchName) | Out-Null
    $existing = @(Invoke-Git @('branch', '--list', $BranchName))
    if ($existing.Count) { throw "Branch already exists: $BranchName" }
    if ($DryRun) {
        Invoke-Git @('ls-remote', '--exit-code', '--heads', 'origin', 'refs/heads/develop') | Out-Null
        Write-Host "DRY RUN: fetch origin develop; create $BranchName from origin/develop; git apply --check; git apply."
        Write-Host 'Patch syntax/path checks passed. Applicability to freshly fetched develop is checked during actual application.'
        if ($automation.Count) { Write-Host 'Actual run requires ALLOW confirmation for automation/CI changes.' }
        Show-ValidationPlan
        exit 0
    }
    Invoke-Git @('fetch', 'origin', '+refs/heads/develop:refs/remotes/origin/develop') | Out-Null
    Invoke-Git @('switch', '-c', $BranchName, '--no-track', 'refs/remotes/origin/develop') | Out-Null
    $script:Branch = $BranchName
    Invoke-Git @('apply', '--check', '--', $PatchPath) | Out-Null
    Invoke-Git @('apply', '--', $PatchPath) | Out-Null
    $applied = $true
    Write-Host 'PATCH: APPLIED'
    $pass = Invoke-Validation
    Write-Host "BRANCH: $script:Branch`nCHANGED FILES: $($paths.Count)"
    Write-Host 'Changes are uncommitted. Review: git diff --stat; git diff; git status --short'
    if (-not $pass) { throw 'Validation failed. Working changes and logs have been preserved.' }
    Write-Host 'PASS. After review, run Publish-ChatGPT-Changes.bat.'
} catch {
    Write-Host "FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $applied) { Write-Host 'PATCH: NOT APPLIED' }
    Write-Host 'Recovery: inspect git status, then save work with git stash push -u before switching branches. Ignored logs remain local.'
    exit 1
} finally { $env:GIT_OPTIONAL_LOCKS = $oldOptionalLocks; Set-Location -LiteralPath $original }
