@echo off
setlocal
cd /d "%~dp0"
if exist "app\0.3.1\MoabLive.exe" (
 start "MoabLive" "app\0.3.1\MoabLive.exe"
 exit /b 0
)
where dotnet >nul 2>&1
if errorlevel 1 (
 echo Para executar pelos fontes, instale o SDK .NET 10 com desenvolvimento desktop.
 pause
 exit /b 1
)
dotnet run --project "src\MoabLive\MoabLive.csproj" --configuration Release
if errorlevel 1 pause


