# Isolated integration tests. Uses real Git and mocked dotnet/gh; never contacts GitHub.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
$sandbox = Join-Path $env:TEMP ('CatDom workflow space ' + [char]0x732b + '-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $sandbox | Out-Null
$oldPath = $env:PATH
$oldLocation = Get-Location
$oldProgramFiles = $env:ProgramFiles
$oldLocalAppData = $env:LOCALAPPDATA
$passed = 0
function Git-Test {
    param([string[]]$Arguments)
    $ErrorActionPreference = 'Continue'
    $result = @(& git @Arguments 2>&1)
    $ErrorActionPreference = 'Stop'
    if ($LASTEXITCODE -ne 0) { throw "Fixture Git failed: $result" }
    return $result
}
function Assert-Test { param([bool]$Condition, [string]$Name)
    if (-not $Condition) { Write-Host ($script:toolOutput -join "`n"); throw "FAIL: $Name" }
    $script:passed++; Write-Host "PASS: $Name"
}
function Run-Tool { param([string]$Tool, [string[]]$Arguments, [string[]]$InputLines)
    if ($InputLines) {
        $script:toolOutput = @($InputLines | & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo "tools/chatgpt-workflow/$Tool.ps1") @Arguments 2>&1)
    } else {
        $script:toolOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo "tools/chatgpt-workflow/$Tool.ps1") @Arguments 2>&1)
    }
    $script:toolCode = $LASTEXITCODE
}
function New-Fixture {
    $script:repo = Join-Path $sandbox ([guid]::NewGuid().ToString('N') + ' repo')
    New-Item -ItemType Directory -Path "$repo/tools/chatgpt-workflow", "$repo/LevelGenerator.Tests", "$repo/Verification" -Force | Out-Null
    Copy-Item -Path "$source/*" -Destination "$repo/tools/chatgpt-workflow"
    [IO.File]::WriteAllText("$repo/Solver.slnx", 'fixture')
    [IO.File]::WriteAllText("$repo/example.txt", "before`n")
    [IO.File]::WriteAllText("$repo/.gitignore", "artifacts/`ngenerated/`n**/bin/`n**/obj/`n")
    Set-Location -LiteralPath $repo
    Git-Test @('init', '-b', 'develop') | Out-Null
    Git-Test @('config', 'user.email', 'workflow-test@example.invalid') | Out-Null
    Git-Test @('config', 'user.name', 'Workflow Test') | Out-Null
    Git-Test @('config', 'core.autocrlf', 'false') | Out-Null
    Git-Test @('add', '.') | Out-Null
    Git-Test @('commit', '-m', 'fixture') | Out-Null
    $bare = "$repo-remote.git"
    Git-Test @('clone', '--bare', '.', $bare) | Out-Null
    Git-Test @('remote', 'add', 'origin', 'https://github.com/KHUOCHONGTRAN123/CatDom-LevelGenerator.git') | Out-Null
    # URL rewrite is local to this disposable repository. Production origin identity remains hard-coded.
    Git-Test @('config', "url.$($bare.Replace('\','/')).insteadOf", 'https://github.com/KHUOCHONGTRAN123/CatDom-LevelGenerator.git') | Out-Null
    # get-url expands insteadOf; wrapper hides only that expansion for identity checks.
    $env:WORKFLOW_FIXTURE_REPO = $repo
    $env:WORKFLOW_FAIL = ''
    $script:patch = Join-Path $sandbox ([guid]::NewGuid().ToString('N') + '.patch')
    [IO.File]::WriteAllText($patch, "diff --git a/example.txt b/example.txt`nindex 3367afd..0000000 100644`n--- a/example.txt`n+++ b/example.txt`n@@ -1 +1 @@`n-before`n+after`n")
}
try {
    $mock = Join-Path $sandbox 'mock'
    New-Item -ItemType Directory -Path $mock | Out-Null
    $realGit = (Get-Command git.exe).Source
    # Invoke real Git except identity queries (which otherwise expand local fixture rewrites).
    [IO.File]::WriteAllText("$mock/git.cmd", "@echo off`r`necho %* | findstr /C:`"remote get-url`" >nul`r`nif not errorlevel 1 (echo https://github.com/KHUOCHONGTRAN123/CatDom-LevelGenerator.git& exit /b 0)`r`n`"$realGit`" %*`r`nexit /b %errorlevel%`r`n")
    [IO.File]::WriteAllText("$mock/dotnet.cmd", "@echo off`r`nif `%1==--list-sdks (echo 10.0.302 [mock]& exit /b 0)`r`necho Mock dotnet %*`r`nif `%1==%WORKFLOW_FAIL% exit /b 7`r`nexit /b 0`r`n")
    [IO.File]::WriteAllText("$mock/gh.cmd", "@echo off`r`nif `%1==auth exit /b 0`r`nif `%2==list exit /b 0`r`nif `%2==create (echo https://github.com/example/fixture/pull/1& exit /b 0)`r`nexit /b 9`r`n")
    $env:PATH = "$mock;$oldPath"
    New-Fixture
    $indexBefore = (Get-FileHash -LiteralPath "$repo/.git/index").Hash
    Run-Tool 'Apply-ChatGPT-Patch' @('-PatchPath', $patch, '-BranchName', 'feature/test', '-DryRun')
    Assert-Test ($toolCode -eq 0 -and (Get-FileHash -LiteralPath "$repo/.git/index").Hash -eq $indexBefore -and (Git-Test @('branch', '--show-current')) -eq 'develop' -and -not @(Git-Test @('status', '--porcelain')).Count -and -not (Test-Path "$repo/artifacts")) 'Apply dry-run changes no files, branch, index, or logs'
    Run-Tool 'Apply-ChatGPT-Patch' @('-PatchPath', $patch, '-BranchName', 'feature/test')
    if ($toolCode -ne 0) { Write-Host ($toolOutput -join "`n") }
    Assert-Test ($toolCode -eq 0 -and [IO.File]::ReadAllText("$repo/example.txt") -eq "after`n") 'Apply valid text patch in space/Unicode repository'
    Assert-Test ((Git-Test @('branch', '--show-current')) -eq 'feature/test' -and (Git-Test @('rev-parse', 'HEAD')) -eq (Git-Test @('rev-parse', 'origin/develop'))) 'Feature starts at remote develop with changes uncommitted'
    Run-Tool 'Apply-ChatGPT-Patch' @('-PatchPath', $patch, '-BranchName', 'feature/dirty')
    Assert-Test ($toolCode -ne 0 -and ($toolOutput -join '') -match 'dirty') 'Reject dirty tree'
    New-Fixture
    [IO.File]::WriteAllText($patch, 'invalid')
    Run-Tool 'Apply-ChatGPT-Patch' @('-PatchPath', $patch, '-BranchName', 'feature/invalid')
    Assert-Test ($toolCode -ne 0 -and [IO.File]::ReadAllText("$repo/example.txt") -eq "before`n") 'Invalid patch preserves source'
    New-Fixture
    [IO.File]::WriteAllText($patch, [IO.File]::ReadAllText($patch).Replace('-before', '-missing context'))
    Run-Tool 'Apply-ChatGPT-Patch' @('-PatchPath', $patch, '-BranchName', 'feature/conflict')
    Assert-Test ($toolCode -ne 0 -and -not @(Git-Test @('status', '--porcelain')).Count) 'Inapplicable patch fails atomically'
    New-Fixture
    $addition = "diff --git a/new text.txt b/new text.txt`nnew file mode 100644`n--- /dev/null`n+++ b/new text.txt`n@@ -0,0 +1 @@`n+new text`n"
    $deletion = "diff --git a/example.txt b/example.txt`ndeleted file mode 100644`n--- a/example.txt`n+++ /dev/null`n@@ -1 +0,0 @@`n-before`n"
    [IO.File]::WriteAllText($patch, $addition + $deletion)
    Run-Tool 'Apply-ChatGPT-Patch' @('-PatchPath', $patch, '-BranchName', 'feature/add-delete')
    Assert-Test ($toolCode -eq 0 -and (Test-Path "$repo/new text.txt") -and -not (Test-Path "$repo/example.txt")) 'Text additions/deletions and file paths containing spaces supported'
    New-Fixture
    Run-Tool 'Apply-ChatGPT-Patch' @('-PatchPath', $patch, '-BranchName', 'main')
    Assert-Test ($toolCode -ne 0) 'Apply refuses protected target branch'
    foreach ($branch in @('develop', 'main')) {
        if ($branch -eq 'main') { Git-Test @('switch', '-c', 'main') | Out-Null }
        Run-Tool 'Publish-ChatGPT-Changes' @('-DryRun')
        Assert-Test ($toolCode -ne 0 -and ($toolOutput -join '') -match 'main/develop') "Publish refuses $branch"
    }
    New-Fixture
    $env:WORKFLOW_FAIL = 'run'
    $head = Git-Test @('rev-parse', 'HEAD')
    Run-Tool 'Apply-ChatGPT-Patch' @('-PatchPath', $patch, '-BranchName', 'feature/failure')
    Assert-Test ($toolCode -ne 0 -and (Git-Test @('rev-parse', 'HEAD')) -eq $head -and (Test-Path "$repo/artifacts/chatgpt-workflow")) 'Harness failure preserves work/logs and creates no commit'
    Run-Tool 'Publish-ChatGPT-Changes' @('-Files', 'example.txt')
    Assert-Test ($toolCode -ne 0 -and (Git-Test @('rev-parse', 'HEAD')) -eq $head -and -not @(Git-Test @('diff', '--cached', '--name-only')).Count) 'Publish stops before staging/commit on validation failure'
    $env:WORKFLOW_FAIL = ''
    New-Item -ItemType Directory -Path "$repo/generated" | Out-Null
    [IO.File]::WriteAllText("$repo/generated/output.txt", 'ignored')
    Run-Tool 'Publish-ChatGPT-Changes' @('-Files', 'generated/output.txt', '-DryRun')
    Assert-Test ($toolCode -ne 0 -and -not @(Git-Test @('diff', '--cached', '--name-only')).Count) 'Ignored generated output cannot be selected/staged'
    $before = @(Git-Test @('status', '--porcelain')) -join "`n"
    Run-Tool 'Publish-ChatGPT-Changes' @('-Files', 'example.txt', '-DryRun')
    Assert-Test ($toolCode -eq 0 -and $before -eq (@(Git-Test @('status', '--porcelain')) -join "`n") -and (Git-Test @('rev-parse', 'HEAD')) -eq $head) 'Publish dry-run preserves work/index/HEAD'
    Run-Tool 'Publish-ChatGPT-Changes' @('-Files', 'example.txt') @('feat(test): fixture publication', 'Fixture text update', 'Mock-only test', 'PUBLISH')
    Assert-Test ($toolCode -eq 0 -and (Git-Test @('rev-parse', 'HEAD')) -ne $head -and -not @(Git-Test @('ls-files', 'generated')).Count -and ($toolOutput -join '') -match 'PR: https://github.com/example/fixture/pull/1') 'Confirmed publication commits only selection, pushes to local bare fixture, uses mocked PR'
    New-Fixture
    $badPatch = [IO.File]::ReadAllText($patch).Replace('a/example.txt', 'a/../escape.txt').Replace('b/example.txt', 'b/../escape.txt')
    [IO.File]::WriteAllText($patch, $badPatch)
    Run-Tool 'Apply-ChatGPT-Patch' @('-PatchPath', $patch, '-BranchName', 'feature/traversal')
    Assert-Test ($toolCode -ne 0 -and ($toolOutput -join '') -match 'Unsafe') 'Traversal patch rejected'
    New-Fixture
    [IO.File]::WriteAllText($patch, "diff --git a/example.txt b/example.txt`nGIT binary patch`nliteral 3`nabc`n")
    Run-Tool 'Apply-ChatGPT-Patch' @('-PatchPath', $patch, '-BranchName', 'feature/binary')
    Assert-Test ($toolCode -ne 0) 'Binary patch rejected'
    New-Fixture
    Git-Test @('switch', '-c', 'feature/secret') | Out-Null
    [IO.File]::WriteAllText("$repo/credentials.json", '{}')
    Run-Tool 'Publish-ChatGPT-Changes' @('-Files', 'credentials.json', '-DryRun')
    Assert-Test ($toolCode -ne 0 -and ($toolOutput -join '') -match 'credential') 'Credential filename rejected'
    Remove-Item -LiteralPath "$repo/credentials.json"
    [IO.File]::WriteAllText("$repo/example.txt", ('api' + '_key=' + 'sensitivevalue12345'))
    Run-Tool 'Publish-ChatGPT-Changes' @('-Files', 'example.txt', '-DryRun')
    Assert-Test ($toolCode -ne 0 -and ($toolOutput -join '') -match 'Possible secret') 'Likely secret content rejected without printing value'
    Rename-Item -LiteralPath "$mock/gh.cmd" -NewName gh.disabled
    $env:PATH = "$mock;$env:SystemRoot\System32;$env:SystemRoot\System32\WindowsPowerShell\v1.0"
    # PowerShell may reconstruct ProgramFiles on startup. Hide fallback locations only in this fixture.
    $commonPath = "$repo/tools/chatgpt-workflow/Workflow.Common.ps1"
    $commonText = [IO.File]::ReadAllText($commonPath).Replace('$env:ProgramFiles', '$env:WORKFLOW_MISSING_GH').Replace('$env:LOCALAPPDATA', '$env:WORKFLOW_MISSING_GH')
    [IO.File]::WriteAllText($commonPath, $commonText)
    $env:WORKFLOW_MISSING_GH = $sandbox
    $env:ProgramFiles = $sandbox; $env:LOCALAPPDATA = $sandbox
    Run-Tool 'Publish-ChatGPT-Changes' @('-DryRun')
    Assert-Test ($toolCode -ne 0 -and ($toolOutput -join '') -match 'GitHub CLI is missing') 'Missing GitHub CLI gives setup action'
    Write-Host "$passed assertions passed. Fixtures retained at: $sandbox"
} finally {
    $env:PATH = $oldPath; $env:ProgramFiles = $oldProgramFiles; $env:LOCALAPPDATA = $oldLocalAppData
    Remove-Item Env:WORKFLOW_FAIL,Env:WORKFLOW_FIXTURE_REPO,Env:WORKFLOW_MISSING_GH -ErrorAction SilentlyContinue
    Set-Location -LiteralPath $oldLocation
}
