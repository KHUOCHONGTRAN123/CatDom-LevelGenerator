@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title CatDom Milestone 4.3 - Quality Guided Generation

set "REQUEST=LevelGenerator.Examples\RegionRelease.quality.request.json"
set "CATALOG=IndependentSolver\IO\HoleShapes.json"
set "COUNT=20"
set "WORKERS=10"
if not exist "LevelGenerator.Cli\LevelGenerator.Cli.csproj" (
  echo [ERROR] Run this BAT from the project root, beside LevelGenerator.Cli.
  goto :fail
)
if not exist "%REQUEST%" (
  echo [ERROR] Missing request: "%REQUEST%"
  goto :fail
)
if not exist "%CATALOG%" (
  echo [ERROR] Missing catalog: "%CATALOG%"
  goto :fail
)
where dotnet >nul 2>&1
if errorlevel 1 (
  echo [ERROR] .NET SDK is not available on PATH.
  goto :fail
)

REM IMPORTANT: leave the new batch output directory NONEXISTENT; the CLI owns it.
set "STAMP="
for /f %%T in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "STAMP=%%T"
if not defined STAMP set "STAMP=%RANDOM%"
set "OUT=generated\m43-region-quality-%STAMP%-%RANDOM%"
if exist "%OUT%" (
  echo [ERROR] Output already exists: "%OUT%"
  goto :fail
)

echo ================================================================
echo CatDom M4.3 - Quality Guided Generation (20 seeds, 10 workers)
echo Input : %REQUEST%
echo Output: %OUT%
echo ================================================================
dotnet run --project LevelGenerator.Cli -- batch "%REQUEST%" "%OUT%" "%CATALOG%" %COUNT% --workers %WORKERS%
if errorlevel 1 (
  echo [WARN] One or more workers had errors or generation was cancelled.
  echo [INFO] Inspect the partial output under: %OUT%
  goto :results
)
echo [OK] Batch finished.
:results
if exist "%OUT%\batch-summary.json" (
  echo [OK] Batch summary: "%OUT%\batch-summary.json"
  if exist "%OUT%\index.html" start "" "%OUT%\index.html"
  if exist "%OUT%\levels" explorer "%CD%\%OUT%\levels"
) else (
  echo [ERROR] No batch summary produced.
)
echo.
echo Levels: each certified seed produces Levelxxxxx.json in the levels folder.
echo Quality selection evidence is NOT a human quality rating or proof of mandatory bottleneck.
pause
exit /b 0
:fail
pause
exit /b 1
