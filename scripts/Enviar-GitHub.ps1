[CmdletBinding()]
param(
    [string]$Message = ("Atualiza Icatya - " + (Get-Date -Format 'yyyy-MM-dd HH:mm')),
    [switch]$VerifyOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$expectedRemote = 'https://github.com/heytavares/IcatyaLive.git'

function Invoke-Git {
    param([string[]]$GitArguments)
    & git -C $projectRoot @GitArguments
    if ($LASTEXITCODE -ne 0) { throw ('O Git nao concluiu: ' + ($GitArguments[0])) }
}

function Get-ProjectPaths {
    $candidates = @(
        'src', 'tests', 'docs', 'scripts', '.github', '.gitignore', '.gitattributes',
        '.editorconfig', 'README.md', 'AGENTS.md', 'MoabLive.slnx', 'icatyalive.slnx', 'Iniciar MoabLive.cmd', 'Iniciar Icatya.cmd', 'Atualizar Icatya.cmd',
        'Enviar para GitHub.cmd', 'Preparar video.cmd', 'Gerar instalador.cmd', 'LICENSE', 'LICENSE.md'
    )
    @($candidates | Where-Object {
        (Test-Path -LiteralPath (Join-Path $projectRoot $_)) -or
        @(& git -C $projectRoot ls-files -- $_).Count -gt 0
    })
}

function Assert-SendableFiles {
    param([string[]]$Files)
    foreach ($file in $Files) {
        $normalized = $file.Replace('\', '/')
        $allowed = $normalized -match '^(src|tests|docs|scripts|\.github)/' -or
            $normalized -in @('.gitignore', '.gitattributes', '.editorconfig', 'README.md', 'AGENTS.md',
                'MoabLive.slnx', 'icatyalive.slnx', 'Iniciar MoabLive.cmd', 'Iniciar Icatya.cmd', 'Atualizar Icatya.cmd', 'Enviar para GitHub.cmd', 'Preparar video.cmd', 'Gerar instalador.cmd', 'LICENSE', 'LICENSE.md')
        if (!$allowed) { throw "Arquivo fora das pastas do projeto preparado para envio: $file" }
        if ($normalized -match '(?i)(^|/)(bin|obj|\.vs|app|releases|TestResults|node_modules|\.wrangler)/|(^|/)(\.env($|\.)|\.dev\.vars($|\.)|credentials\.json$|secrets\.json$)|\.(exe|dll|pdb|zip|pfx|p12|pem|key|snk|user|log|tmp|bak|dmp)$') {
            throw "Arquivo local ou privado preparado para envio: $file. Retire-o do envio antes de continuar."
        }
        $fullPath = Join-Path $projectRoot $file
        if ((Test-Path -LiteralPath $fullPath -PathType Leaf) -and (Get-Item -LiteralPath $fullPath).Length -gt 10MB) {
            throw "Arquivo acima de 10 MB: $file. Pacotes e executaveis devem ficar em releases ou app."
        }
    }
}

try {
    if (!(Get-Command git -ErrorAction SilentlyContinue)) {
        throw 'Instale o Git for Windows e execute este arquivo novamente.'
    }
    if (!(Test-Path -LiteralPath (Join-Path $projectRoot 'src\Icatya\Icatya.csproj'))) {
        throw 'Projeto principal nao encontrado. Mantenha este script dentro da pasta scripts do Icatya.'
    }
    if (!(Test-Path -LiteralPath (Join-Path $projectRoot '.git'))) {
        if ($VerifyOnly) { throw 'Repositorio Git ainda nao inicializado.' }
        Invoke-Git -GitArguments @('init', '-b', 'main')
    }
    $gitRoot = (& git -C $projectRoot rev-parse --show-toplevel)
    if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($gitRoot.Trim()) -ne $projectRoot) {
        throw 'O repositorio precisa estar na propria pasta do projeto Icatya, separado de outros projetos.'
    }
    $paths = Get-ProjectPaths
    if ($VerifyOnly) {
        $files = @(& git -C $projectRoot -c core.quotepath=false ls-files --cached --others --exclude-standard -- @paths | Sort-Object -Unique)
        if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel listar os arquivos.' }
        Assert-SendableFiles -Files $files
        Write-Host 'VERIFICACAO: somente estes arquivos do projeto seriam enviados:' -ForegroundColor Cyan
        $files | ForEach-Object { Write-Host ('  ' + $_) }
        Write-Host ("Total: {0} arquivos. Nenhum commit ou envio foi realizado." -f $files.Count) -ForegroundColor Green
        exit 0
    }

    $remotes = @(& git -C $projectRoot remote)
    if ('origin' -notin $remotes) { Invoke-Git -GitArguments @('remote', 'add', 'origin', $expectedRemote) }
    $remote = & git -C $projectRoot remote get-url origin
    if ($LASTEXITCODE -ne 0 -or $remote.Trim() -notin @($expectedRemote, 'https://github.com/heytavares/MoabLive.git', 'https://github.com/heytavares/MoabLive', 'git@github.com:heytavares/MoabLive.git', 'https://github.com/heytavares/icatyalive.git', 'https://github.com/heytavares/icatyalive', 'git@github.com:heytavares/icatyalive.git')) {
        throw 'O origin aponta para outro repositorio. O envio foi interrompido para evitar publicar no lugar errado.'
    }
    $branch = & git -C $projectRoot branch --show-current
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branch)) {
        throw 'O Git nao esta em uma branch normal. Resolva esse estado antes de enviar.'
    }
    if (@(& git -C $projectRoot ls-files --unmerged).Count -gt 0) {
        throw 'Existem conflitos de arquivos. Resolva-os antes de enviar.'
    }
    foreach ($setting in @(@('user.name', 'Seu nome para os commits'), @('user.email', 'Seu e-mail para os commits (pode ser o noreply do GitHub)'))) {
        $value = & git -C $projectRoot config --get $setting[0]
        if ([string]::IsNullOrWhiteSpace($value)) {
            $value = Read-Host $setting[1]
            if ([string]::IsNullOrWhiteSpace($value)) { throw 'Nome e e-mail sao necessarios para criar o commit.' }
            Invoke-Git -GitArguments @('config', '--local', $setting[0], $value)
        }
    }

    Write-Host 'Conferindo acesso ao repositorio do GitHub...' -ForegroundColor Cyan
    Invoke-Git -GitArguments @('fetch', 'origin')
    $staged = @(& git -C $projectRoot -c core.quotepath=false diff --cached --name-only --diff-filter=ACMR)
    Assert-SendableFiles -Files $staged
    $candidates = @(& git -C $projectRoot -c core.quotepath=false ls-files --cached --others --exclude-standard -- @paths | Sort-Object -Unique)
    Assert-SendableFiles -Files $candidates
    Invoke-Git -GitArguments (@('add', '--all', '--') + $paths)
    & git -C $projectRoot diff --cached --quiet --exit-code
    $difference = $LASTEXITCODE
    if ($difference -eq 1) {
        Invoke-Git -GitArguments @('commit', '-m', $Message)
    } elseif ($difference -ne 0) {
        throw 'Nao foi possivel conferir as alteracoes.'
    } else {
        Write-Host 'Nenhuma alteracao nova; verificando commits que ainda precisam ser enviados.'
    }
    & git -C $projectRoot rev-parse --verify HEAD *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Nao existe um commit para enviar.' }
    Write-Host 'Enviando para o repositorio configurado no Git...' -ForegroundColor Cyan
    & git -C $projectRoot push --set-upstream origin $branch
    if ($LASTEXITCODE -ne 0) {
        throw 'O GitHub recusou o envio. Confira a autenticacao e se existem alteracoes remotas. Nenhum historico remoto foi sobrescrito; os arquivos e o commit local foram preservados.'
    }
    Write-Host 'Icatya enviado ao GitHub com sucesso.' -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ''
    Write-Host ('Envio interrompido: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'Voce pode executar novamente depois de corrigir o problema.'
    exit 1
}
