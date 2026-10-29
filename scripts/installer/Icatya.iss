#ifndef AppVersion
  #error AppVersion must be supplied by Gerar-Instalador.ps1
#endif
#ifndef AppSource
  #error AppSource must be the validated publish directory
#endif
#ifndef ReleaseDir
  #error ReleaseDir must be supplied
#endif
#define ProjectRoot AddBackslash(SourcePath) + "..\.."

[Setup]
AppId={{934BF5B9-5C14-42C7-A8DB-41D84AB8A64A}
AppName=iCatya
AppVersion={#AppVersion}
AppVerName=iCatya {#AppVersion}
AppPublisher=iCatya
AppPublisherURL=https://github.com/heytavares/IcatyaLive
AppSupportURL=https://github.com/heytavares/IcatyaLive/issues
AppUpdatesURL=https://github.com/heytavares/IcatyaLive/releases
DefaultDirName={localappdata}\Programs\iCatya
DefaultGroupName=iCatya
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupArchitecture=x64
MinVersion=10.0.19041
OutputDir={#ReleaseDir}
OutputBaseFilename=Icatya-{#AppVersion}-Instalador
SetupIconFile={#ProjectRoot}\src\Icatya\Assets\Icatya.ico
UninstallDisplayIcon={app}\Icatya.exe
Uninstallable=not PortableMode
UsePreviousAppDir=not PortableMode
DisableProgramGroupPage=yes
AllowNoIcons=yes
WizardStyle=modern dark includetitlebar
Compression=lzma2/normal
CompressionThreads=2
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter=Icatya.exe
RestartApplications=no
SetupMutex=iCatya-Installer
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Instalador do iCatya
VersionInfoProductName=iCatya

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; Flags: unchecked; Check: not PortableMode

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\iCatya"; Filename: "{app}\Icatya.exe"; WorkingDir: "{app}"; AppUserModelID: "IcatyaLive.iCatya"; Check: not PortableMode
Name: "{autodesktop}\iCatya"; Filename: "{app}\Icatya.exe"; WorkingDir: "{app}"; AppUserModelID: "IcatyaLive.iCatya"; Tasks: desktopicon; Check: not PortableMode

[Run]
Filename: "{app}\Icatya.exe"; Description: "Abrir iCatya"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
// Extraction-only mode for automated payload verification: no shortcuts or uninstall registry.
function PortableMode: Boolean;
begin
  Result := ExpandConstant('{param:PORTABLE|0}') = '1';
end;

procedure InitializeWizard;
begin
  if PortableMode then
    WizardForm.NoIconsCheck.Checked := True;
end;
