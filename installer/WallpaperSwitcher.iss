; Inno Setup script for the Wallpaper Switcher Windows installer.
;
; Built by scripts/publish-windows.ps1 and by the release workflow. Compile with:
;   iscc /DAppVersion=1.0.0 /DSourceDir=..\artifacts\publish\win-x64 WallpaperSwitcher.iss
;
; This is a per-user install: it lands in %LOCALAPPDATA%\Programs and never shows
; a UAC prompt. A wallpaper switcher has no business asking for administrator
; rights, and everything it touches -- settings, logs, the autostart entry -- is
; per-user anyway.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif

#define AppName "Wallpaper Switcher"
#define AppExeName "WallpaperSwitcher.exe"
#define AppPublisher "msk-one"
#define AppUrl "https://github.com/msk-one/WallpaperSwitcher"

[Setup]
; Never change AppId: it is what lets a new version upgrade in place rather than
; installing alongside the old one.
AppId={{8B1F3C42-6A5D-4E97-9C3B-2D7E4A8F1B60}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}

DefaultDirName={localappdata}\Programs\WallpaperSwitcher
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto

PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputDir=..\artifacts\installer
OutputBaseFilename=WallpaperSwitcher-{#AppVersion}-win-x64-Setup
SetupIconFile=..\WallpaperSwitcher.Desktop\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE

; Shuts down a running instance so an upgrade does not fail on a locked file.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; The app writes this itself when "Start at login" is ticked. The installer only
; cleans it up, so uninstalling cannot leave an autostart entry pointing at an
; executable that no longer exists. Deliberately no [Tasks] entry to create it:
; two writers to one value is how the original round-trip bug happened.
; No deletevalue here: that runs at install time and would silently switch off
; the user's autostart every time they upgraded.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueName: "WallpaperSwitcher"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
// Settings and logs are left in place on uninstall so a reinstall keeps the
// user's folder and their Day/Night assignments. Removing them is offered, not
// assumed.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\WallpaperSwitcher');
    if DirExists(DataDir) then
    begin
      if SuppressibleMsgBox(
           'Also remove your Wallpaper Switcher settings and logs?' + #13#10 + #13#10 +
           'Choose No to keep your wallpaper folder and Day/Night assignments for a future reinstall.',
           mbConfirmation, MB_YESNO, IDNO) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
