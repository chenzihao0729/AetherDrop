; AetherDrop setup (Inno Setup 6). Built by scripts\build-installer.ps1 from build\stage.
; One exe installs everything: the client and service (self-contained, no .NET
; needed), the user-mode radio, awdld + Wintun, the AirDrop engine with its own
; Python, the register-proxy driver, TAP-Windows, firewall rules and the service.
#define AppVersion "2.1.3"

[Setup]
AppId={{6C1F2E7A-4B8E-4E57-9C61-5E2B1D0A9F31}
AppName=AetherDrop
AppVersion={#AppVersion}
AppPublisher=AetherDrop project
DefaultDirName={autopf}\AetherDrop
; The shared AppId must not restore AetherDrop's previously recorded directory.
UsePreviousAppDir=no
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputBaseFilename=AetherDrop-{#AppVersion}-setup
OutputDir=..\build
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=../app/assets/AetherDrop.ico
UninstallDisplayIcon={app}\AetherDrop.exe
CloseApplications=yes

[Languages]
Name: "zh"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
zh.Installing=正在安装驱动、TAP 网卡、防火墙规则和 AetherDrop 服务（约需 1 分钟）…
en.Installing=Installing the driver, TAP adapter, firewall rules and the AetherDrop service (about a minute)…
zh.Failed=AetherDrop 的系统组件没有装好：%n%n%1%n%n完整日志：%2
en.Failed=AetherDrop's system parts did not install:%n%n%1%n%nFull log: %2
zh.StartNow=启动 AetherDrop
en.StartNow=Start AetherDrop
zh.Desktop=创建桌面快捷方式
en.Desktop=Create a desktop shortcut

[Tasks]
Name: "desktop"; Description: "{cm:Desktop}"; Flags: unchecked

[Files]
; Explicit runtime payload only. Tests, snapshots, logs and source build outputs
; outside these component directories can never enter the installer.
Source: "..\build\stage\AetherDrop.exe"; DestDir: "{tmp}\AetherDrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\AetherDropService.exe"; DestDir: "{tmp}\AetherDrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\AetherDrop-shell.dll"; DestDir: "{tmp}\AetherDrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\tap-windows.exe"; DestDir: "{tmp}\AetherDrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\install.ps1"; DestDir: "{tmp}\AetherDrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\uninstall.ps1"; DestDir: "{tmp}\AetherDrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\uninstall.cmd"; DestDir: "{tmp}\AetherDrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\setup-tap.ps1"; DestDir: "{tmp}\AetherDrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\restore.*"; DestDir: "{tmp}\AetherDrop-stage"; Excludes: "*.log,*.pdb,*.bak"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\report.*"; DestDir: "{tmp}\AetherDrop-stage"; Excludes: "*.log,*.pdb,*.bak"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\airdrop\*.py"; DestDir: "{tmp}\AetherDrop-stage\airdrop"; Excludes: "test*,*test*.py"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\airdrop\LICENSE"; DestDir: "{tmp}\AetherDrop-stage\airdrop"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\scripts\airdrop-beacon.ps1"; DestDir: "{tmp}\AetherDrop-stage\scripts"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\awdl\*"; DestDir: "{tmp}\AetherDrop-stage\awdl"; Excludes: "*.log,*.pdb,*.bak,tests\*,__pycache__\*"; Flags: recursesubdirs ignoreversion deleteafterinstall
Source: "..\build\stage\driver\*"; DestDir: "{tmp}\AetherDrop-stage\driver"; Excludes: "*.log,*.pdb,*.bak,tests\*"; Flags: recursesubdirs ignoreversion deleteafterinstall
Source: "..\build\stage\kdriver\*"; DestDir: "{tmp}\AetherDrop-stage\kdriver"; Excludes: "*.log,*.pdb,*.bak,tests\*"; Flags: recursesubdirs ignoreversion deleteafterinstall
Source: "..\build\stage\appleid\*"; DestDir: "{tmp}\AetherDrop-stage\appleid"; Excludes: "*.log,*.pdb,*.bak"; Flags: recursesubdirs ignoreversion deleteafterinstall skipifsourcedoesntexist
Source: "..\build\stage\licenses\*"; DestDir: "{tmp}\AetherDrop-stage\licenses"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\python\*"; DestDir: "{tmp}\AetherDrop-stage\python"; Excludes: "*.log,*.pdb,*.bak,*.pyc,__pycache__\*,test\*,tests\*"; Flags: recursesubdirs ignoreversion deleteafterinstall
Source: "..\build\stage\radio\*"; DestDir: "{tmp}\AetherDrop-stage\radio"; Excludes: "*.log,*.pdb,*.bak,tests\*"; Flags: recursesubdirs ignoreversion deleteafterinstall
Source: "..\build\stage\shell\*"; DestDir: "{tmp}\AetherDrop-stage\shell"; Excludes: "*.log,*.pdb,*.bak,tests\*"; Flags: recursesubdirs ignoreversion deleteafterinstall

[Icons]
; The AppUserModelID lets Windows show AetherDrop's notifications under its own name.
Name: "{autoprograms}\AetherDrop"; Filename: "{app}\AetherDrop.exe"; AppUserModelID: "AetherDrop.app"
Name: "{autodesktop}\AetherDrop"; Filename: "{app}\AetherDrop.exe"; AppUserModelID: "AetherDrop.app"; Tasks: desktop
; One-click rollback: gives the Wi-Fi card back to Intel's driver (restore.ps1).
Name: "{autoprograms}\AetherDrop - restore Windows Wi-Fi"; Filename: "{app}\restore.cmd"; IconFilename: "{sys}\netshell.dll"; IconIndex: 0
; Diagnostic report for bug reports: logs and system details, redacted (report.ps1).
Name: "{autoprograms}\AetherDrop - diagnostic report"; Filename: "{app}\report.cmd"; IconFilename: "{sys}\imageres.dll"; IconIndex: 76

[Run]
; Started as the signed-in user: the first start registers the Explorer
; "用 AetherDrop 发送" menu, Send To and the notification protocol for that user.
Filename: "{app}\AetherDrop.exe"; Description: "{cm:StartNow}"; Flags: postinstall nowait skipifsilent runasoriginaluser
; An update started from AetherDrop runs silently: start the new version again.
Filename: "{app}\AetherDrop.exe"; Parameters: "--background"; Flags: nowait runasoriginaluser; Check: WizardSilent

[UninstallRun]
Filename: "{app}\AetherDrop.exe"; Parameters: "--unregister"; Flags: waituntilterminated skipifdoesntexist runhidden; RunOnceId: "AetherDrop-unregister"
Filename: "{sysnative}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\uninstall.ps1"" -Target ""{app}"" -FromInstaller"; \
  Flags: runhidden waituntilterminated; RunOnceId: "AetherDrop-uninstall"

[Code]
var
  InstallFailed: Boolean;

function GetCustomSetupExitCode: Integer;
begin
  if InstallFailed then Result := 1 else Result := 0;
end;

function InitializeUninstall(): Boolean;
var
  Code: Integer;
begin
  Result := False;
  if not Exec(ExpandConstant('{sysnative}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\uninstall.ps1') +
      '" -Target "' + ExpandConstant('{app}') + '" -FromInstaller -RemoveInnoOnly',
      '', SW_HIDE, ewWaitUntilTerminated, Code) then Code := -1;
  if Code = 0 then Result := True
  else SuppressibleMsgBox('Inno Setup removal failed. AetherDrop has not been removed.' + #13#10 +
      ExpandConstant('{commonappdata}\AetherDrop-uninstall.log'), mbError, MB_OK, IDOK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
  Lines: TArrayOfString;
  Err: String;
  Log: String;
  I: Integer;
begin
  if CurStep <> ssPostInstall then Exit;
  WizardForm.StatusLabel.Caption := CustomMessage('Installing');
  Log := ExpandConstant('{commonappdata}\AetherDrop\install.log');
  // The files are copied by now, so the bar stood full and still for the half
  // minute or more the driver, TAP and service take, and looked stuck (#45):
  // let it run while install.ps1 works.
  WizardForm.ProgressGauge.Style := npbstMarquee;
  try
    if not Exec(ExpandConstant('{sysnative}\WindowsPowerShell\v1.0\powershell.exe'),
        '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\AetherDrop-stage\install.ps1') +
        '" -Target "' + ExpandConstant('{app}') + '"',
        '', SW_HIDE, ewWaitUntilTerminated, Code) then
      Code := -1;
  finally
    WizardForm.ProgressGauge.Style := npbstNormal;
  end;
  if Code <> 0 then
  begin
    InstallFailed := True;
    // install.ps1 writes UTF-8 (with a BOM); LoadStringFromFile read it as ANSI,
    // which garbled Windows' own Chinese error text and showed the BOM as a stray character.
    Err := '';
    if LoadStringsFromFile(ExpandConstant('{commonappdata}\AetherDrop\install-error.txt'), Lines) then
      for I := 0 to GetArrayLength(Lines) - 1 do
        Err := Err + Lines[I] + #13#10;
    Err := Trim(Err);
    if Err = '' then
      Err := 'install.ps1 exit code ' + IntToStr(Code);
    SuppressibleMsgBox(FmtMessage(CustomMessage('Failed'), [UTF8Decode(Err), Log]), mbError, MB_OK, IDOK);
  end;
end;
