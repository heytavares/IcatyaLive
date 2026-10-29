[CmdletBinding()]
param([string]$Version, [switch]$Rebuild)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$Version) { [xml]$project=Get-Content (Join-Path $root 'src\Icatya\Icatya.csproj');$Version=[string]$project.Project.PropertyGroup.Version }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Numero de versao invalido.' }
$source=Join-Path $root ('app\'+$Version)
$output=Join-Path $root ('releases\'+$Version)
if (!(Test-Path -LiteralPath (Join-Path $source 'Icatya.exe'))) { throw 'Publique o aplicativo antes de empacotar.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Atualizar-Icatya.ps1') -Destination (Join-Path $source 'Atualizar-Icatya.ps1') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Atualizar-Icone.ps1') -Destination (Join-Path $source 'Atualizar-Icone.ps1') -Force
@'
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Atualizar-Icatya.ps1"
pause
'@ | Set-Content -LiteralPath (Join-Path $source 'Atualizar Icatya.cmd') -Encoding ascii
@{version=$Version} | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $source 'version.json') -Encoding utf8
if (Test-Path -LiteralPath $output) {
    if (!$Rebuild) { throw 'A pasta de pacotes desta versao ja existe. Use -Rebuild para preservar os pacotes anteriores em Backups e recriar.' }
    $backupRoot=[IO.Path]::GetFullPath((Join-Path $root '..\Backups')).TrimEnd('\')+'\'
    [IO.Directory]::CreateDirectory($backupRoot)|Out-Null
    $saved=Join-Path $backupRoot ("Icatya-$Version-pacotes-"+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
    $absoluteOutput=[IO.Path]::GetFullPath($output)
    if (!$absoluteOutput.StartsWith($root.TrimEnd('\')+'\releases\',[StringComparison]::OrdinalIgnoreCase) -or !$saved.StartsWith($backupRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Caminho de backup invalido.' }
    Move-Item -LiteralPath $absoluteOutput -Destination $saved
}
# The installer builder validates the publish source before any ZIP is created.
& (Join-Path $PSScriptRoot 'Gerar-Instalador.ps1') -Version $Version -SourceDir $source
$updateDir=Join-Path $output 'atualizacao'
[IO.Directory]::CreateDirectory($updateDir)|Out-Null
$updateZip=Join-Path $updateDir ("Icatya-$Version-Windows-x64.zip")
[IO.Compression.ZipFile]::CreateFromDirectory($source,$updateZip,[IO.Compression.CompressionLevel]::Optimal,$false)
$portable=Join-Path $output ("Icatya-$Version-Portatil.zip")
$archive=[IO.Compression.ZipFile]::Open($portable,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
        $relative=$file.FullName.Substring($source.Length+1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,('iCatya/'+$relative),[IO.Compression.CompressionLevel]::Optimal)|Out-Null
    }
} finally { $archive.Dispose() }
foreach($zip in @($portable,$updateZip)) { (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash|Set-Content -LiteralPath ($zip+'.sha256') -Encoding ascii }
@"
PARA ENVIAR AOS AMIGOS

Opcao principal: Icatya-$Version-Instalador.exe
Alternativa sem instalar: Icatya-$Version-Portatil.zip
O ZIP portatil extrai somente uma pasta chamada iCatya.

A pasta atualizacao e tecnica: seu ZIP deve acompanhar a Release do GitHub
para manter os atualizadores antigos funcionando. Nao e a opcao para extrair em Downloads.

Os arquivos .sha256 servem para conferir a integridade dos downloads.
"@ | Set-Content -LiteralPath (Join-Path $output 'COMO-ENVIAR.txt') -Encoding utf8
Write-Host "Pacotes prontos em: $output"
& (Join-Path $PSScriptRoot 'Organizar-Pastas.ps1') -CurrentVersion $Version
