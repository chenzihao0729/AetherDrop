#Requires -RunAsAdministrator
# Hardware rounds for the radio daemon. -Quick: one short listen on channel 6.
param([switch]$Quick, [switch]$Tx, [switch]$Kinds)
$runs = if ($Kinds) { @('AetherDrop-radio-mt.exe|6 --test-kinds 20') } elseif ($Tx) { @('AetherDrop-radio-mt.exe|44 --test-tx 4') } elseif ($Quick) { @('AetherDrop-radio-mt.exe|6 --test-rx 6') } else { @(
  'AetherDrop-radio-mt.exe|6 --test-rx 8',
  'AetherDrop-radio-mt.exe|149 --test-rx 12',
  'AetherDrop-radio-mt.exe|44 --test-rx 12',
  'AetherDrop-radio-mt.exe|6 --test-tx 5') }
# An installed AetherDrop owns the card: stop its service for the test, start it again after.
$svc = Get-Service AetherDropService -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq 'Running') { Stop-Service AetherDropService -Force; Start-Sleep 3 }
try { & (Join-Path $PSScriptRoot 'smoke-test.ps1') -Step id -Runs $runs }
finally { if ($svc) { Start-Service AetherDropService } }
