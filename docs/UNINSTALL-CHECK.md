# Uninstaller validation

Windows PowerShell 5.1 parsing, `-SelfTest`, `-DryRun -Target <installation>`
and rejection of a drive-root target were checked without real uninstall.
The script now derives its default target from its installed location when
possible, validates absolute paths/application markers and protected directories,
refuses targets containing the configured received-file location, reports step
failures and returns a nonzero exit status on incomplete removal.

Administrator rights are checked after the read-only switches so validation
does not require or trigger system removal. External Wi-Fi restore, driver
enumeration and client unregister exit codes are checked. `-FromInstaller`
preserves Inno Setup's own `unins*` files while its process is running.

The actual end-to-end removal of service, driver, certificates, adapter and files
was deliberately not executed during validation. Administrative permissions,
locked files and the machine's installed driver state can still affect removal;
step errors are written to `%ProgramData%\nekodrop-uninstall.log`.

Usage (PowerShell):

```powershell
.\uninstall.ps1 -SelfTest
.\uninstall.ps1 -DryRun -Target 'C:\Program Files\AetherDrop'
# Actual removal requires an administrator terminal:
.\uninstall.ps1 -Target 'C:\Program Files\AetherDrop'
```
