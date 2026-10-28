[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$TestRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\scripts\Atualizar-Icatya.ps1') -FunctionsOnly
$work = [IO.Path]::GetFullPath($TestRoot)
[IO.Directory]::CreateDirectory($work) | Out-Null

function Assert-Rejected([scriptblock]$Action, [string]$Label) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (!$rejected) { throw "Nao rejeitou: $Label" }
    Write-Host "PASS $Label"
}

$source = Join-Path $work 'source'
[IO.Directory]::CreateDirectory($source) | Out-Null
foreach ($file in @('Icatya.exe','Icatya.dll','Icatya.runtimeconfig.json')) { [IO.File]::WriteAllText((Join-Path $source $file), 'fixture-only') }
[IO.File]::WriteAllText((Join-Path $source 'version.json'), '{"version":"99.0.0"}')
$zip = Join-Path $work 'valid.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($source, $zip)
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
Expand-VerifiedUpdate $zip $hash '99.0.0' (Join-Path $work 'valid')
Write-Host 'PASS extracao de pacote completo com hash e versao corretos'
Assert-Rejected { Expand-VerifiedUpdate $zip ('0' * 64) '99.0.0' (Join-Path $work 'wrong-hash') } 'hash divergente'
Assert-Rejected { Expand-VerifiedUpdate $zip $hash '98.0.0' (Join-Path $work 'wrong-version') } 'versao interna divergente'
Assert-Rejected { Get-SafeUpdatePath $work '..\escape.txt' } 'caminho fora da pasta'
Assert-Rejected { Get-SafeUpdatePath $work 'C:\escape.txt' } 'caminho absoluto'

$badZip = Join-Path $work 'traversal.zip'
$archive = [IO.Compression.ZipFile]::Open($badZip, [IO.Compression.ZipArchiveMode]::Create)
try { $archive.CreateEntry('../escape.txt') | Out-Null } finally { $archive.Dispose() }
Assert-Rejected { Expand-VerifiedUpdate $badZip (Get-FileHash -LiteralPath $badZip).Hash '99.0.0' (Join-Path $work 'traversal') } 'travessia de diretorio no ZIP'
if (Test-Path -LiteralPath (Join-Path $work 'escape.txt')) { throw 'Extraiu arquivo antes de validar.' }

$target = Join-Path $work 'installed'
$staged = Join-Path $work 'staged'
$backup = Join-Path $work 'backup'
foreach ($path in @($target,$staged)) { [IO.Directory]::CreateDirectory($path) | Out-Null }
[IO.File]::WriteAllText((Join-Path $target 'a.txt'), 'old-a')
[IO.File]::WriteAllText((Join-Path $staged 'a.txt'), 'new-a')
Install-UpdateFiles $staged $target $backup
if ((Get-Content -LiteralPath (Join-Path $target 'a.txt') -Raw) -ne 'new-a') { throw 'Nao instalou arquivo novo.' }
if ((Get-Content -LiteralPath (Join-Path $backup 'a.txt') -Raw) -ne 'old-a') { throw 'Nao preservou a copia anterior.' }
Write-Host 'PASS aplicacao e backup da versao anterior'

[IO.File]::WriteAllText((Join-Path $target 'a.txt'), 'before-rollback')
[IO.File]::WriteAllText((Join-Path $target 'b.txt'), 'locked-original')
[IO.File]::WriteAllText((Join-Path $staged 'b.txt'), 'new-b')
$locked = [IO.File]::Open((Join-Path $target 'b.txt'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
try { Assert-Rejected { Install-UpdateFiles $staged $target (Join-Path $work 'rollback-backup') } 'arquivo ocupado interrompe instalacao' }
finally { $locked.Dispose() }
if ((Get-Content -LiteralPath (Join-Path $target 'a.txt') -Raw) -ne 'before-rollback') { throw 'Nao restaurou arquivo alterado antes da falha.' }
if ((Get-Content -LiteralPath (Join-Path $target 'b.txt') -Raw) -ne 'locked-original') { throw 'Alterou arquivo ocupado.' }
Write-Host 'PASS restauracao apos falha parcial'
Write-Host 'ALL UPDATER CHECKS PASSED'

