[CmdletBinding()]
param([string]$CurrentVersion)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
if (!$CurrentVersion) { [xml]$project=Get-Content (Join-Path $root 'src\Icatya\Icatya.csproj');$CurrentVersion=[string]$project.Project.PropertyGroup.Version }
if ($CurrentVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Versao invalida.' }
$backupRoot=[IO.Path]::GetFullPath((Join-Path $root '..\Backups')).TrimEnd('\')+'\'
[IO.Directory]::CreateDirectory($backupRoot)|Out-Null
$saved=Join-Path $backupRoot ('Icatya-organizacao-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
$active=@(Get-Process -Name Icatya,MoabLive -ErrorAction SilentlyContinue|ForEach-Object { try { $_.Path } catch { } })
$moves=New-Object 'Collections.Generic.List[string]'
foreach($group in @('app','releases')) {
    $base=[IO.Path]::GetFullPath((Join-Path $root $group)).TrimEnd('\')+'\'
    foreach($item in @(Get-ChildItem -LiteralPath $base -Force)) {
        if ($item.PSIsContainer -and $item.Name -eq $CurrentVersion) { continue }
        if ($group -eq 'releases' -and !$item.PSIsContainer -and $item.Name -eq 'LEIA-ME.txt') { continue }
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { Write-Warning "Link preservado sem mover: $($item.Name)"; continue }
        $source=[IO.Path]::GetFullPath($item.FullName)
        $destination=[IO.Path]::GetFullPath((Join-Path (Join-Path $saved $group) $item.Name))
        if (!$source.StartsWith($base,[StringComparison]::OrdinalIgnoreCase) -or !$destination.StartsWith($backupRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Origem/destino de arquivo antigo fora dos diretorios permitidos.' }
        if ($item.PSIsContainer -and @($active|Where-Object { $_ -and $_.StartsWith($source.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) }).Count -gt 0) {
            Write-Warning "Versao em uso preservada na pasta atual: $($item.Name)"; continue
        }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null
        if (Test-Path -LiteralPath $destination) { throw 'Destino de backup ja existente.' }
        try { Move-Item -LiteralPath $source -Destination $destination }
        catch [IO.IOException] {
            Write-Warning "Arquivo/pasta em uso preservado: $source. Execute a organizacao novamente depois de fechar o programa que o utiliza."
            continue
        }
        $moves.Add("$source -> $destination")
    }
}
if ($moves.Count -gt 0) {
    $moves|Set-Content -LiteralPath (Join-Path $saved 'MAPA-DAS-PASTAS.txt') -Encoding utf8
    Write-Host "$($moves.Count) pastas/arquivos antigos preservados em: $saved"
} else { Write-Host 'Pastas atuais ja organizadas.' }
@"
VERSAO ATUAL: $CurrentVersion

Abra a pasta $CurrentVersion para encontrar o instalador e o ZIP portatil.
Para os amigos, envie o instalador ou o ZIP Portatil.
O ZIP Portatil extrai uma unica pasta iCatya, sem arquivos soltos em Downloads.
Pacotes tecnicos do atualizador ficam separados em $CurrentVersion\atualizacao.
Versoes anteriores ficam em Backups, ao lado da pasta principal do projeto.
"@ | Set-Content -LiteralPath (Join-Path $root 'releases\LEIA-ME.txt') -Encoding utf8
