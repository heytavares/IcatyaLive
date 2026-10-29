@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Preparar-Instalador.ps1"
if errorlevel 1 goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Empacotar-Versao.ps1" -Rebuild
if errorlevel 1 goto failed
echo Instalador e ZIP portatil gerados na pasta releases.
pause
exit /b 0
:failed
echo Nao foi possivel gerar o instalador. Confira a mensagem acima.
pause
exit /b 1
