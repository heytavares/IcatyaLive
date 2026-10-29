[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Installer, [Parameter(Mandatory=$true)][string]$SourceDir, [Parameter(Mandatory=$true)][string]$TestRoot)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')+'\'
$destination=[IO.Path]::GetFullPath($TestRoot)
$setup=[IO.Path]::GetFullPath($Installer)
$source=[IO.Path]::GetFullPath($SourceDir).TrimEnd('\')
if (!$destination.StartsWith($root+'work\',[StringComparison]::OrdinalIgnoreCase)) { throw 'A verificacao so pode extrair na pasta work do projeto.' }
if (Test-Path -LiteralPath $destination) { throw 'Use uma pasta de teste nova.' }
$log=$destination+'.log'
# No visible window, restart, application launch/closure, shortcuts or uninstall registration.
$arguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/PORTABLE=1','/NOICONS','/NOCLOSEAPPLICATIONS','/NORESTARTAPPLICATIONS',('/DIR="'+$destination+'"'),('/LOG="'+$log+'"'))
$process=Start-Process -FilePath $setup -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Falha ao verificar instalador: $($process.ExitCode). Log: $log" }
$count=0
foreach($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
    $relative=$file.FullName.Substring($source.Length+1)
    $installed=Join-Path $destination $relative
    if (!(Test-Path -LiteralPath $installed -PathType Leaf)) { throw "Arquivo nao extraido: $relative" }
    if ((Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw "Arquivo extraido difere da origem: $relative" }
    $count++
}
if (Test-Path -LiteralPath (Join-Path $destination 'unins000.exe')) { throw 'Modo de teste registrou desinstalador.' }
$version=Get-Content (Join-Path $destination 'version.json') -Raw|ConvertFrom-Json
Write-Host "PASS instalador $($version.version): $count arquivos extraidos com SHA256 identico, sem abrir o app nem registrar instalacao."
