@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
 echo Para executar pelos fontes, instale o SDK .NET 10 com desenvolvimento desktop.
 pause
 exit /b 1
)
echo Iniciando o iCatya pelos fontes atuais desta pasta...
dotnet run --project "src\Icatya\Icatya.csproj" --configuration Release
if errorlevel 1 pause
