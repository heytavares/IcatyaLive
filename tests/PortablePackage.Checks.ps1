[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Package,[Parameter(Mandatory=$true)][string]$TestRoot)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')+'\'
$destination=[IO.Path]::GetFullPath($TestRoot)
if (!$destination.StartsWith($project+'work\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Extraia o teste somente na pasta work.' }
if (Test-Path -LiteralPath $destination) { throw 'Use uma pasta de teste nova.' }
$archive=[IO.Compression.ZipFile]::OpenRead($Package)
try {
    foreach($entry in $archive.Entries) {
        if (!$entry.FullName.StartsWith('iCatya/',[StringComparison]::Ordinal) -or $entry.FullName -match '(^|/)\.\.(/|$)|:|\\') { throw "Arquivo fora da pasta principal: $($entry.FullName)" }
    }
}finally{$archive.Dispose()}
[IO.Compression.ZipFile]::ExtractToDirectory($Package,$destination)
$top=@(Get-ChildItem -LiteralPath $destination -Force)
if ($top.Count -ne 1 -or !$top[0].PSIsContainer -or $top[0].Name -ne 'iCatya') { throw 'A extracao espalhou arquivos na pasta de destino.' }
foreach($required in @('Icatya.exe','Icatya.dll','version.json','native\ffmpeg\avcodec-62.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $top[0].FullName $required) -PathType Leaf)) { throw "Arquivo ausente: $required" }
}
Write-Host 'PASS ZIP portatil: uma unica pasta iCatya, sem arquivos soltos no destino.'
