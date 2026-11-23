#Requires -RunAsAdministrator
# Development: puts a freshly built radio and service from build\stage into an
# installed nekodrop and restarts the service. Logs to build\deploy.log.
param([string]$Target = "$env:ProgramFiles\AetherDrop")
$repo = Split-Path $PSScriptRoot -Parent
Start-Transcript "$repo\build\deploy.log" -Force | Out-Null
try {
  Stop-Service NekoDropService -Force -ErrorAction SilentlyContinue
  Get-Process NekoDropService, nekodrop-radio, awdld -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep 2
  Copy-Item "$repo\build\stage\radio\nekodrop-radio.exe" "$Target\radio\" -Force
  Copy-Item "$repo\build\stage\NekoDropService.exe" "$Target\" -Force
  # The client and the AirDrop engine too; the user's client is started again afterwards.
  Get-Process AetherDrop, nekodrop -ErrorAction SilentlyContinue | Stop-Process -Force
  Get-CimInstance Win32_Process -Filter "Name='python.exe'" | Where-Object { $_.ExecutablePath -like "$Target*" } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Start-Sleep 1
  Copy-Item "$repo\build\stage\AetherDrop.exe" "$Target\" -Force
  Copy-Item "$repo\build\stage\airdrop\*.py" "$Target\airdrop\" -Force
  # The receiver's spare ports (8772-8779) beside 8771.
  Get-NetFirewallRule -DisplayName 'nekodrop AirDrop receiver' -ErrorAction SilentlyContinue |
    Set-NetFirewallRule -LocalPort 8771-8779
  Set-Service NekoDropService -StartupType Automatic
  Start-Service NekoDropService
  Get-Item "$Target\radio\nekodrop-radio.exe", "$Target\NekoDropService.exe" | Select-Object Name, LastWriteTime
  'deployed'
} catch { $_ | Out-String }
Stop-Transcript | Out-Null
