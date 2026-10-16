@echo off
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
title CatDom M4.1b - Baseline vs Guided - 20 seeds

rem === Fixed benchmark settings ===
set "COUNT=20"
set "WORKERS=10"
set "PROJECT=LevelGenerator.Cli"
set "CATALOG=IndependentSolver\IO\HoleShapes.json"
set "CONFIG_A=LevelGenerator.Examples\RegionRelease.repair.request.json"
set "CONFIG_B=LevelGenerator.Examples\RegionRelease.guided.request.json"

echo.
echo ============================================================
echo CatDom M4.1b A/B benchmark: %COUNT% seeds per variant, %WORKERS% workers
 echo Baseline runs first; Guided runs second (not simultaneously).
echo ============================================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet was not found. Install the .NET SDK and try again.
    goto :failed
)
where powershell >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Windows PowerShell was not found.
    goto :failed
)
if not exist "%PROJECT%\LevelGenerator.Cli.csproj" (
    echo [ERROR] Project not found: %PROJECT%\LevelGenerator.Cli.csproj
    echo Put this .bat file in your project ROOT folder.
    goto :failed
)
if not exist "%CATALOG%" (
    echo [ERROR] Shape catalog not found: %CATALOG%
    goto :failed
)
if not exist "%CONFIG_A%" (
    echo [ERROR] Baseline config not found: %CONFIG_A%
    goto :failed
)
if not exist "%CONFIG_B%" (
    echo [ERROR] Guided config not found: %CONFIG_B%
    goto :failed
)

rem Generate a locale-independent name; never overwrite an earlier run.
set "STAMP="
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"`) do set "STAMP=%%I"
if not defined STAMP (
    echo [ERROR] Could not generate run timestamp.
    goto :failed
)
set "AB_ROOT=%CD%\generated\m41b-ab-%STAMP%"
set "AB_BASE=%AB_ROOT%\baseline"
set "AB_GUIDED=%AB_ROOT%\guided"
if exist "%AB_ROOT%" (
    echo [ERROR] Output directory already exists: "%AB_ROOT%"
    echo Retry in a second so a new timestamp is generated.
    goto :failed
)
mkdir "%AB_ROOT%" || goto :failed

echo Output root: "%AB_ROOT%"
echo.
echo [1/4] Running BASELINE...
call :RunVariant "BASELINE" "%CONFIG_A%" "%AB_BASE%"
if errorlevel 1 goto :failed

echo.
echo [2/4] Running GUIDED...
call :RunVariant "GUIDED" "%CONFIG_B%" "%AB_GUIDED%"
if errorlevel 1 goto :failed

echo.
echo [3/4] Comparing results...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $a=Get-Content -LiteralPath (Join-Path $env:AB_BASE 'batch-summary.json') -Raw | ConvertFrom-Json; $b=Get-Content -LiteralPath (Join-Path $env:AB_GUIDED 'batch-summary.json') -Raw | ConvertFrom-Json; $da=Get-Content -LiteralPath (Join-Path $env:AB_BASE 'diagnostics.json') -Raw | ConvertFrom-Json; $db=Get-Content -LiteralPath (Join-Path $env:AB_GUIDED 'diagnostics.json') -Raw | ConvertFrom-Json; $lines=@('CatDom M4.1b - 20-seed A/B benchmark','BASELINE vs GUIDED','-------------------------',('Completed seeds: {0}/{1} vs {2}/{3}' -f $a.rows.Count,$a.count,$b.rows.Count,$b.count),('Certified: {0}/{1} vs {2}/{3}' -f $a.certifiedCount,$a.count,$b.certifiedCount,$b.count),('Witnessed: {0} vs {1}' -f $a.witnessedCount,$b.witnessedCount),('Budget exhausted: {0} vs {1}' -f $da.budgetExhaustedCount,$db.budgetExhaustedCount),('Batch wall time (s): {0:N1} vs {1:N1}' -f ($a.elapsedWallMs/1000),($b.elapsedWallMs/1000)),('Attempts: {0} vs {1}' -f $da.totalAttempts,$db.totalAttempts),('Repairs proposed: {0} vs {1}' -f $da.totalRepairsProposed,$db.totalRepairsProposed),('Repair certifications: {0} vs {1}' -f $da.totalRepairsCertified,$db.totalRepairsCertified),('Solver calls: {0} vs {1}' -f $da.totalSolverCalls,$db.totalSolverCalls),('Run errors: {0} vs {1}' -f $a.errorCount,$b.errorCount),'','Notes: Wall time depends on concurrent PC load; inspect both diagnostics for failure breakdown.'); $lines | Set-Content -LiteralPath (Join-Path $env:AB_ROOT 'AB-comparison.txt') -Encoding UTF8; $lines | ForEach-Object { Write-Host $_ }"
if errorlevel 1 goto :failed

echo.
echo [4/4] Exporting certified levels into a test-only folder...
if not exist "%AB_GUIDED%\levels\" (
    echo [ERROR] Guided levels folder is missing. Install the patched LevelGenerator.Core.
    goto :failed
)
mkdir "%AB_ROOT%\levels" || goto :failed
if exist "%AB_GUIDED%\levels\Level*.json" (
    xcopy /Q /Y "%AB_GUIDED%\levels\Level*.json" "%AB_ROOT%\levels\" >nul
    if errorlevel 1 goto :failed
)
for /f %%C in ('dir /b /a-d "%AB_ROOT%\levels\Level*.json" 2^>nul ^| find /c /v ""') do set "EXPORTED=%%C"
echo [OK] Exported %EXPORTED% Guided certified levels to "%AB_ROOT%\levels"

echo.
echo Opening the A/B comparison and output folder...
start "" notepad.exe "%AB_ROOT%\AB-comparison.txt"
start "" explorer.exe "%AB_ROOT%"
echo.
echo [DONE] Outputs were saved in:
echo "%AB_ROOT%"
echo.
echo Local gameplay test levels: "%AB_ROOT%\levels"
echo.
echo Share the following files for analysis:
echo   baseline\diagnostics.json
echo   guided\diagnostics.json
echo   AB-comparison.txt
echo.
pause
exit /b 0

:RunVariant
echo --- %~1: 20 consecutive seeds, 10 workers ---
echo Request: "%~2"
echo Output:  "%~3"
dotnet run --project "%PROJECT%" -- batch "%~2" "%~3" "%CATALOG%" %COUNT% --workers %WORKERS%
if errorlevel 1 (
    echo [ERROR] %~1 batch failed; inspect output directory for partial reports.
    exit /b 1
)
echo Running offline diagnostics for %~1...
dotnet run --project "%PROJECT%" -- diagnose "%~3"
if errorlevel 1 (
    echo [ERROR] %~1 diagnostics failed.
    exit /b 1
)
exit /b 0

:failed
echo.
echo [STOPPED] The benchmark could not finish. Existing output was preserved.
echo Fix the error shown above and start the .bat again.
echo.
pause
exit /b 1
