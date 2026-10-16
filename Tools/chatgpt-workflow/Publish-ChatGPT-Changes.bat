@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-ChatGPT-Changes.ps1" %*
set "workflow_exit=%ERRORLEVEL%"
if not "%workflow_exit%"=="0" echo Workflow failed with exit code %workflow_exit%.
pause
exit /b %workflow_exit%
