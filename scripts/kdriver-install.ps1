#Requires -RunAsAdministrator
# Installs the AetherDrop kernel driver (build\kdriver\package) on this PC and
# moves the AX201 to it. Needs test signing (bcdedit /set testsigning on, then
# a reboot), which this script checks but never changes.
#   1. remembers which INF the card uses now (Intel's), for kdriver-restore.ps1
#   2. signs the package with a certificate made here, trusts it, deletes its key
#   3. adds the package to the driver store and binds it to the card
# Log: %ProgramData%\AetherDrop\kdriver-install.log. Undo: kdriver-restore.ps1.
# The package: beside this script when installed, else the build output.
param([string]$Package = $(if (Test-Path (Join-Path $PSScriptRoot 'AetherDrop-wlan.inf')) { $PSScriptRoot }
                          else { Join-Path (Split-Path $PSScriptRoot -Parent) 'build\kdriver\package' }))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$state = Join-Path $env:ProgramData 'AetherDrop'
New-Item -ItemType Directory -Force $state | Out-Null
Start-Transcript (Join-Path $state 'kdriver-install.log') -Force | Out-Null
try {
  $boot = bcdedit /enum '{current}' | Out-String
  if ($boot -notmatch 'testsigning\s+Yes') {
    throw "Test signing is off. Run 'bcdedit /set testsigning on' as administrator, reboot, then run this again."
  }
  $dev = Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -match 'PCI\\VEN_8086&DEV_(43F0|A0F0|02F0|06F0|34F0|3DF0|4DF0)' } |
         Select-Object -First 1
  if (-not $dev) { throw 'No Intel AX201-family card found.' }
  $inf = (Get-PnpDeviceProperty -InstanceId $dev.InstanceId -KeyName DEVPKEY_Device_DriverInfPath).Data
  "card $($dev.InstanceId), driver INF now: $inf"
  if ($inf -and $inf -notmatch 'AetherDrop') { Set-Content (Join-Path $state 'vendor-inf.txt') $inf }

  $work = Join-Path $env:TEMP "AetherDrop-kdriver-$(Get-Random)"
  Copy-Item $Package $work -Recurse
  $subject = 'CN=AetherDrop driver (signed on this PC)'
  foreach ($store in 'Root', 'TrustedPublisher') {
    Get-ChildItem "Cert:\LocalMachine\$store" | Where-Object Subject -eq $subject | Remove-Item
  }
  $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject -CertStoreLocation Cert:\CurrentUser\My `
            -KeyExportPolicy NonExportable -HashAlgorithm SHA256 -NotAfter (Get-Date).AddYears(10)
  try {
    $cer = Join-Path $work 'AetherDrop-signing.cer'
    Export-Certificate -Cert $cert -FilePath $cer | Out-Null
    foreach ($store in 'Root', 'TrustedPublisher') { Import-Certificate -FilePath $cer -CertStoreLocation "Cert:\LocalMachine\$store" | Out-Null }
    foreach ($f in 'AetherDrop-wlan.sys', 'AetherDrop-wlan.cat') {
      $r = Set-AuthenticodeSignature -FilePath (Join-Path $work $f) -Certificate $cert -HashAlgorithm SHA256
      if ($r.Status -ne 'Valid') { throw "$f could not be signed: $($r.StatusMessage)" }
    }
  } finally {
    Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -DeleteKey
  }
  'signed'

  pnputil /add-driver (Join-Path $work 'AetherDrop-wlan.inf') | Out-Host
  if ($LASTEXITCODE -ne 0) { throw "pnputil could not add the package ($LASTEXITCODE)" }

  # Intel's driver ranks higher: bind ours the way devcon update does.
  Add-Type -Namespace Neko -Name NewDev -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("newdev.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
public static extern bool UpdateDriverForPlugAndPlayDevicesW(System.IntPtr hwnd, string hardwareId, string infPath, uint flags, out bool reboot);
'@
  $hwid = ($dev.InstanceId -split '&SUBSYS')[0]
  $reboot = $false
  if (-not [Neko.NewDev]::UpdateDriverForPlugAndPlayDevicesW([IntPtr]::Zero, $hwid, (Join-Path $work 'AetherDrop-wlan.inf'), 1, [ref]$reboot)) {
    throw "binding failed: $([ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error()).Message)"
  }
  Start-Sleep 5
  $now = Get-PnpDevice -InstanceId $dev.InstanceId
  "device now: $($now.FriendlyName), status $($now.Status), problem $($now.Problem)$(if ($reboot) { ' (reboot requested)' })"
  Get-NetAdapter | Where-Object InterfaceDescription -match 'AetherDrop' | Format-Table Name, InterfaceDescription, Status, MacAddress
  'Open Windows Wi-Fi settings: the AetherDrop adapter should list networks.'
} catch {
  $_ | Out-String
  'Failed. scripts\kdriver-restore.ps1 gives the card back to Intel.'
} finally {
  Stop-Transcript | Out-Null
}
