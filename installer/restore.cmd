@echo off
rem AetherDrop one-click rollback: gives the Wi-Fi card back to Intel's driver.
rem Double-click it; it asks for administrator rights. Log: %ProgramData%\AetherDrop\restore.log
net session >nul 2>&1
if %errorlevel% neq 0 (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '%*' -Verb RunAs"
  exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0restore.ps1" %*
echo.
pause
