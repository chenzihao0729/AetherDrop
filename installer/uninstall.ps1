# Removes AetherDrop: gives the Wi-Fi card back to its vendor driver first, then
# removes the service, the driver package, the certificate trust, the firewall
# rules, AetherDrop's TAP adapter, settings and the files. Received files stay
# where they were saved. Best effort: a step that fails is reported in
# %ProgramData%\AetherDrop-uninstall.log and the rest still runs (#22: failures
# were silent and left parts behind).
param([string]$Target = $(if ((Test-Path -LiteralPath (Join-Path $PSScriptRoot 'AetherDropService.exe')) -or (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'NekoDropService.exe'))) { $PSScriptRoot } else { "$env:ProgramFiles\AetherDrop" }),
      [switch]$DryRun, [switch]$SelfTest, [switch]$FromInstaller,
      [switch]$NoPause, [string]$LogPath = '', [switch]$RemoveInnoOnly)
$ErrorActionPreference = 'Continue'
if ([Environment]::Is64BitOperatingSystem -and -not [Environment]::Is64BitProcess) {
  $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-Target',$Target)
  if ($DryRun) { $arguments += '-DryRun' }; if ($SelfTest) { $arguments += '-SelfTest' }; if ($FromInstaller) { $arguments += '-FromInstaller' }
  if ($NoPause) { $arguments += '-NoPause' }; if ($LogPath) { $arguments += @('-LogPath', $LogPath) }
  if ($RemoveInnoOnly) { $arguments += '-RemoveInnoOnly' }
  & (Join-Path $env:SystemRoot 'Sysnative\WindowsPowerShell\v1.0\powershell.exe') @arguments
  exit $LASTEXITCODE
}
$PnpUtil = Join-Path $env:SystemRoot 'System32\pnputil.exe'
$ServiceControl = Join-Path $env:SystemRoot 'System32\sc.exe'
$Log = if ($LogPath) { $LogPath } else { Join-Path $env:ProgramData 'AetherDrop-uninstall.log' }

function Write-UninstallError($Failure, [string]$Step = 'Uninstall') {
  $details = @(
    "$(Get-Date -Format s) FAILED: $Step",
    ($Failure | Format-List * -Force | Out-String)
  ) -join [Environment]::NewLine
  if ($Failure -is [Management.Automation.ErrorRecord]) {
    $details += [Environment]::NewLine + $Failure.InvocationInfo.PositionMessage
    $details += [Environment]::NewLine + $Failure.ScriptStackTrace
  }
  Write-Host $details -ForegroundColor Red
  try { Add-Content -LiteralPath $script:Log -Value $details -Encoding utf8 -ErrorAction Stop }
  catch {
    # Even a non-administrator launch must leave the error somewhere writable.
    $script:Log = Join-Path ([IO.Path]::GetTempPath()) 'AetherDrop-uninstall-error.log'
    try { Add-Content -LiteralPath $script:Log -Value $details -Encoding utf8 -ErrorAction Stop }
    catch { Write-Host "Could not save the error log: $($_.Exception.Message)" -ForegroundColor Red }
  }
}

function Complete-Uninstall([int]$Code) {
  if ($Code -ne 0) { Write-Host "Error log: $script:Log" -ForegroundColor Yellow }
  # Installer/automation runs must never wait for keyboard input in a hidden window.
  if (-not ($FromInstaller -or $NoPause -or $DryRun -or $SelfTest) -and
      [Environment]::UserInteractive -and $Host.Name -eq 'ConsoleHost' -and
      -not [Console]::IsInputRedirected) {
    try { $null = Read-Host 'Press Enter to close this window' } catch { }
  }
  exit $Code
}

trap {
  Write-UninstallError $_ 'Unexpected error'
  Complete-Uninstall 1
}

function Resolve-UninstallTarget([string]$Path) {
  if ([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathRooted($Path)) { throw 'Target must be an absolute installation path.' }
  $resolved = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
  $protected = @([IO.Path]::GetPathRoot($resolved).TrimEnd('\'), $env:ProgramFiles,
    ${env:ProgramFiles(x86)}, $env:SystemRoot, $env:USERPROFILE, $env:ProgramData,
    $env:LOCALAPPDATA, $env:TEMP, (Join-Path $env:USERPROFILE 'Downloads'),
    (Join-Path $env:USERPROFILE 'Desktop'), (Join-Path $env:USERPROFILE 'Documents'))
  if ($protected | Where-Object { $_ -and $resolved -eq $_.TrimEnd('\', '/') }) { throw "Refusing protected target: $resolved" }
  if (Test-Path -LiteralPath $resolved) {
    if ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Target must not be a directory link.' }
    if (-not (Test-Path -LiteralPath (Join-Path $resolved 'AetherDropService.exe')) -and
        -not (Test-Path -LiteralPath (Join-Path $resolved 'NekoDropService.exe')) -and
        -not (Test-Path -LiteralPath (Join-Path $resolved 'AetherDrop.exe')) -and
        -not ((Test-Path -LiteralPath (Join-Path $resolved 'uninstall.ps1')) -and
              (Test-Path -LiteralPath (Join-Path $resolved 'unins000.exe')))) { throw "No AetherDrop installation found in $resolved" }
    foreach ($settings in @((Join-Path $env:LOCALAPPDATA 'AetherDrop\settings.json'), (Join-Path $env:LOCALAPPDATA 'nekodrop\settings.json'))) {
    if (Test-Path -LiteralPath $settings) {
      $saved = (Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json).SaveFolder
      if ($saved) {
        $saved = [IO.Path]::GetFullPath($saved).TrimEnd('\', '/')
        if ($saved -eq $resolved -or $saved.StartsWith($resolved + '\', [StringComparison]::OrdinalIgnoreCase)) {
          throw 'Received files are saved inside the target folder. Move them and change the save location before uninstalling.'
        }
      }
    }
    }
  }
  return $resolved
}
function Test-UninstallRuntimeProcess($Process, [string]$Root, [int[]]$ProtectedIds) {
  if ($ProtectedIds -contains [int]$Process.ProcessId) { return $false }
  $runtimeNames = @('AetherDrop.exe','AetherDropService.exe','NekoDropService.exe',
    'nekodrop.exe','AetherDrop-radio.exe','AetherDrop-radio-rtl.exe',
    'AetherDrop-radio-mt.exe','nekodrop-radio-mt.exe','AetherDrop-radio-mtk.exe',
    'appleid.exe','appleid-anisette.exe',
    'nekodrop-radio.exe','nekodrop-radio-rtl.exe','awdld.exe','python.exe')
  if ($runtimeNames -contains $Process.Name) {
    return ($Process.ExecutablePath -and $Process.ExecutablePath.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase))
  }
  # Only this application's beacon helper may be selected by command line.
  # Inno runs a temporary *.tmp uninstaller with the install folder in its
  # arguments; matching every command line containing Root killed that parent.
  if ($Process.Name -in @('powershell.exe','pwsh.exe') -and $Process.CommandLine) {
    $script = Join-Path $Root 'scripts\airdrop-beacon.ps1'
    return [regex]::IsMatch($Process.CommandLine,
      '(?i)(?:^|\s)-File\s+(?:"' + [regex]::Escape($script) + '"|' + [regex]::Escape($script) + '(?=\s|$))')
  }
  return $false
}

function Resolve-InnoUninstaller($Entry, [string]$AppTarget) {
  if ($Entry.DisplayName -notmatch '^Inno Setup \d+(?:\.\d+)*(?:\s|$)') { return $null }
  $command = [string]$Entry.UninstallString
  if ($command -notmatch '^"([^"]+\.exe)"(?:\s.*)?$' -and $command -notmatch '^(\S+\.exe)(?:\s.*)?$') {
    throw "Invalid Inno Setup uninstall command: $($Entry.DisplayName)"
  }
  $exe = $Matches[1]
  if (-not [IO.Path]::IsPathRooted($exe) -or -not $Entry.InstallLocation) { throw 'Inno Setup installation path is missing' }
  $directory = [IO.Path]::GetFullPath([string]$Entry.InstallLocation).TrimEnd('\','/')
  $exe = [IO.Path]::GetFullPath($exe)
  if ([IO.Path]::GetDirectoryName($exe) -ne $directory -or
      [IO.Path]::GetFileName($exe) -notmatch '^unins\d+\.exe$' -or
      $directory -eq [IO.Path]::GetFullPath($AppTarget).TrimEnd('\','/')) {
    throw 'Inno Setup uninstall path does not match its installation directory'
  }
  return $exe
}

function Remove-InnoSetup {
  $registryRoots = @(
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKCU:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
  )
  $entries = @(Get-ItemProperty -Path $registryRoots -ErrorAction SilentlyContinue |
    Where-Object { $_.DisplayName -match '^Inno Setup \d+(?:\.\d+)*(?:\s|$)' })
  foreach ($entry in $entries) {
    $exe = Resolve-InnoUninstaller $entry $Target
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Inno Setup uninstaller is missing: $exe" }
    $message = "Removing $($entry.DisplayName); waiting for its uninstaller to finish..."
    Write-Host $message
    Add-Content -LiteralPath $Log -Value $message -Encoding utf8 -ErrorAction Stop
    $process = Start-Process -FilePath $exe -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait -PassThru -ErrorAction Stop
    if ($process.ExitCode -ne 0) { throw "Inno Setup uninstall exited $($process.ExitCode); AetherDrop removal stopped" }
    # Exit alone is insufficient: confirm its registration has disappeared.
    $deadline = (Get-Date).AddSeconds(15)
    while (Test-Path -LiteralPath $entry.PSPath) {
      if ((Get-Date) -ge $deadline) { throw 'Inno Setup is still registered after its uninstaller exited; AetherDrop removal stopped' }
      Start-Sleep -Milliseconds 300
    }
    Add-Content -LiteralPath $Log -Value "$($entry.DisplayName) removed successfully." -Encoding utf8 -ErrorAction Stop
  }
  if (-not $entries.Count) { Write-Host 'Inno Setup is not installed; continuing.' }
}

if ($SelfTest) {
  foreach ($unsafe in @('', '.', [IO.Path]::GetPathRoot($env:SystemRoot), $env:ProgramFiles, $env:SystemRoot, $env:USERPROFILE, $env:LOCALAPPDATA)) {
    $rejected = $false
    try { $null = Resolve-UninstallTarget $unsafe } catch { $rejected = $true }
    if (-not $rejected) { throw "Unsafe target accepted: $unsafe" }
  }
  $testRoot = 'C:\Program Files\AetherDrop\'
  foreach ($case in @(
    @('AetherDrop.exe', ($testRoot + 'AetherDrop.exe'), '', 10, $true),
    @('python.exe', ($testRoot + 'python\python.exe'), '', 11, $true),
    @('python.exe', 'C:\Other\python.exe', '', 12, $false),
    @('unins000.exe', ($testRoot + 'unins000.exe'), '', 13, $false),
    @('_iu14D2N.tmp', 'C:\Temp\_iu14D2N.tmp', ('/UNINSTALL ' + $testRoot), 14, $false),
    @('powershell.exe', 'C:\Windows\powershell.exe', ('-File "' + $testRoot + 'uninstall.ps1"'), 15, $false),
    @('powershell.exe', 'C:\Windows\powershell.exe', ('-File "' + $testRoot + 'scripts\airdrop-beacon.ps1"'), 16, $true),
    @('AetherDrop.exe', ($testRoot + 'AetherDrop.exe'), '', 99, $false)
  )) {
    $process = [pscustomobject]@{ Name=$case[0]; ExecutablePath=$case[1]; CommandLine=$case[2]; ProcessId=$case[3] }
    if ([bool](Test-UninstallRuntimeProcess $process $testRoot @(99)) -ne $case[4]) { throw "Process selection failed: $($case[0]) / $($case[3])" }
  }
  $entry = [pscustomobject]@{ DisplayName='Inno Setup 7.1.1'; InstallLocation='C:\Tools\Inno Setup'; UninstallString='"C:\Tools\Inno Setup\unins000.exe" /SILENT' }
  if ((Resolve-InnoUninstaller $entry $testRoot) -ne 'C:\Tools\Inno Setup\unins000.exe') { throw 'Inno uninstall command parsing failed' }
  $entry.DisplayName = 'AetherDrop'
  if ($null -ne (Resolve-InnoUninstaller $entry $testRoot)) { throw 'Unrelated application matched Inno Setup' }
  $entry.DisplayName = 'Inno Setup 7.1.1'
  $entry.UninstallString = '"C:\Other\unins000.exe"'
  $rejected = $false
  try { $null = Resolve-InnoUninstaller $entry $testRoot } catch { $rejected = $true }
  if (-not $rejected) { throw 'Mismatched Inno uninstall directory accepted' }
  'Uninstall self-test passed: protected paths, 8 process cases and Inno uninstall selection; no system changes made.'
  exit 0
}
try { $Target = Resolve-UninstallTarget $Target } catch { Write-UninstallError $_ 'Validate installation path'; Complete-Uninstall 1 }
if ($DryRun) {
  "CHECK ONLY: uninstall target $Target"
  'Plan: remove Inno Setup and wait for completion, then restore Wi-Fi, remove service, driver certificate, firewall rules, app TAP adapter, shell registrations and application files.'
  'Received files and shared .NET runtimes will be preserved. No processes, files, registry or drivers changed.'
  exit 0
}
$admin = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $admin.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { Write-UninstallError 'Run uninstall.ps1 as administrator.' 'Administrator rights'; Complete-Uninstall 1 }
$Services = @('AetherDropService', 'NekoDropService')
$CertSubject = 'CN=AetherDrop driver (signed on this PC)'
"AetherDrop uninstall $(Get-Date -Format s), target $Target" | Set-Content $Log -Encoding utf8 -ErrorAction SilentlyContinue
try { Remove-InnoSetup } catch { Write-UninstallError $_ 'Remove Inno Setup first'; Complete-Uninstall 1 }
if ($RemoveInnoOnly) { 'Inno Setup removal completed; AetherDrop files retained.'; Complete-Uninstall 0 }
$failures = 0
function Try-Step($what, [scriptblock]$do) {
  $what
  try { $ErrorActionPreference = 'Stop'; & $do 2>&1 | ForEach-Object { "  $_" } | Add-Content $Log -Encoding utf8 -ErrorAction Stop }
  catch { $script:failures++; Write-UninstallError $_ $what }
}

Try-Step 'Giving the Wi-Fi card back' {
 foreach ($Service in $Services) {
  if (Get-Service $Service -ErrorAction SilentlyContinue) {
    Stop-Service $Service -Force -ErrorAction SilentlyContinue  # the card is given back below either way
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Service $Service).Status -ne 'Stopped' -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
  }
 }
  # Select only known application runtimes, never the installer or its hosts.
  $root = [IO.Path]::GetFullPath($Target).TrimEnd('\') + '\'
  # Never by path under a drive root or a folder that is not AetherDrop's.
  $byPath = $root.Length -gt 4 -and ((Test-Path (Join-Path $root 'AetherDropService.exe')) -or (Test-Path (Join-Path $root 'NekoDropService.exe')) -or (Test-Path (Join-Path $root 'python\python.exe')))
  $snapshot = @(Get-CimInstance Win32_Process -ErrorAction Stop)
  $protectedIds = @([int]$PID)
  $ancestor = $snapshot | Where-Object ProcessId -eq $PID | Select-Object -First 1
  while ($ancestor -and $ancestor.ParentProcessId -and $protectedIds -notcontains [int]$ancestor.ParentProcessId) {
    $protectedIds += [int]$ancestor.ParentProcessId
    $ancestor = $snapshot | Where-Object ProcessId -eq $ancestor.ParentProcessId | Select-Object -First 1
  }
  $processes = @($snapshot | Where-Object {
    $byPath -and (Test-UninstallRuntimeProcess $_ $root $protectedIds)
  } | ForEach-Object { Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue })
  $processes | Stop-Process -Force -ErrorAction SilentlyContinue
  $processes | ForEach-Object { try { $null = $_.WaitForExit(10000) } catch { } }
  $svc = Join-Path $Target 'AetherDropService.exe'
  if (-not (Test-Path -LiteralPath $svc)) { $svc = Join-Path $Target 'NekoDropService.exe' }
  if (Test-Path $svc) { & $svc --give-back; if ($LASTEXITCODE -ne 0) { throw "Wi-Fi restore exited $LASTEXITCODE" } }
  else {
    # A failed update can leave the folder without the service: restore.ps1
    # gives the card back on its own, from the same folder or this script's.
    $restore = @((Join-Path $Target 'restore.ps1'), (Join-Path $PSScriptRoot 'restore.ps1')) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($restore) { & $restore } else { throw "neither NekoDropService.exe nor restore.ps1 is in $Target" }
  }
}

Try-Step 'Service' {
 foreach ($Service in $Services) {
  if (Get-Service $Service -ErrorAction SilentlyContinue) {
    & $ServiceControl delete $Service | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe delete exited $LASTEXITCODE" }
  }
 }
}

Try-Step 'Driver package and certificate' {
  $store = [xml](& $PnpUtil /enum-drivers /format xml | Out-String)
  if ($LASTEXITCODE -ne 0) { throw "pnputil /enum-drivers exited $LASTEXITCODE" }
  $store.PnpUtil.Driver | Where-Object { $_.OriginalName -eq 'winawdl_regproxy.inf' -and $_.ProviderName -eq 'winairdrop project' } |
    ForEach-Object {
      & $PnpUtil /delete-driver $_.DriverName /uninstall /force | Out-Null
      if ($LASTEXITCODE -ne 0) { throw "pnputil /delete-driver $($_.DriverName) exited $LASTEXITCODE" }
    }
  foreach ($s in 'Root', 'TrustedPublisher') {
    Get-ChildItem "Cert:\LocalMachine\$s" | Where-Object { $_.Subject -in @($CertSubject, 'CN=nekodrop driver (signed on this PC)') } | Remove-Item -ErrorAction Stop
  }
}

Try-Step 'Firewall' {
  Get-NetFirewallRule -Group 'AetherDrop AirDrop','nekodrop AirDrop' -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction Stop
}

# Only the adapter setup-tap.ps1 made and named "AetherDrop"; the TAP-Windows
# driver stays, other programs (VPNs) may use it (#22).
Try-Step 'TAP adapter' {
  foreach ($a in @(Get-NetAdapter -Name 'AetherDrop','nekodrop' -IncludeHidden -ErrorAction SilentlyContinue |
                   Where-Object { $_.InterfaceDescription -like 'TAP-Windows Adapter V9*' })) {
    if ($a.PnPDeviceID) {
      & $PnpUtil /remove-device $a.PnPDeviceID | Out-Null
      if ($LASTEXITCODE -ne 0) { throw "pnputil /remove-device exited $LASTEXITCODE" }
    }
  }
}

Try-Step 'Files' {
  foreach ($packageName in 'AetherDrop.shell','nekodrop.shell') {
    Get-AppxPackage -AllUsers -Name $packageName -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
  }
  $client = Join-Path $Target 'AetherDrop.exe'
  if (-not $FromInstaller -and (Test-Path $client)) {
    $unregister = Start-Process -FilePath $client -ArgumentList '--unregister' -PassThru -WindowStyle Hidden
    if (-not $unregister.WaitForExit(15000)) {
      Stop-Process -Id $unregister.Id -Force -ErrorAction SilentlyContinue
      throw 'AetherDrop.exe --unregister did not exit within 15 seconds'
    }
    if ($unregister.ExitCode -ne 0) { throw "Shell unregister exited $($unregister.ExitCode)" }
  }
  foreach ($dir in $Target, "$env:ProgramData\AetherDrop", "$env:LOCALAPPDATA\AetherDrop", "$env:ProgramData\nekodrop", "$env:LOCALAPPDATA\nekodrop") {
    if (Test-Path -LiteralPath $dir) {
      for ($attempt=0; ; $attempt++) { try {
       if ($FromInstaller -and $dir -eq $Target) {
        # Inno Setup is running its own uninstaller from this folder.
        Get-ChildItem -LiteralPath $dir -Force | Where-Object { $_.Name -notlike 'unins*' } |
          ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop }
       } else { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction Stop }
       break
      } catch { if ($attempt -ge 9) { throw }; Start-Sleep -Milliseconds 300 } }
    }
  }
}
if (-not $FromInstaller -and -not (Test-Path -LiteralPath $Target)) {
  Try-Step 'Shortcuts and uninstall registration' {
    $shortcutNames = @('AetherDrop.lnk','AetherDrop - diagnostic report.lnk','AetherDrop - restore Windows Wi-Fi.lnk')
    foreach ($folder in @((Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs'),
                          (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'))) {
      foreach ($name in $shortcutNames) {
        $shortcut = Join-Path $folder $name
        if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -ErrorAction Stop }
      }
    }
    foreach ($folder in @([Environment]::GetFolderPath('CommonDesktopDirectory'), [Environment]::GetFolderPath('Desktop'))) {
      if ($folder) {
        $shortcut = Join-Path $folder 'AetherDrop.lnk'
        if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -ErrorAction Stop }
      }
    }
    $registration = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{6C1F2E7A-4B8E-4E57-9C61-5E2B1D0A9F31}_is1'
    if (Test-Path -LiteralPath $registration) {
      $location = (Get-ItemProperty -LiteralPath $registration).InstallLocation
      if ($location -and [IO.Path]::GetFullPath($location).TrimEnd('\','/') -eq $Target) {
        Remove-Item -LiteralPath $registration -Recurse -ErrorAction Stop
      }
    }
  }
}
if ($failures) { "AetherDrop uninstall incomplete: $failures step(s) failed. See $Log"; Complete-Uninstall 1 } else { 'AetherDrop is removed.'; Complete-Uninstall 0 }
