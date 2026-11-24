@echo off
setlocal
rem Use a process-only execution policy; never change the machine/user policy.
set "AetherDropPowerShell=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "AetherDropPowerShell=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
rem Read the whole block before uninstall.ps1 removes this launcher from disk.
(
  "%AetherDropPowerShell%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1" -NoPause %*
  if errorlevel 1 (
    echo.
    echo Uninstall failed. Review the error above and the log path shown.
    echo For actual removal, right-click uninstall.cmd and choose Run as administrator.
    pause
    exit /b 1
  )
  echo.
  pause
  exit /b 0
)
