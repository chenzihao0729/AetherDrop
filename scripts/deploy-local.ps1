#Requires -RunAsAdministrator
# Development: puts a freshly built radio and service from build\stage into an
# installed AetherDrop and restarts the service. Logs to build\deploy.log.
param([string]$Target = "$env:ProgramFiles\AetherDrop")
$repo = Split-Path $PSScriptRoot -Parent
Start-Transcript "$repo\build\deploy.log" -Force | Out-Null
try {
  Stop-Service AetherDropService -Force -ErrorAction SilentlyContinue
  Stop-Service NekoDropService -Force -ErrorAction SilentlyContinue
  Get-Process AetherDropService, NekoDropService, AetherDrop-radio, awdld -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep 2
  Copy-Item "$repo\build\stage\radio\AetherDrop-radio.exe" "$Target\radio\" -Force
  Copy-Item "$repo\build\stage\AetherDropService.exe" "$Target\" -Force
  # The client and the AirDrop engine too; the user's client is started again afterwards.
  Get-Process AetherDrop, AetherDrop -ErrorAction SilentlyContinue | Stop-Process -Force
  Get-CimInstance Win32_Process -Filter "Name='python.exe'" | Where-Object { $_.ExecutablePath -like "$Target*" } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Start-Sleep 1
  Copy-Item "$repo\build\stage\AetherDrop.exe" "$Target\" -Force
  Copy-Item "$repo\build\stage\airdrop\*.py" "$Target\airdrop\" -Force
  # The receiver's spare ports (8772-8779) beside 8771.
  Get-NetFirewallRule -DisplayName 'AetherDrop AirDrop receiver' -ErrorAction SilentlyContinue |
    Set-NetFirewallRule -LocalPort 8771-8779
  if (Get-Service NekoDropService -ErrorAction SilentlyContinue) {
    & "$env:SystemRoot\System32\sc.exe" delete NekoDropService | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not remove legacy service' }
  }
  if (-not (Get-Service AetherDropService -ErrorAction SilentlyContinue)) {
    New-Service -Name AetherDropService -BinaryPathName "`"$Target\AetherDropService.exe`"" -DisplayName 'AetherDrop AirDrop' -StartupType Automatic | Out-Null
  }
  Set-Service AetherDropService -StartupType Automatic
  Start-Service AetherDropService
  Get-Item "$Target\radio\AetherDrop-radio.exe", "$Target\AetherDropService.exe" | Select-Object Name, LastWriteTime
  'deployed'
} catch { $_ | Out-String }
Stop-Transcript | Out-Null
