; =============================================================================
; Syslog-NG + PostgreSQL Stack Installer
; Inno Setup 6.x Script — Rewritten from scratch
; =============================================================================
; One-click installer for air-gapped Windows environments.
; Target: Windows 10 LTSC 2019+ (Build 17763+)
; Installs: Hyper-V (if needed), Docker Desktop 4.15, PostgreSQL 17, syslog-ng
; =============================================================================

#define MyAppName "Syslog Stack"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "IT Infrastructure"
#define DefaultInstallDir "C:\SyslogStack"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={#DefaultInstallDir}
DefaultGroupName={#MyAppName}
OutputDir=Output
OutputBaseFilename=SyslogStack-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
WizardSizePercent=120
DisableProgramGroupPage=yes
DisableReadyPage=no
ShowLanguageDialog=no
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full";   Description: "Full Installation (Docker + PostgreSQL + Syslog-NG)"
Name: "syslog"; Description: "Syslog-NG Only (use existing PostgreSQL)"
Name: "custom"; Description: "Custom Installation"; Flags: iscustom

[Components]
Name: "docker";    Description: "Docker Desktop 4.15";              Types: full custom
Name: "postgres";  Description: "PostgreSQL 17 Database";           Types: full custom
Name: "syslogng";  Description: "Syslog-NG Container";              Types: full syslog custom; Flags: fixed
Name: "firewall";  Description: "Configure Windows Firewall";       Types: full syslog custom
Name: "tls";       Description: "Generate TLS Certificates";        Types: full syslog custom

[Files]
Source: "scripts\*";              DestDir: "{app}\scripts";       Flags: ignoreversion recursesubdirs
Source: "sql\*";                  DestDir: "{app}\sql";           Flags: ignoreversion recursesubdirs
Source: "payload\*";              DestDir: "{app}\payload";       Flags: ignoreversion recursesubdirs
Source: "dependencies\*";         DestDir: "{app}\dependencies";  Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "config.ini";             DestDir: "{app}";               Flags: ignoreversion

[Dirs]
Name: "{app}\certs";  Permissions: admins-full
Name: "{app}\logs";   Permissions: admins-full

[Icons]
Name: "{group}\Syslog Stack Logs";      Filename: "{app}\logs"
Name: "{group}\Syslog Stack Config";    Filename: "{app}\payload"
Name: "{group}\Uninstall Syslog Stack"; Filename: "{uninstallexe}"

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\Uninstall-SyslogStack.ps1"" -InstallDir ""{app}"""; Flags: runhidden; RunOnceId: "UninstallSyslog"

[Code]
// =========================================================================
// CONSTANTS
// =========================================================================
const
  STATE_KEY = 'Software\SyslogStack\Install';
  RUNONCE_KEY = 'SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce';
  RUNONCE_NAME = 'SyslogStackResume';
  RESUME_PARAM = '/RESUMEINSTALL';

// =========================================================================
// GLOBAL VARIABLES
// =========================================================================
var
  PagePort: TInputQueryWizardPage;
  PageTLS:  TInputOptionWizardPage;

  IsResumeMode:      Boolean;
  DockerInstalled:    Boolean;
  PostgresInstalled:  Boolean;
  OSBuild:            Integer;

// =========================================================================
// HELPER: Boolean to string
// =========================================================================
function BoolStr(B: Boolean): String;
begin
  if B then Result := 'True' else Result := 'False';
end;

// =========================================================================
// HELPER: Safe registry read
// =========================================================================
function RegRead(Root: Integer; Key, Name: String): String;
var V: String;
begin
  if RegQueryStringValue(Root, Key, Name, V) then
    Result := V
  else
    Result := '';
end;

// =========================================================================
// Detect installed software
// =========================================================================
procedure DetectSoftware();
var
  BuildStr: String;
begin
  // OS Build
  BuildStr := RegRead(HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\Windows NT\CurrentVersion', 'CurrentBuildNumber');
  OSBuild := StrToIntDef(BuildStr, 0);
  Log('OS Build: ' + IntToStr(OSBuild));

  // Docker Desktop
  DockerInstalled := RegKeyExists(HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Docker Desktop');
  Log('Docker installed: ' + BoolStr(DockerInstalled));

  // PostgreSQL (check 15, 16, 17)
  PostgresInstalled := False;
  if RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\PostgreSQL\Installations\postgresql-x64-17') or
     RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\PostgreSQL\Installations\postgresql-x64-16') or
     RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\PostgreSQL\Installations\postgresql-x64-15') or
     RegKeyExists(HKEY_LOCAL_MACHINE,
       'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PostgreSQL 17') or
     RegKeyExists(HKEY_LOCAL_MACHINE,
       'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PostgreSQL 16') then
  begin
    PostgresInstalled := True;
  end;

  // Also check if any PostgreSQL service exists
  if not PostgresInstalled then
  begin
    if RegKeyExists(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\postgresql-x64-17') or
       RegKeyExists(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\postgresql-x64-16') or
       RegKeyExists(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\postgresql-syslog') then
      PostgresInstalled := True;
  end;

  Log('PostgreSQL installed: ' + BoolStr(PostgresInstalled));
end;

// =========================================================================
// Check if we are in resume mode (after reboot)
// =========================================================================
function CheckResumeMode(): Boolean;
begin
  Result := (Pos(RESUME_PARAM, UpperCase(GetCmdTail)) > 0) or
            (RegRead(HKEY_LOCAL_MACHINE, STATE_KEY, 'State') = 'NeedResume');
end;

// =========================================================================
// Save settings to registry for resume after reboot
// =========================================================================
procedure SaveState();
begin
  RegWriteStringValue(HKEY_LOCAL_MACHINE, STATE_KEY, 'State', 'NeedResume');
  RegWriteStringValue(HKEY_LOCAL_MACHINE, STATE_KEY, 'InstallDir',
    ExpandConstant('{app}'));
  RegWriteStringValue(HKEY_LOCAL_MACHINE, STATE_KEY, 'SyslogPort',
    PagePort.Values[0]);
  RegWriteStringValue(HKEY_LOCAL_MACHINE, STATE_KEY, 'TLSPort',
    PagePort.Values[1]);
  RegWriteStringValue(HKEY_LOCAL_MACHINE, STATE_KEY, 'InstallPG',
    BoolStr(not PostgresInstalled));
  RegWriteStringValue(HKEY_LOCAL_MACHINE, STATE_KEY, 'InstallDocker',
    BoolStr(not DockerInstalled));
  RegWriteStringValue(HKEY_LOCAL_MACHINE, STATE_KEY, 'GenTLS',
    BoolStr(PageTLS.SelectedValueIndex = 0));
end;

// =========================================================================
// Register RunOnce to resume after reboot
// =========================================================================
procedure RegisterResume();
var
  SrcExe, DestExe: String;
begin
  SrcExe := ExpandConstant('{srcexe}');
  DestExe := ExpandConstant('{app}\SyslogStack-Resume.exe');

  // Copy EXE to install dir so it survives temp cleanup
  if not FileCopy(SrcExe, DestExe, False) then
  begin
    Log('WARN: Could not copy EXE to ' + DestExe + ', using source path');
    DestExe := SrcExe;
  end;

  RegWriteStringValue(HKEY_LOCAL_MACHINE, RUNONCE_KEY, RUNONCE_NAME,
    '"' + DestExe + '" /SILENT ' + RESUME_PARAM);
  Log('RunOnce registered: ' + DestExe);
end;

// =========================================================================
// Clean up all installer state
// =========================================================================
procedure CleanupState();
var
  R: Integer;
  ResumeExe: String;
begin
  RegDeleteValue(HKEY_LOCAL_MACHINE, RUNONCE_KEY, RUNONCE_NAME);
  RegDeleteKeyIncludingSubkeys(HKEY_LOCAL_MACHINE, STATE_KEY);

  Exec('cmd.exe', '/c schtasks /delete /tn "SyslogStackResume" /f',
       '', SW_HIDE, ewWaitUntilTerminated, R);

  ResumeExe := ExpandConstant('{app}\SyslogStack-Resume.exe');
  if FileExists(ResumeExe) then
    DeleteFile(ResumeExe);
end;

// =========================================================================
// Run PowerShell script and return exit code
// =========================================================================
function RunPS(Script, Args: String): Integer;
var
  PSExe, Params: String;
  OK: Boolean;
begin
  PSExe := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Params := '-NoProfile -ExecutionPolicy Bypass -File "' + Script + '" ' + Args;
  Log('RunPS: ' + PSExe + ' ' + Params);
  OK := Exec(PSExe, Params, ExpandConstant('{app}'), SW_SHOW,
             ewWaitUntilTerminated, Result);
  if not OK then
  begin
    Log('RunPS: Failed to launch PowerShell. Error=' + IntToStr(Result));
    Result := -1;
  end
  else
    Log('RunPS: Exit code = ' + IntToStr(Result));
end;

// =========================================================================
// Enable Hyper-V using DISM directly (no PowerShell needed)
// Returns: 0=already enabled, 3010=reboot needed, other=error
// =========================================================================
function EnableHyperV(): Integer;
var
  DismExe: String;
  OK: Boolean;
begin
  DismExe := ExpandConstant('{sys}\dism.exe');
  OK := Exec(DismExe,
    '/Online /Enable-Feature /All /FeatureName:Microsoft-Hyper-V /NoRestart',
    '', SW_HIDE, ewWaitUntilTerminated, Result);
  if not OK then
  begin
    Log('DISM failed to launch. Error=' + IntToStr(Result));
    Result := -1;
  end
  else
    Log('DISM Hyper-V exit code: ' + IntToStr(Result));
end;

// =========================================================================
// WIZARD INIT
// =========================================================================
procedure InitializeWizard();
begin
  DetectSoftware();
  IsResumeMode := CheckResumeMode();

  // Port configuration page
  PagePort := CreateInputQueryPage(wpSelectComponents,
    'Syslog-NG Port Configuration',
    'Network ports for syslog collection',
    'Both UDP and TCP syslog listen on the same port.');
  PagePort.Add('Syslog Port (UDP + TCP):', False);
  PagePort.Add('TLS Port (encrypted syslog):', False);

  if IsResumeMode then
  begin
    PagePort.Values[0] := RegRead(HKEY_LOCAL_MACHINE, STATE_KEY, 'SyslogPort');
    PagePort.Values[1] := RegRead(HKEY_LOCAL_MACHINE, STATE_KEY, 'TLSPort');
    if PagePort.Values[0] = '' then PagePort.Values[0] := '514';
    if PagePort.Values[1] = '' then PagePort.Values[1] := '6514';
  end
  else
  begin
    PagePort.Values[0] := '514';
    PagePort.Values[1] := '6514';
  end;

  // TLS page
  PageTLS := CreateInputOptionPage(PagePort.ID,
    'TLS Certificate Configuration',
    'SSL/TLS certificates for encrypted syslog',
    'Choose how to handle TLS certificates:', True, False);
  PageTLS.Add('Generate new self-signed certificates (recommended)');
  PageTLS.Add('I will provide my own certificates later');
  PageTLS.SelectedValueIndex := 0;
end;

// =========================================================================
// Skip pages in resume mode
// =========================================================================
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := IsResumeMode;
end;

// =========================================================================
// Validate port inputs
// =========================================================================
function NextButtonClick(CurPageID: Integer): Boolean;
var
  P: Integer;
begin
  Result := True;
  if CurPageID = PagePort.ID then
  begin
    P := StrToIntDef(PagePort.Values[0], 0);
    if (P < 1) or (P > 65535) then
    begin
      MsgBox('Syslog Port must be between 1 and 65535.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    P := StrToIntDef(PagePort.Values[1], 0);
    if (P < 1) or (P > 65535) then
    begin
      MsgBox('TLS Port must be between 1 and 65535.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;
end;

// =========================================================================
// MAIN POST-INSTALL LOGIC
// =========================================================================
procedure CurStepChanged(CurStep: TSetupStep);
var
  RC: Integer;
  SyslogPort, TLSPort, PSArgs: String;
  NeedHyperV: Boolean;
  InstallPG, InstallDocker, GenTLS: Boolean;
begin
  if CurStep <> ssPostInstall then Exit;

  DetectSoftware();

  // ------ Determine settings ------
  if IsResumeMode then
  begin
    SyslogPort := RegRead(HKEY_LOCAL_MACHINE, STATE_KEY, 'SyslogPort');
    TLSPort    := RegRead(HKEY_LOCAL_MACHINE, STATE_KEY, 'TLSPort');
    InstallPG  := (RegRead(HKEY_LOCAL_MACHINE, STATE_KEY, 'InstallPG') = 'True');
    InstallDocker := (RegRead(HKEY_LOCAL_MACHINE, STATE_KEY, 'InstallDocker') = 'True');
    GenTLS     := (RegRead(HKEY_LOCAL_MACHINE, STATE_KEY, 'GenTLS') = 'True');
    if SyslogPort = '' then SyslogPort := '514';
    if TLSPort = '' then TLSPort := '6514';
  end
  else
  begin
    SyslogPort := PagePort.Values[0];
    TLSPort    := PagePort.Values[1];
    InstallPG  := not PostgresInstalled;
    InstallDocker := not DockerInstalled;
    GenTLS     := (PageTLS.SelectedValueIndex = 0);
  end;

  // ------ STEP 1: Hyper-V (only if Docker not installed and LTSC 2019) ------
  NeedHyperV := False;
  if (not DockerInstalled) and (OSBuild < 19041) then
  begin
    // LTSC 2019 needs Hyper-V for Docker
    if not RegKeyExists(HKEY_LOCAL_MACHINE,
      'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Virtualization') then
      NeedHyperV := True;
  end;

  if NeedHyperV and (not IsResumeMode) then
  begin
    Log('Enabling Hyper-V via DISM...');
    RC := EnableHyperV();

    if RC = 3010 then
    begin
      // Reboot required
      Log('Hyper-V enabled, reboot required');
      SaveState();
      RegisterResume();
      MsgBox('Hyper-V has been enabled. The system must restart.' + #13#10 +
             'Installation will resume automatically after restart.',
             mbInformation, MB_OK);
      Exec('cmd.exe',
        '/c shutdown /r /t 30 /f /c "Syslog Stack - restarting for Hyper-V"',
        '', SW_HIDE, ewNoWait, RC);
      Exit;
    end
    else if (RC <> 0) and (RC <> 1) then
    begin
      // Non-fatal: Hyper-V might already be enabled or not needed
      Log('DISM returned ' + IntToStr(RC) + ', continuing anyway');
    end;
  end;

  // ------ STEP 2: Run main install script ------
  PSArgs := '-InstallDir "' + ExpandConstant('{app}') + '"' +
            ' -SyslogPort "' + SyslogPort + '"' +
            ' -TLSPort "' + TLSPort + '"';

  if InstallDocker then
    PSArgs := PSArgs + ' -InstallDocker';
  if InstallPG then
    PSArgs := PSArgs + ' -InstallPostgres';
  if GenTLS then
    PSArgs := PSArgs + ' -GenerateTLS';

  RC := RunPS(ExpandConstant('{app}') + '\scripts\Install-SyslogStack.ps1',
              PSArgs);

  if RC = 0 then
  begin
    MsgBox('Syslog Stack installed successfully!' + #13#10#13#10 +
           'Syslog-NG is now listening on:' + #13#10 +
           '  UDP+TCP port ' + SyslogPort + #13#10 +
           '  TLS port ' + TLSPort,
           mbInformation, MB_OK);
  end
  else
  begin
    MsgBox('Installation encountered an error (code ' + IntToStr(RC) + ').' + #13#10 + 'Check: ' + ExpandConstant('{app}') + '\install.log', mbError, MB_OK);
  end;

  // Clean up state regardless
  CleanupState();
end;

// =========================================================================
// Confirm before uninstall
// =========================================================================
function InitializeUninstall(): Boolean;
begin
  Result := MsgBox('This will stop the Syslog-NG container and remove the installation.' + #13#10 + 'PostgreSQL data will NOT be removed.' + #13#10#13#10 + 'Continue?', mbConfirmation, MB_YESNO) = IDYES;
end;
