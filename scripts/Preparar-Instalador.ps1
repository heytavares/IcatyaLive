[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$download=Join-Path $root 'work\tools\inno-download'
$destination=Join-Path $root 'work\tools\inno-7.1.1'
if (Test-Path -LiteralPath (Join-Path $destination 'ISCC.exe')) { Write-Host 'Compilador ja preparado.'; return }
[IO.Directory]::CreateDirectory($download)|Out-Null
$file=Join-Path $download 'innosetup-7.1.1-x64.exe'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe' -OutFile $file -TimeoutSec 120
$hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
if ($hash -ne '8A5A6711C3766A6E5FB74F280BA0E3551E719CE94538D901FDBD1F8F9920B3F0') { throw 'O compilador baixado nao corresponde a versao verificada.' }
$signature=Get-AuthenticodeSignature -LiteralPath $file
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.') { throw 'A assinatura do compilador nao confere.' }
# Official portable mode: no global compiler install, associations, shortcuts or uninstall entry.
$arguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/CURRENTUSER','/PORTABLE=1','/NOICONS',('/DIR="'+$destination+'"'),('/LOG="'+(Join-Path $download 'portable-extract.log')+'"'))
$process=Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $destination 'ISCC.exe'))) { throw 'Nao foi possivel preparar o compilador.' }
Write-Host 'Compilador oficial preparado na pasta work do projeto.'
