[CmdletBinding()]
param([string]$Version, [string]$SourceDir, [string]$CompilerPath)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$Version) { [xml]$project=Get-Content (Join-Path $root 'src\Icatya\Icatya.csproj');$Version=[string]$project.Project.PropertyGroup.Version }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Numero de versao invalido.' }
if (!$SourceDir) { $SourceDir=Join-Path $root ('app\'+$Version) }
$source=[IO.Path]::GetFullPath($SourceDir)
if (!$source.StartsWith($root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Use o pacote publicado dentro do projeto.' }
foreach($name in @('Icatya.exe','Icatya.dll','Icatya.runtimeconfig.json','version.json')) {
  if (!(Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) { throw "Pacote incompleto: $name" }
}
$manifest=Get-Content (Join-Path $source 'version.json') -Raw|ConvertFrom-Json
if ($manifest.version -ne $Version) { throw 'A versao interna difere da versao do instalador.' }
foreach($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
  $relative=$file.FullName.Substring($source.Length+1).Replace('\','/')
  if ($relative -match '(^|/)(work|src|tests|obj|bin|node_modules|\.wrangler)/|(^|/)(room\.json|profile\.json|ui\.json|client-id\.txt)$|\.(pdb|pfx|pem|key|log|tmp)$') { throw "Arquivo local indevido no pacote: $relative" }
  if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Links nao sao permitidos no pacote.' }
}
if (!$CompilerPath) { $CompilerPath=Join-Path $root 'work\tools\inno-7.1.1\ISCC.exe' }
if (!(Test-Path -LiteralPath $CompilerPath)) { throw 'Prepare o compilador executando scripts\Preparar-Instalador.ps1.' }
$output=Join-Path $root ('releases\'+$Version)
[IO.Directory]::CreateDirectory($output)|Out-Null
& $CompilerPath '/Qp' "/DAppVersion=$Version" "/DAppSource=$source" "/DReleaseDir=$output" (Join-Path $PSScriptRoot 'installer\Icatya.iss')
if ($LASTEXITCODE -ne 0) { throw 'O compilador nao concluiu o instalador.' }
$installer=Join-Path $output ("Icatya-$Version-Instalador.exe")
(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash|Set-Content -LiteralPath ($installer+'.sha256') -Encoding ascii
Write-Host "Instalador pronto: $installer"
