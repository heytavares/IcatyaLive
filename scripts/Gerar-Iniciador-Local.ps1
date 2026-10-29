[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src\Icatya\Icatya.csproj')
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Versao invalida.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Compilador do .NET Framework do Windows nao encontrado.' }
$working = Join-Path $projectRoot 'work\local-launcher'
[IO.Directory]::CreateDirectory($working) | Out-Null
$metadata = Join-Path $working 'AssemblyInfo.cs'
@"
using System.Reflection;
[assembly: AssemblyTitle("iCatya")]
[assembly: AssemblyProduct("iCatya")]
[assembly: AssemblyVersion("$version.0")]
[assembly: AssemblyFileVersion("$version.0")]
"@ | Set-Content -LiteralPath $metadata -Encoding UTF8
$built = Join-Path $working 'iCatya.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll "/win32icon:$(Join-Path $projectRoot 'src\Icatya\Assets\Icatya.ico')" "/out:$built" (Join-Path $PSScriptRoot 'Iniciador-Local.cs') $metadata
if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel compilar o iniciador.' }
$destination = Join-Path $projectRoot 'iCatya.exe'
if (Test-Path -LiteralPath $destination) {
    if (Get-Process -Name iCatya -ErrorAction SilentlyContinue | Where-Object Path -EQ $destination) { throw 'Feche o iniciador antes de substitui-lo.' }
    $backups = [IO.Path]::GetFullPath((Join-Path $projectRoot '..\Backups'))
    [IO.Directory]::CreateDirectory($backups) | Out-Null
    Copy-Item -LiteralPath $destination -Destination (Join-Path $backups ('iCatya-iniciador-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.exe'))
}
Copy-Item -LiteralPath $built -Destination $destination -Force
Write-Host "Pronto: $destination"
