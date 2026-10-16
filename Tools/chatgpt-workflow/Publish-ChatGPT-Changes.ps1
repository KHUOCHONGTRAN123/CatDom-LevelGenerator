[CmdletBinding()]
param([string[]]$Files, [switch]$DryRun, [switch]$Help)
if ($Help) {
    Write-Host 'Publish-ChatGPT-Changes.ps1 [-Files <repo-relative paths>] [-DryRun] [-Help]'
    Write-Host 'Example: .\Publish-ChatGPT-Changes.ps1 -Files README.md,docs/design.md -DryRun'
    exit 0
}
. "$PSScriptRoot\Workflow.Common.ps1"
$original = Get-Location
$oldOptionalLocks = $env:GIT_OPTIONAL_LOCKS
$env:GIT_OPTIONAL_LOCKS = '0'
try {
    Initialize-Workflow
    if (-not $script:Branch.StartsWith('feature/')) { throw 'Publication requires a feature/* branch; main/develop are protected.' }
    Assert-Gh
    Invoke-Git @('ls-remote', '--exit-code', '--heads', 'origin', 'refs/heads/develop') | Out-Null
    if (@(Invoke-Git @('diff', '--cached', '--name-only')).Count) { throw 'Index must be empty. Review existing staging; use git restore --staged -- <path> to unstage without losing work.' }
    $candidates = @(@(Invoke-Git @('diff', '--name-only', '--no-renames')) + @(Invoke-Git @('ls-files', '--others', '--exclude-standard')) | Sort-Object -Unique)
    if (-not $candidates.Count) { throw 'No uncommitted changes to publish. For an interrupted push/PR, see README recovery instructions.' }
    Write-Host 'Available files:'
    for ($i = 0; $i -lt $candidates.Count; $i++) { Write-Host "  $($i + 1). $($candidates[$i])" }
    if (-not $Files) {
        if ($DryRun) { $Files = $candidates } else {
            $selection = Read-Host 'Select file numbers separated by commas (no default/all)'
            $Files = @($selection.Split(',') | ForEach-Object {
                $n = 0
                if (-not [int]::TryParse($_.Trim(), [ref]$n) -or $n -lt 1 -or $n -gt $candidates.Count) { throw 'Invalid file selection.' }
                $candidates[$n - 1]
            } | Select-Object -Unique)
        }
    }
    foreach ($path in $Files) {
        if ($candidates -cnotcontains $path) { throw "Not an available changed file: $path" }
        Assert-PublishPath $path
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            Assert-NoSecrets ([IO.File]::ReadAllText((Join-Path $script:RepoRoot $path))) $path
        }
    }
    if ($DryRun) {
        Write-Host "DRY RUN: validate; stage selected files: $($Files -join ', '); scan outgoing changes; request commit message and PUBLISH confirmation; commit; push $script:Branch; create/reuse PR targeting develop."
        Show-ValidationPlan
        exit 0
    }
    Invoke-Git @('fetch', 'origin', '+refs/heads/develop:refs/remotes/origin/develop') | Out-Null
    Invoke-Git @('merge-base', '--is-ancestor', 'refs/remotes/origin/develop', 'HEAD') | Out-Null
    # Unselected changes could affect validation but would not be included in the commit.
    if (@($candidates | Where-Object { $Files -cnotcontains $_ }).Count) { throw 'Unselected changes remain. Stash them separately before validation/publication.' }
    $contentBefore = @{}
    foreach ($path in $Files) {
        $contentBefore[$path] = if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'DELETED' }
    }
    if (-not (Invoke-Validation)) { throw 'Validation failed; nothing staged, committed or pushed.' }
    foreach ($path in $Files) {
        $contentAfter = if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'DELETED' }
        if ($contentBefore[$path] -ne $contentAfter) { throw "Validation modified $path. Review and rerun." }
    }
    $after = @(@(Invoke-Git @('diff', '--name-only', '--no-renames')) + @(Invoke-Git @('ls-files', '--others', '--exclude-standard')) | Sort-Object -Unique)
    if (@(Compare-Object $candidates $after).Count) { throw 'Validation changed the file list. Review and rerun.' }
    if (@(Invoke-Git @('diff', '--cached', '--name-only')).Count) { throw 'Validation changed the index. Review staging and rerun.' }
    Invoke-Git (@('add', '--') + $Files) | Out-Null
    # Scan the entire outgoing branch diff, including any existing local commits.
    $outgoing = @(Invoke-Git @('diff', '--cached', '--name-only', '--no-renames', 'refs/remotes/origin/develop'))
    foreach ($path in $outgoing) {
        Assert-PublishPath $path
        $entry = @(Invoke-Git @('ls-files', '--stage', '--', $path))
        if ($entry.Count) {
            if ([string]$entry[0] -notmatch '^100644 ') { throw "Unsupported executable/link/submodule: $path" }
            Assert-NoSecrets ((Invoke-Git @('show', ":$path")) -join "`n") $path
        }
    }
    # Examine added lines in every outgoing commit too, including subsequently removed secrets.
    Assert-NoSecrets ((Invoke-Git @('log', '-p', '--format=', 'refs/remotes/origin/develop..HEAD')) -join "`n") 'outgoing commit history'
    Invoke-Git @('diff', '--cached', '--stat') | ForEach-Object { Write-Host $_ }
    Invoke-Git @('diff', '--cached', '--check') | Out-Null
    $tree = [string](Invoke-Git @('write-tree'))
    $message = Read-Host 'Commit message [feat(workflow): apply reviewed ChatGPT patch]'
    if (-not $message) { $message = 'feat(workflow): apply reviewed ChatGPT patch' }
    $summary = Read-Host 'PR summary (describe resulting behavior)'
    if (-not $summary) { $summary = $message }
    $risks = Read-Host 'Risks or known limitations [No additional limitations reported by author]'
    if (-not $risks) { $risks = 'No additional limitations reported by author.' }
    if ((Read-Host "Commit and push $script:Branch, then create/reuse PR to develop? Type PUBLISH") -cne 'PUBLISH') { throw 'Publication cancelled; selected changes remain staged for review.' }
    if ([string](Invoke-Git @('write-tree')) -ne $tree -or @(Invoke-Git (@('diff', '--name-only', '--') + $Files)).Count) { throw 'Selected content changed after validation. Review and rerun.' }
    Invoke-Git @('commit', '-m', $message) | ForEach-Object { Write-Host $_ }
    $hash = [string](Invoke-Git @('rev-parse', 'HEAD'))
    Invoke-Git @('push', '--set-upstream', 'origin', "HEAD:refs/heads/$script:Branch") | ForEach-Object { Write-Host $_ }
    $prs = @(& $script:Gh pr list --repo $script:ExpectedRepo --head $script:Branch --base develop --state open --json url --jq '.[].url')
    if ($LASTEXITCODE -ne 0) { throw 'Push succeeded, but PR lookup failed. Retry using gh pr list/create; see README.' }
    if (-not $prs.Count) {
        $body = "$summary`n`nModified files:`n" + (($outgoing | ForEach-Object { "- $_" }) -join "`n") + "`n`nValidation: dependency restore, Release build, Generator Test Harness, independent countdown oracle: PASS (process exit codes 0).`n`nRisks / known limitations: $risks`n`nCI review is still required. GitHub Actions results have not been asserted."
        $bodyPath = Join-Path $script:RepoRoot 'artifacts/chatgpt-workflow/pr-description.md'
        [IO.File]::WriteAllText($bodyPath, $body, (New-Object Text.UTF8Encoding($false)))
        $prs = @(& $script:Gh pr create --repo $script:ExpectedRepo --base develop --head $script:Branch --title $message --body-file $bodyPath)
        if ($LASTEXITCODE -ne 0) { throw 'Push succeeded, but PR creation failed. Retry gh pr create with artifacts/chatgpt-workflow/pr-description.md.' }
    }
    Write-Host "COMMIT: $hash`nBRANCH: $script:Branch`nPUSH: SUCCESS`nPR: $($prs -join ', ')`nTARGET: develop"
} catch {
    Write-Host "FAIL: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Work is preserved. Inspect git status; if staged, git restore --staged -- <path> preserves edits. Never reset/clean to recover.'
    exit 1
} finally { $env:GIT_OPTIONAL_LOCKS = $oldOptionalLocks; Set-Location -LiteralPath $original }
