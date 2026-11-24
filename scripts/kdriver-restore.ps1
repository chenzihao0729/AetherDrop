#Requires -RunAsAdministrator
# Gives the AX201 back to Intel's driver and removes the AetherDrop kernel
# driver package. Safe to run more than once.
$ErrorActionPreference = 'Continue'
$state = Join-Path $env:ProgramData 'AetherDrop'
$dev = Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -match 'PCI\\VEN_8086&DEV_(43F0|A0F0|02F0|06F0|34F0|3DF0|4DF0)' } |
       Select-Object -First 1
if (-not $dev) { 'No Intel AX201-family card found.'; return }
$vendor = Get-Content (Join-Path $state 'vendor-inf.txt') -ErrorAction SilentlyContinue
if (-not $vendor) {
  # Intel's package in the driver store: provider Intel, class Net, Netwtw*.
  $store = [xml](pnputil /enum-drivers /format xml | Out-String)
  $vendor = $store.PnpUtil.Driver | Where-Object { $_.ProviderName -match 'Intel' -and $_.ClassName -eq 'Net' } |
            Sort-Object DriverVersion -Descending | Select-Object -First 1 -ExpandProperty DriverName
  if ($vendor) { $vendor = Join-Path $env:windir "INF\$vendor" }
}
if (-not $vendor) { 'Intel''s driver package was not found; reinstall it from Intel or Windows Update.'; return }
"giving the card back to $vendor"
Add-Type -Namespace Neko -Name NewDev -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("newdev.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
public static extern bool UpdateDriverForPlugAndPlayDevicesW(System.IntPtr hwnd, string hardwareId, string infPath, uint flags, out bool reboot);
'@
$hwid = ($dev.InstanceId -split '&SUBSYS')[0]
$reboot = $false
if (-not [Neko.NewDev]::UpdateDriverForPlugAndPlayDevicesW([IntPtr]::Zero, $hwid, $vendor, 1, [ref]$reboot)) {
  "binding Intel's driver failed: $([ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error()).Message)"
}
$store = [xml](pnputil /enum-drivers /format xml | Out-String)
$store.PnpUtil.Driver | Where-Object { $_.OriginalName -eq 'AetherDrop-wlan.inf' } |
  ForEach-Object { pnputil /delete-driver $_.DriverName /force | Out-Null; "removed $($_.DriverName)" }
Start-Sleep 3
$now = Get-PnpDevice -InstanceId $dev.InstanceId
"device now: $($now.FriendlyName), status $($now.Status)$(if ($reboot) { ' (reboot requested)' })"
