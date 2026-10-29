@echo off
setlocal
cd /d "%~dp0"
if exist "app\0.7.2\Icatya.exe" (
 start "iCatya" "app\0.7.2\Icatya.exe"
 exit /b 0
)
where dotnet >nul 2>&1
if errorlevel 1 (
 echo Para executar pelos fontes, instale o SDK .NET 10 com desenvolvimento desktop.
 pause
 exit /b 1
)
dotnet run --project "src\Icatya\Icatya.csproj" --configuration Release
if errorlevel 1 pause
