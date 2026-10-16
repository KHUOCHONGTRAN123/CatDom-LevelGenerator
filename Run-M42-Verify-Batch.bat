@echo off
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
title CatDom - M4.2 Verify Batch (fixed folder detection)

rem Usage: double-click for latest generated batch, or drag a batch folder onto this BAT.
rem You may also drag an A/B parent folder: the GUIDED subfolder is preferred.
if not exist "LevelGenerator.Cli\LevelGenerator.Cli.csproj" (
    echo [ERROR] BAT must be placed beside LevelGenerator.Cli in the project root.
    echo Current folder: "%CD%"
    pause
    exit /b 2
)
if not exist "IndependentSolver\IO\HoleShapes.json" (
    echo [ERROR] Missing IndependentSolver\IO\HoleShapes.json in "%CD%".
    pause
    exit /b 2
)
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet was not found. Install a compatible .NET SDK.
    pause
    exit /b 2
)

set "BATCHDIR="
set "AUTODETECT="
if not "%~1"=="" set "BATCHDIR=%~1"

rem Search using PowerShell WITHOUT any pipe operators inside CMD's FOR /F.
rem This avoids the escaped-pipe parsing issue in the previous BAT.
if not defined BATCHDIR if exist "generated\" (
    for /f "delims=" %%D in ('powershell -NoProfile -Command "$best=$null; foreach($f in (Get-ChildItem -LiteralPath 'generated' -Filter 'batch-summary.json' -File -Recurse -ErrorAction SilentlyContinue)){if(($null -eq $best) -or ($f.LastWriteTimeUtc -gt $best.LastWriteTimeUtc)){$best=$f}}; if($null -ne $best){[Console]::Out.WriteLine($best.DirectoryName)}"') do (
        if not defined BATCHDIR set "BATCHDIR=%%D"
    )
    if defined BATCHDIR set "AUTODETECT=1"
)

:CHECK_BATCH
if not defined BATCHDIR goto REQUEST_BATCH
set "BATCHDIR=%BATCHDIR:"=%"
rem Also accept a path to batch-summary.json rather than its containing folder.
for %%I in ("%BATCHDIR%") do if /I "%%~nxI"=="batch-summary.json" set "BATCHDIR=%%~dpI"
rem If an A/B parent folder is selected, choose its guided subfolder.
if exist "%BATCHDIR%\guided\batch-summary.json" set "BATCHDIR=%BATCHDIR%\guided"
if exist "%BATCHDIR%\batch-summary.json" goto FOUND_BATCH
rem If the levels folder was selected, use its parent batch directory.
for %%I in ("%BATCHDIR%\..") do if exist "%%~fI\batch-summary.json" set "BATCHDIR=%%~fI"
if exist "%BATCHDIR%\batch-summary.json" goto FOUND_BATCH

:REQUEST_BATCH
echo.
echo [WARN] Could not locate a valid batch folder automatically.
if defined BATCHDIR echo [WARN] Checked: "%BATCHDIR%"
echo.
echo Find a folder containing batch-summary.json, for example:
echo   generated\m41b-ab-YYYYMMDD-HHMMSS\guided
echo You can also drag the batch folder onto this BAT the next time.
echo.
set "BATCHDIR="
set /p "BATCHDIR=Paste full batch folder path (Enter to cancel): "
if not defined BATCHDIR (
    echo [ERROR] No batch selected. Nothing was modified.
    pause
    exit /b 2
)
goto CHECK_BATCH

:FOUND_BATCH
for %%I in ("%BATCHDIR%") do set "BATCHDIR=%%~fI"
echo.
echo [OK] Found batch: "%BATCHDIR%"
if not exist "%BATCHDIR%\levels\" (
    echo [INFO] No levels folder here. Verifier will read certified-draft files per seed.
)

set "STAMP="
for /f "delims=" %%D in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "STAMP=%%D"
if not defined STAMP set "STAMP=%RANDOM%"
set "OUTDIR=%BATCHDIR%\verification-m42-%STAMP%"
if exist "%OUTDIR%" set "OUTDIR=%OUTDIR%-%RANDOM%"

rem Bounded offline verification. Does NOT regenerate or change the level JSON files.
set "WORKERS=4"
set "SOLVE_MS=15000"
set "SEARCH_MS=5000"
set "MAX_PLANS=250"

echo ================================================================
echo CatDom M4.2 - Strong Bottleneck Verification
echo Input   : "%BATCHDIR%"
echo Output  : "%OUTDIR%"
echo Workers : %WORKERS%, Solve: %SOLVE_MS% ms, Search: %SEARCH_MS% ms
echo ================================================================
echo.
dotnet run --project "LevelGenerator.Cli" -- verify-batch "%BATCHDIR%" "%OUTDIR%" "IndependentSolver\IO\HoleShapes.json" %WORKERS% %SOLVE_MS% %SEARCH_MS% %MAX_PLANS%
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" echo [WARN] Verifier exited with code %EXITCODE%.

if exist "%OUTDIR%\verification-summary.json" (
    echo.
    echo [OK] Report: "%OUTDIR%\verification-summary.json"
    powershell -NoProfile -Command "$s=Get-Content -LiteralPath '%OUTDIR%\verification-summary.json' -Raw | ConvertFrom-Json; $s | Select-Object totalSeeds,eligible,verified,bypassFound,unknown,skipped,elapsedWallMs | Format-List"
    start "" "%OUTDIR%"
) else (
    echo [WARN] No verification summary was created. Check errors above.
)
echo.
echo Original batch and levels folder were left unchanged.
echo A missing bypass in bounded search does not prove a bottleneck mandatory.
echo.
pause
exit /b %EXITCODE%
