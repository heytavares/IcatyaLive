@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Enviar-GitHub.ps1" %*
set "MOAB_RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %MOAB_RESULT%
