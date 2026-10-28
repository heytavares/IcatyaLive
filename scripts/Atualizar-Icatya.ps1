[CmdletBinding()]
param([switch]$FunctionsOnly)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-SafeUpdatePath([string]$Root, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or $Relative -match '(^[/\\])|:|(^|[/\\])\.\.([/\\]|$)') { throw 'Caminho invalido no pacote.' }
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $path = [IO.Path]::GetFullPath((Join-Path $base $Relative))
    if (!$path.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { throw 'Arquivo fora da pasta do aplicativo.' }
    return $path
}

function Expand-VerifiedUpdate([string]$Package, [string]$Hash, [string]$Version, [string]$Destination) {
    if ($Hash -notmatch '^[a-fA-F0-9]{64}$' -or (Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash -ne $Hash) { throw 'O arquivo baixado nao passou na verificacao SHA256.' }
    $archive = [IO.Compression.ZipFile]::OpenRead($Package)
    try {
        $seen = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        [long]$total = 0
        if ($archive.Entries.Count -gt 5000) { throw 'Pacote com arquivos demais.' }
        foreach ($entry in $archive.Entries) {
            $path = Get-SafeUpdatePath $Destination $entry.FullName
            if (!$seen.Add($path)) { throw 'Arquivo duplicado no pacote.' }
            if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'Links nao sao aceitos no pacote.' }
            $total += $entry.Length
            if ($total -gt 1GB) { throw 'Pacote excede o tamanho permitido.' }
        }
        foreach ($required in @('Icatya.exe', 'Icatya.dll', 'Icatya.runtimeconfig.json', 'version.json')) {
            if (!$seen.Contains((Get-SafeUpdatePath $Destination $required))) { throw "Pacote incompleto: $required" }
        }
    }
    finally { $archive.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($Package, $Destination)
    $manifest = Get-Content -LiteralPath (Join-Path $Destination 'version.json') -Raw | ConvertFrom-Json
    if ($manifest.version -ne $Version) { throw 'A versao interna difere da versao publicada.' }
}

function Install-UpdateFiles([string]$Staged, [string]$Target, [string]$Backup) {
    # Back up every affected file before changing the running installation.
    $files = @(Get-ChildItem -LiteralPath $Staged -File -Recurse)
    $changed = New-Object 'Collections.Generic.List[string]'
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($Staged.TrimEnd('\').Length + 1)
        $destination = Get-SafeUpdatePath $Target $relative
        if (Test-Path -LiteralPath $destination -PathType Leaf) {
            $saved = Get-SafeUpdatePath $Backup $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
            [IO.File]::Copy($destination, $saved, $true)
        }
    }
    try {
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($Staged.TrimEnd('\').Length + 1)
            $destination = Get-SafeUpdatePath $Target $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            # Record before copying so a partially written file can also be restored.
            $changed.Add($relative)
            [IO.File]::Copy($file.FullName, $destination, $true)
        }
    }
    catch {
        $reason = $_.Exception.Message
        $restoreFailed = $false
        foreach ($relative in $changed) {
            $destination = Get-SafeUpdatePath $Target $relative
            $saved = Get-SafeUpdatePath $Backup $relative
            try {
                if (Test-Path -LiteralPath $saved -PathType Leaf) { [IO.File]::Copy($saved, $destination, $true) }
                elseif (Test-Path -LiteralPath $destination -PathType Leaf) { Remove-Item -LiteralPath $destination -Force }
            }
            catch { $restoreFailed = $true }
        }
        if ($restoreFailed) { throw "Atualizacao interrompida. Copia anterior preservada em: $Backup. Motivo: $reason" }
        throw "Atualizacao interrompida; a versao anterior foi restaurada. Motivo: $reason"
    }
}

function Invoke-IcatyaUpdate {
    $target = [IO.Path]::GetFullPath($PSScriptRoot)
    $executable = Join-Path $target 'Icatya.exe'
    $versionFile = Join-Path $target 'version.json'
    if (!(Test-Path -LiteralPath $executable) -or !(Test-Path -LiteralPath $versionFile)) { throw 'Execute o atualizador na pasta do pacote pronto do Icatya.' }
    $local = Get-Content -LiteralPath $versionFile -Raw | ConvertFrom-Json
    [version]$installed = $local.version
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $headers = @{ 'User-Agent' = 'Icatya-Updater'; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
    Write-Host 'Procurando atualizacao no GitHub...'
    try { $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/heytavares/IcatyaLive/releases/latest' -Headers $headers -TimeoutSec 30 }
    catch { throw 'Nao foi possivel consultar uma versao publica no GitHub. Confira a internet e se o autor ja publicou uma Release.' }
    if ($release.draft -or $release.prerelease -or $release.tag_name -notmatch '^v?(\d+\.\d+\.\d+)$') { throw 'Versao publicada invalida.' }
    $version = $Matches[1]
    if ([version]$version -le $installed) { Write-Host "Voce ja esta atualizado: $installed" -ForegroundColor Green; return }
    $name = "Icatya-$version-Windows-x64.zip"
    $assets = @($release.assets | Where-Object { $_.name -eq $name -and $_.state -eq 'uploaded' })
    if ($assets.Count -ne 1) { throw "A Release nao contem o pacote $name." }
    $asset = $assets[0]
    if ($asset.size -gt 512MB -or $asset.browser_download_url -notmatch '^https://github\.com/heytavares/(MoabLive|icatyalive)/releases/download/') { throw 'Origem ou tamanho do pacote invalido.' }
    if ($asset.digest -notmatch '^sha256:([a-fA-F0-9]{64})$') { throw 'O GitHub ainda nao disponibilizou a verificacao do pacote. Tente mais tarde.' }
    $hash = $Matches[1]
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $work = Join-Path $temporaryRoot ('icatya-update-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($work) | Out-Null
    $completed = $false
    try {
        $package = Join-Path $work 'update.zip'
        $staged = Join-Path $work 'staged'
        $backup = Join-Path $work 'backup'
        Write-Host "Baixando Icatya $version..."
        Invoke-WebRequest -UseBasicParsing -Uri $asset.browser_download_url -OutFile $package -Headers @{ 'User-Agent' = 'Icatya-Updater' } -TimeoutSec 600
        if ((Get-Item -LiteralPath $package).Length -gt 512MB) { throw 'Download excede o tamanho permitido.' }
        Expand-VerifiedUpdate $package $hash $version $staged
        Write-Host 'Arquivo verificado. Fechando o Icatya para aplicar...'
        foreach ($process in @(Get-Process -Name Icatya -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable })) {
            if (!$process.CloseMainWindow() -or !$process.WaitForExit(15000)) { throw 'Feche o Icatya e execute o atualizador novamente. Nenhum arquivo foi instalado.' }
        }
        Install-UpdateFiles $staged $target $backup
        $completed = $true
        Write-Host "Icatya $version atualizado!" -ForegroundColor Green
        Start-Process -FilePath $executable -WorkingDirectory $target
    }
    finally {
        # Only remove the unique temporary directory created by this invocation.
        if ($completed -and $work.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and
            [IO.Path]::GetFileName($work) -match '^icatya-update-[a-f0-9]{32}$') { Remove-Item -LiteralPath $work -Recurse -Force }
        elseif (Test-Path -LiteralPath $work) { Write-Host "Arquivos de verificacao/recuperacao preservados em: $work" }
    }
}

if (!$FunctionsOnly) {
    try { Invoke-IcatyaUpdate }
    catch { Write-Host $_.Exception.Message -ForegroundColor Red; exit 1 }
}
