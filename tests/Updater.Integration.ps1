[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$TestRoot)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$testBase=[IO.Path]::GetFullPath($TestRoot)
[IO.Directory]::CreateDirectory($testBase)|Out-Null
$installed=Join-Path $testBase 'installed';$package=Join-Path $testBase 'package'
foreach($folder in @($installed,$package)){[IO.Directory]::CreateDirectory($folder)|Out-Null}
foreach($file in @('Icatya.exe','Icatya.dll','Icatya.runtimeconfig.json')){
    [IO.File]::WriteAllText((Join-Path $installed $file),'old fixture')
    [IO.File]::WriteAllText((Join-Path $package $file),'new fixture')
}
[IO.File]::WriteAllText((Join-Path $installed 'version.json'),'{"version":"98.0.0"}')
[IO.File]::WriteAllText((Join-Path $package 'version.json'),'{"version":"99.0.0"}')
$script:packageFixture=Join-Path $testBase 'release.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($package,$script:packageFixture)
$script:fixtureHash=(Get-FileHash $script:packageFixture).Hash
Copy-Item (Join-Path $PSScriptRoot '../scripts/Atualizar-Icatya.ps1') (Join-Path $installed 'Atualizar-Icatya.ps1')
. (Join-Path $installed 'Atualizar-Icatya.ps1') -FunctionsOnly -ExpectedVersion '99.0.0'
# Fixtures only: no network, real process termination or window creation.
function Invoke-RestMethod {param($Uri,$Headers,$TimeoutSec)
    [PSCustomObject]@{draft=$false;prerelease=$false;tag_name='v99.0.0';assets=@([PSCustomObject]@{name='Icatya-99.0.0-Windows-x64.zip';state='uploaded';size=(Get-Item $script:packageFixture).Length;browser_download_url='https://github.com/heytavares/IcatyaLive/releases/download/v99.0.0/Icatya-99.0.0-Windows-x64.zip';digest=('sha256:'+$script:fixtureHash)})}
}
function Invoke-WebRequest {param($Uri,$OutFile,$Headers,$TimeoutSec,[switch]$UseBasicParsing) Copy-Item $script:packageFixture -Destination $OutFile}
function Get-Process {param($Name,$ErrorAction) @()}
function Start-Process {param($FilePath,$WorkingDirectory) $script:reopened=$FilePath}
$previousProfile=$env:ICATYA_DATA_DIRECTORY
$env:ICATYA_DATA_DIRECTORY=Join-Path $testBase 'profile'
try {
    Invoke-IcatyaUpdate
    if((Get-Content (Join-Path $installed 'version.json') -Raw|ConvertFrom-Json).version -ne '99.0.0'){throw 'Manifesto nao foi atualizado'}
    if((Get-Content (Join-Path $installed 'Icatya.dll') -Raw) -ne 'new fixture'){throw 'Arquivos nao foram substituidos'}
    if($script:reopened -ne (Join-Path $installed 'Icatya.exe')){throw 'Destino de reabertura incorreto'}
    $saved=@(Get-ChildItem (Join-Path $env:ICATYA_DATA_DIRECTORY 'UpdateBackups') -Filter Icatya.dll -Recurse)
    if($saved.Count -ne 1 -or [IO.File]::ReadAllText($saved[0].FullName) -ne 'old fixture'){throw 'Backup anterior nao foi preservado'}
    Write-Host 'PASS fluxo completo com Release simulada: download, hash, extracao, instalacao, backup persistente e reabertura simulada'
}finally{$env:ICATYA_DATA_DIRECTORY=$previousProfile}
