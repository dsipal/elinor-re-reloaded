; Inno Setup script for Elinor. Built by CI on every v* tag:
;   iscc /DAppVersion=1.0.0 /DSourceExe=..\Elinor\bin\publish\Elinor.exe installer\Elinor.iss
; Output: installer\Output\Elinor-Setup-<version>.exe

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
; Must match App.AppUserModelId in Elinor/App.xaml.cs.
#define AppUserModelId "dsipal.Elinor"

#ifndef SourceExe
  #define SourceExe "..\Elinor\bin\publish\Elinor.exe"
#endif

[Setup]
; Never change AppId: it's how upgrades and the uninstaller find an existing install.
AppId={{6E0C5A8B-3F6D-4B9E-9C1A-2D7F4E8B1A53}
AppName=Elinor
AppVersion={#AppVersion}
AppVerName=Elinor {#AppVersion}
AppPublisher=dsipal
AppPublisherURL=https://github.com/dsipal/elinor-re-reloaded
AppSupportURL=https://github.com/dsipal/elinor-re-reloaded/issues
AppUpdatesURL=https://github.com/dsipal/elinor-re-reloaded/releases
VersionInfoVersion={#AppVersion}

; Per-user install by default (no admin prompt); the user can choose "all users" instead.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\Elinor
DisableProgramGroupPage=yes

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

LicenseFile=..\Elinor\license.rtf
SetupIconFile=..\Elinor\mainicon.ico
UninstallDisplayIcon={app}\Elinor.exe
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=Elinor-Setup-{#AppVersion}

; Makes Explorer refresh at the end of setup. Without it the Start menu app list can take a
; long time to notice the new shortcut.
ChangesAssociations=yes

; Close a running Elinor during upgrades.
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Elinor"; Filename: "{app}\Elinor.exe"; AppUserModelID: "{#AppUserModelId}"
Name: "{autodesktop}\Elinor"; Filename: "{app}\Elinor.exe"; AppUserModelID: "{#AppUserModelId}"; Tasks: desktopicon

[Run]
Filename: "{app}\Elinor.exe"; Description: "{cm:LaunchProgram,Elinor}"; Flags: nowait postinstall skipifsilent

; Profiles and settings in %APPDATA%\Elinor are deliberately kept on uninstall.
