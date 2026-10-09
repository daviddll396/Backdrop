#ifndef AppVersion
  #error AppVersion must be supplied on the ISCC command line.
#endif
#ifndef PayloadRoot
  #error PayloadRoot must be supplied on the ISCC command line.
#endif
#ifndef OutputDir
  #error OutputDir must be supplied on the ISCC command line.
#endif
#ifndef OutputName
  #error OutputName must be supplied on the ISCC command line.
#endif

#define AppId "Backdrop.ConsumerInstaller"
#define AppName "Backdrop"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Backdrop
DefaultDirName={localappdata}\Programs\Backdrop
DefaultGroupName=Backdrop
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
OutputDir={#OutputDir}
OutputBaseFilename={#OutputName}
UninstallDisplayName=Backdrop
UninstallDisplayIcon={app}\Backdrop.exe
CloseApplications=yes
CloseApplicationsFilter=Backdrop.exe,Backdrop.Shell.dll
RestartApplications=no
ChangesAssociations=yes
SetupLogging=yes
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PayloadRoot}\Backdrop.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\Backdrop.Shell.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\scripts\classic-shell.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Backdrop"; Filename: "{app}\Backdrop.exe"
Name: "{autoprograms}\Uninstall Backdrop"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\Backdrop.exe"; Description: "Launch Backdrop"; Flags: postinstall nowait skipifsilent

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\Backdrop.ConsumerInstaller_is1';

var
  PreviousInstallPath: String;
  UpgradeShellWasUnregistered: Boolean;
  ShellRegistrationSucceeded: Boolean;
  UninstallShellWasUnregistered: Boolean;
  UninstallInstallPath: String;

function RunClassicShellAction(const InstallPath, Action: String; var ErrorText: String): Boolean;
var
  PowerShellPath, ScriptPath, Parameters: String;
  ExitCode: Integer;
begin
  ScriptPath := AddBackslash(InstallPath) + 'scripts\classic-shell.ps1';
  if not FileExists(ScriptPath) then
  begin
    ErrorText := 'The Explorer integration script is missing: ' + ScriptPath;
    Result := False;
    exit;
  end;

  PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Parameters := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ScriptPath + '" -Action ' + Action;
  if not Exec(PowerShellPath, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    ErrorText := 'PowerShell could not start. Check that Windows PowerShell is available.';
    Result := False;
    exit;
  end;

  Result := ExitCode = 0;
  if not Result then
    ErrorText := 'Explorer integration failed with exit code ' + IntToStr(ExitCode) + '.';
end;

function ReadPreviousInstallPath(var InstallPath: String): Boolean;
begin
  Result := RegQueryStringValue(HKCU, UninstallKey, 'InstallLocation', InstallPath);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExpectedPath, ErrorText: String;
  ScriptPath: String;
begin
  Result := '';
  if not ReadPreviousInstallPath(PreviousInstallPath) then
    exit;

  ExpectedPath := ExpandConstant('{localappdata}\Programs\Backdrop');
  if CompareText(RemoveBackslashUnlessRoot(PreviousInstallPath), RemoveBackslashUnlessRoot(ExpectedPath)) <> 0 then
  begin
    Result := 'A previous Backdrop install uses a different location. Remove that install before continuing.';
    exit;
  end;

  ScriptPath := AddBackslash(PreviousInstallPath) + 'scripts\classic-shell.ps1';
  if not FileExists(ScriptPath) then
  begin
    Result := 'The previous Backdrop Explorer integration script is missing. Repair or remove that installation before upgrading.';
    exit;
  end;

  if not RunClassicShellAction(PreviousInstallPath, 'Unregister', ErrorText) then
  begin
    Result := 'Backdrop could not remove its previous Explorer registration.' + #13#10 + ErrorText;
    exit;
  end;
  UpgradeShellWasUnregistered := True;
end;

function InitializeUninstall(): Boolean;
var
  ErrorText: String;
begin
  Result := True;
  UninstallInstallPath := ExpandConstant('{app}');
  if not FileExists(AddBackslash(UninstallInstallPath) + 'scripts\classic-shell.ps1') then
  begin
    MsgBox('Backdrop could not find its Explorer integration script. The application files were kept.',
      mbError, MB_OK);
    Result := False;
    exit;
  end;

  if not RunClassicShellAction(UninstallInstallPath, 'Unregister', ErrorText) then
  begin
    MsgBox('Backdrop could not remove its Explorer registration. The application files were kept.' + #13#10 + ErrorText,
      mbError, MB_OK);
    Result := False;
  end;
  if Result then
    UninstallShellWasUnregistered := True;
end;

procedure DeinitializeUninstall();
var
  ErrorText: String;
begin
  if UninstallShellWasUnregistered and
     FileExists(AddBackslash(UninstallInstallPath) + 'Backdrop.exe') and
     FileExists(AddBackslash(UninstallInstallPath) + 'Backdrop.Shell.dll') and
     FileExists(AddBackslash(UninstallInstallPath) + 'scripts\classic-shell.ps1') then
  begin
    if not RunClassicShellAction(UninstallInstallPath, 'Register', ErrorText) then
      MsgBox('Backdrop could not restore its Explorer menu. If Backdrop is still installed, reinstall it to repair the menu.' + #13#10 + ErrorText,
        mbError, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ErrorText: String;
begin
  if CurStep = ssPostInstall then
  begin
    ShellRegistrationSucceeded := RunClassicShellAction(ExpandConstant('{app}'), 'Register', ErrorText);
    if not ShellRegistrationSucceeded then
      MsgBox('Backdrop was installed, but the Explorer menu could not be enabled. Open Backdrop and choose Enable Explorer menu to try again.' + #13#10 + ErrorText,
        mbError, MB_OK);
  end;
end;

procedure DeinitializeSetup();
var
  ErrorText: String;
begin
  if UpgradeShellWasUnregistered and not ShellRegistrationSucceeded and
     FileExists(AddBackslash(PreviousInstallPath) + 'Backdrop.exe') and
     FileExists(AddBackslash(PreviousInstallPath) + 'scripts\classic-shell.ps1') then
  begin
    if not RunClassicShellAction(PreviousInstallPath, 'Register', ErrorText) then
      MsgBox('Setup did not restore the previous Explorer menu. Open Backdrop and choose Enable Explorer menu to try again.' + #13#10 + ErrorText,
        mbError, MB_OK);
  end;
end;
