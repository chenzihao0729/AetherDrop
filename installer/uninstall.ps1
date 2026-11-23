# Removes nekodrop: gives the Wi-Fi card back to its vendor driver first, then
# removes the service, the driver package, the certificate trust, the firewall
# rules, nekodrop's TAP adapter, settings and the files. Received files stay
# where they were saved. Best effort: a step that fails is reported in
# %ProgramData%\nekodrop-uninstall.log and the rest still runs (#22: failures
# were silent and left parts behind).
param([string]$Target = $(if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'NekoDropService.exe')) { $PSScriptRoot } else { "$env:ProgramFiles\AetherDrop" }),
      [switch]$DryRun, [switch]$SelfTest, [switch]$FromInstaller)
$ErrorActionPreference = 'Continue'

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
    if (-not (Test-Path -LiteralPath (Join-Path $resolved 'NekoDropService.exe')) -and
        -not (Test-Path -LiteralPath (Join-Path $resolved 'AetherDrop.exe'))) { throw "No AetherDrop installation found in $resolved" }
    $settings = Join-Path $env:LOCALAPPDATA 'nekodrop\settings.json'
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
  return $resolved
}
if ($SelfTest) {
  foreach ($unsafe in @('', '.', [IO.Path]::GetPathRoot($env:SystemRoot), $env:ProgramFiles, $env:SystemRoot, $env:USERPROFILE, $env:LOCALAPPDATA)) {
    $rejected = $false
    try { $null = Resolve-UninstallTarget $unsafe } catch { $rejected = $true }
    if (-not $rejected) { throw "Unsafe target accepted: $unsafe" }
  }
  'Uninstall self-test passed: protected and relative targets rejected; no system changes made.'
  exit 0
}
try { $Target = Resolve-UninstallTarget $Target } catch { Write-Error $_; exit 1 }
if ($DryRun) {
  "CHECK ONLY: uninstall target $Target"
  'Plan: restore Wi-Fi, remove service, driver certificate, firewall rules, app TAP adapter, shell registrations and application files.'
  'Received files and shared .NET runtimes will be preserved. No processes, files, registry or drivers changed.'
  exit 0
}
$admin = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $admin.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { Write-Error 'Run uninstall.ps1 as administrator.'; exit 1 }
$Service = 'NekoDropService'
$CertSubject = 'CN=nekodrop driver (signed on this PC)'
$Log = Join-Path $env:ProgramData 'nekodrop-uninstall.log'
"nekodrop uninstall $(Get-Date -Format s), target $Target" | Set-Content $Log -ErrorAction SilentlyContinue
$failures = 0
function Try-Step($what, [scriptblock]$do) {
  $what
  try { $ErrorActionPreference = 'Stop'; & $do 2>&1 | ForEach-Object { "  $_" } | Add-Content $Log -ErrorAction Stop }
  catch { $script:failures++; "  FAILED: $what - $($_.Exception.Message)" | Tee-Object -Append $Log }
}

Try-Step 'Giving the Wi-Fi card back' {
  if (Get-Service $Service -ErrorAction SilentlyContinue) {
    Stop-Service $Service -Force -ErrorAction SilentlyContinue  # the card is given back below either way
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Service $Service).Status -ne 'Stopped' -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
  }
  # Everything running from the install folder, the AirDrop engine's python.exe too (#22).
  $root = [IO.Path]::GetFullPath($Target).TrimEnd('\') + '\'
  # Never by path under a drive root or a folder that is not nekodrop's.
  $byPath = $root.Length -gt 4 -and ((Test-Path (Join-Path $root 'NekoDropService.exe')) -or (Test-Path (Join-Path $root 'python\python.exe')))
  @(Get-Process AetherDrop, nekodrop, nekodrop-radio, nekodrop-radio-rtl, awdld -ErrorAction SilentlyContinue) +
  @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
      try { $byPath -and $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) } catch { $false } }) |
    Stop-Process -Force -ErrorAction SilentlyContinue
  # Processes started from elsewhere that name the folder on their command
  # line: airdrop-beacon.ps1's powershell.exe held airdrop\ open (#22).
  @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
      $byPath -and $_.ProcessId -ne $PID -and $_.CommandLine -and
      $_.CommandLine.IndexOf($root.TrimEnd(''), [StringComparison]::OrdinalIgnoreCase) -ge 0 }) |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  $svc = Join-Path $Target 'NekoDropService.exe'
  if (Test-Path $svc) { & $svc --give-back; if ($LASTEXITCODE -ne 0) { throw "Wi-Fi restore exited $LASTEXITCODE" } }
  else {
    # A failed update can leave the folder without the service: restore.ps1
    # gives the card back on its own, from the same folder or this script's.
    $restore = @((Join-Path $Target 'restore.ps1'), (Join-Path $PSScriptRoot 'restore.ps1')) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($restore) { & $restore } else { throw "neither NekoDropService.exe nor restore.ps1 is in $Target" }
  }
}

Try-Step 'Service' {
  if (Get-Service $Service -ErrorAction SilentlyContinue) {
    sc.exe delete $Service | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe delete exited $LASTEXITCODE" }
  }
}

Try-Step 'Driver package and certificate' {
  $store = [xml](pnputil /enum-drivers /format xml | Out-String)
  if ($LASTEXITCODE -ne 0) { throw "pnputil /enum-drivers exited $LASTEXITCODE" }
  $store.PnpUtil.Driver | Where-Object { $_.OriginalName -eq 'winawdl_regproxy.inf' -and $_.ProviderName -eq 'winairdrop project' } |
    ForEach-Object {
      pnputil /delete-driver $_.DriverName /uninstall /force | Out-Null
      if ($LASTEXITCODE -ne 0) { throw "pnputil /delete-driver $($_.DriverName) exited $LASTEXITCODE" }
    }
  foreach ($s in 'Root', 'TrustedPublisher') {
    Get-ChildItem "Cert:\LocalMachine\$s" | Where-Object { $_.Subject -eq $CertSubject } | Remove-Item -ErrorAction Stop
  }
}

Try-Step 'Firewall' {
  Get-NetFirewallRule -Group 'nekodrop AirDrop' -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction Stop
}

# Only the adapter setup-tap.ps1 made and named "nekodrop"; the TAP-Windows
# driver stays, other programs (VPNs) may use it (#22).
Try-Step 'TAP adapter' {
  foreach ($a in @(Get-NetAdapter -Name 'nekodrop' -IncludeHidden -ErrorAction SilentlyContinue |
                   Where-Object { $_.InterfaceDescription -like 'TAP-Windows Adapter V9*' })) {
    if ($a.PnPDeviceID) {
      pnputil /remove-device $a.PnPDeviceID | Out-Null
      if ($LASTEXITCODE -ne 0) { throw "pnputil /remove-device exited $LASTEXITCODE" }
    }
  }
}

Try-Step 'Files' {
  Get-AppxPackage -AllUsers -Name nekodrop.shell -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
  $client = Join-Path $Target 'AetherDrop.exe'
  if (Test-Path $client) { & $client --unregister; if ($LASTEXITCODE -ne 0) { throw "Shell unregister exited $LASTEXITCODE" } }
  foreach ($dir in $Target, "$env:ProgramData\nekodrop", "$env:LOCALAPPDATA\nekodrop") {
    if (Test-Path -LiteralPath $dir) {
      if ($FromInstaller -and $dir -eq $Target) {
        # Inno Setup is running its own uninstaller from this folder.
        Get-ChildItem -LiteralPath $dir -Force | Where-Object { $_.Name -notlike 'unins*' } |
          ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop }
      } else { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction Stop }
    }
  }
}
if ($failures) { "AetherDrop uninstall incomplete: $failures step(s) failed. See $Log"; exit 1 } else { 'AetherDrop is removed.'; exit 0 }
