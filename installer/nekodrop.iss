; AetherDrop setup (Inno Setup 6). Built by scripts\build-installer.ps1 from build\stage.
; One exe installs everything: the client and service (self-contained, no .NET
; needed), the user-mode radio, awdld + Wintun, the AirDrop engine with its own
; Python, the register-proxy driver, TAP-Windows, firewall rules and the service.
#define AppVersion "0.1.36"

[Setup]
AppId={{6C1F2E7A-4B8E-4E57-9C61-5E2B1D0A9F31}
AppName=AetherDrop
AppVersion={#AppVersion}
AppPublisher=AetherDrop project
DefaultDirName={autopf}\AetherDrop
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
zh.Installing=正在安装驱动、TAP 网卡、防火墙规则和 AetherDrop 服务…
en.Installing=Installing the driver, TAP adapter, firewall rules and the AetherDrop service...
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
Source: "..\build\stage\AetherDrop.exe"; DestDir: "{tmp}\nekodrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\NekoDropService.exe"; DestDir: "{tmp}\nekodrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\nekodrop-shell.dll"; DestDir: "{tmp}\nekodrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\tap-windows.exe"; DestDir: "{tmp}\nekodrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\install.ps1"; DestDir: "{tmp}\nekodrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\uninstall.ps1"; DestDir: "{tmp}\nekodrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\setup-tap.ps1"; DestDir: "{tmp}\nekodrop-stage"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\restore.*"; DestDir: "{tmp}\nekodrop-stage"; Excludes: "*.log,*.pdb,*.bak"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\report.*"; DestDir: "{tmp}\nekodrop-stage"; Excludes: "*.log,*.pdb,*.bak"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\airdrop\*.py"; DestDir: "{tmp}\nekodrop-stage\airdrop"; Excludes: "test*,*test*.py"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\airdrop\LICENSE"; DestDir: "{tmp}\nekodrop-stage\airdrop"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\scripts\airdrop-beacon.ps1"; DestDir: "{tmp}\nekodrop-stage\scripts"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\awdl\*"; DestDir: "{tmp}\nekodrop-stage\awdl"; Excludes: "*.log,*.pdb,*.bak,tests\*,__pycache__\*"; Flags: recursesubdirs ignoreversion deleteafterinstall
Source: "..\build\stage\driver\*"; DestDir: "{tmp}\nekodrop-stage\driver"; Excludes: "*.log,*.pdb,*.bak,tests\*"; Flags: recursesubdirs ignoreversion deleteafterinstall
Source: "..\build\stage\kdriver\*"; DestDir: "{tmp}\nekodrop-stage\kdriver"; Excludes: "*.log,*.pdb,*.bak,tests\*"; Flags: recursesubdirs ignoreversion deleteafterinstall
Source: "..\build\stage\licenses\*"; DestDir: "{tmp}\nekodrop-stage\licenses"; Flags: ignoreversion deleteafterinstall
Source: "..\build\stage\python\*"; DestDir: "{tmp}\nekodrop-stage\python"; Excludes: "*.log,*.pdb,*.bak,*.pyc,__pycache__\*,test\*,tests\*"; Flags: recursesubdirs ignoreversion deleteafterinstall
Source: "..\build\stage\radio\*"; DestDir: "{tmp}\nekodrop-stage\radio"; Excludes: "*.log,*.pdb,*.bak,tests\*"; Flags: recursesubdirs ignoreversion deleteafterinstall
Source: "..\build\stage\shell\*"; DestDir: "{tmp}\nekodrop-stage\shell"; Excludes: "*.log,*.pdb,*.bak,tests\*"; Flags: recursesubdirs ignoreversion deleteafterinstall

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
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\uninstall.ps1"" -Target ""{app}"" -FromInstaller"; \
  Flags: runhidden waituntilterminated; RunOnceId: "AetherDrop-uninstall"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
  Err: AnsiString;
  Log: String;
begin
  if CurStep <> ssPostInstall then Exit;
  WizardForm.StatusLabel.Caption := CustomMessage('Installing');
  Log := ExpandConstant('{commonappdata}\nekodrop\install.log');
  if not Exec('powershell.exe',
      '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\nekodrop-stage\install.ps1') +
      '" -Target "' + ExpandConstant('{app}') + '"',
      '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Code := -1;
  if Code <> 0 then
  begin
    if not LoadStringFromFile(ExpandConstant('{commonappdata}\nekodrop\install-error.txt'), Err) then
      Err := 'install.ps1 exit code ' + IntToStr(Code);
    SuppressibleMsgBox(FmtMessage(CustomMessage('Failed'), [String(Err), Log]), mbError, MB_OK, IDOK);
  end;
end;
