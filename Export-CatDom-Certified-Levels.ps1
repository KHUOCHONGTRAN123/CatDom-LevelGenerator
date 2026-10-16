param(
    [Parameter(Mandatory=$true)][string]$BatchDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$batch = (Resolve-Path -LiteralPath $BatchDirectory).Path
$destination = [IO.Path]::GetFullPath($OutputDirectory)
$summaryFile = Join-Path $batch 'batch-summary.json'
if (!(Test-Path -LiteralPath $summaryFile -PathType Leaf)) {
    throw "Missing batch-summary.json: $summaryFile"
}
$summary = Get-Content -LiteralPath $summaryFile -Raw | ConvertFrom-Json
if ((Test-Path -LiteralPath $destination) -and (Get-ChildItem -LiteralPath $destination -Force | Select-Object -First 1)) {
    throw "Refusing to overwrite nonempty levels folder: $destination"
}
[IO.Directory]::CreateDirectory($destination) | Out-Null
$exported = 0
foreach ($entry in $summary.rows) {
    if ($entry.status -ne 'ReviewRequired' -or $entry.independentlyReplayed -ne $true) { continue }
    $seed = [int]$entry.seed
    if ($seed -lt 0 -or $seed -gt 99999) { throw "Seed outside Levelxxxxx naming range: $seed" }
    $seedDir = Join-Path $batch "seed-$seed"
    $reportPath = Join-Path $seedDir 'report.json'
    $draftPath = Join-Path $seedDir 'certified-draft.json'
    if (!(Test-Path -LiteralPath $reportPath -PathType Leaf) -or !(Test-Path -LiteralPath $draftPath -PathType Leaf)) {
        throw "Missing certified report/draft for seed $seed"
    }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($report.status -ne 'ReviewRequired' -or $report.independentlyReplayed -ne $true -or [string]::IsNullOrEmpty($report.inputHash)) {
        throw "Invalid certification report for seed $seed"
    }
    # SHA256 is computed over decoded UTF-8 text, like HashUtil.Text in the C# generator.
    $content = [IO.File]::ReadAllText($draftPath)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $rawHash = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($content)) } finally { $sha.Dispose() }
    $hash = [BitConverter]::ToString($rawHash).Replace('-','').ToLowerInvariant()
    if ($hash -ne $report.inputHash) { throw "Certified draft checksum mismatch: seed $seed" }
    $level = $content | ConvertFrom-Json
    if ($level.sizeX -ne 7 -or $level.sizeY -ne 8 -or $null -eq $level.holeInfos) {
        throw "Invalid level format for seed $seed"
    }
    $filename = 'Level{0:D5}.json' -f $seed
    $outPath = Join-Path $destination $filename
    if (Test-Path -LiteralPath $outPath) { throw "Level already exists: $outPath" }
    [IO.File]::WriteAllText($outPath, $content, (New-Object System.Text.UTF8Encoding($false)))
    $exported++
}
Write-Host "[OK] Exported $exported certified level(s) into: $destination"
Write-Host "     Only Levelxxxxx.json files; all are playtest drafts, not designer-approved."
