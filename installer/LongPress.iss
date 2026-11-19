; Inno Setup 7 script for LongPress.
; Build with ..\build-installer.ps1, which publishes the app and passes /DAppVersion=<csproj Version>.

#ifndef AppVersion
  #error AppVersion is not defined. Build with build-installer.ps1, or pass /DAppVersion=x.y.z to ISCC.
#endif

#define AppName "LongPress"
#define AppExe "LongPress.exe"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"

[Setup]
; Never change AppId: it is how upgrades find and replace the installed version.
AppId={{44736018-044D-408E-96B0-E55D34DC8517}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=jmcurran
AppPublisherURL=https://github.com/jmcurran/LongPress
AppSupportURL=https://github.com/jmcurran/LongPress/issues
AppUpdatesURL=https://github.com/jmcurran/LongPress/releases
VersionInfoVersion={#AppVersion}

; Per-user install: no admin prompt.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\assets\LongPress.ico
UninstallDisplayName={#AppName}

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; The app holds this mutex while running (see Program.cs).
AppMutex=Local\LongPressAccents
CloseApplications=yes
RestartApplications=no

WizardStyle=modern dynamic
Compression=lzma2/max
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=LongPress-Setup-{#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start {#AppName} when Windows starts"; GroupDescription: "Startup:"

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Same value the tray menu's "Start with Windows" writes, so the two stay in step.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; Settings in %APPDATA%\LongPress are deliberately left alone on uninstall.

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Remove the startup entry however it was created (by setup or by the tray menu).
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, '{#RunKey}', '{#AppName}');
end;
