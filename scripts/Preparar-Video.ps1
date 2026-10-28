[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$url = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-10-27-18-38/ffmpeg-n8.1.3-24-g92611900bb-win64-lgpl-shared-8.1.zip'
$expected = '6c6167da57decb941a0466b60d95f7f51e4edd6b2d79c24dc4860c1c15701292'
$cache = Join-Path $root 'work\ffmpeg-8.1.3'
$archive = Join-Path $cache 'ffmpeg.zip'
$extract = Join-Path $cache 'unpacked'
$destination = Join-Path $root 'native\ffmpeg'
try {
    New-Item -ItemType Directory -Path $cache,$destination -Force | Out-Null
    if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) {
        Write-Host 'Baixando os componentes de video do MoabLive...'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $archive
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) {
        throw 'O arquivo baixado nao corresponde ao pacote verificado. Nenhum componente sera instalado.'
    }
    Expand-Archive -LiteralPath $archive -DestinationPath $extract -Force
    $package = @(Get-ChildItem -LiteralPath $extract -Directory)
    if ($package.Count -ne 1) { throw 'Estrutura do pacote inesperada.' }
    foreach ($name in @('avcodec-62.dll','avutil-60.dll','swresample-6.dll','swscale-9.dll')) {
        $source = Join-Path $package[0].FullName ('bin\' + $name)
        if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Componente ausente: $name" }
        Copy-Item -LiteralPath $source -Destination (Join-Path $destination $name) -Force
    }
    Copy-Item -LiteralPath (Join-Path $package[0].FullName 'LICENSE.txt') -Destination (Join-Path $destination 'LGPL-3.0.txt') -Force
    Invoke-WebRequest -UseBasicParsing -Uri 'https://www.gnu.org/licenses/gpl-3.0.txt' -OutFile (Join-Path $destination 'GPL-3.0.txt')
    @("FFmpeg n8.1.3-24-g92611900bb, BtbN build 2026-10-27.", "Binary package: $url", "SHA256: $expected", 'FFmpeg source: https://github.com/FFmpeg/FFmpeg/tree/92611900bb', 'Build recipes and dependencies: https://github.com/BtbN/FFmpeg-Builds/tree/autobuild-2026-10-27-18-38', 'This build uses LGPL-3.0-or-later. The shared libraries remain replaceable.', 'OpenH264: https://github.com/cisco/openh264 (BSD-2-Clause).', 'Only codec, scaling, resampling and utility libraries are included. No executables are run by this script.') | Set-Content -LiteralPath (Join-Path $destination 'ORIGIN.txt') -Encoding UTF8
    Write-Host 'Componentes de video preparados. Nenhuma configuracao do Windows foi alterada.' -ForegroundColor Green
    exit 0
} catch { Write-Host ('Falha: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }
