@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Preparar-Video.ps1"
set "MOAB_RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %MOAB_RESULT%
