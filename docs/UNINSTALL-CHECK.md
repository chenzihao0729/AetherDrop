# Uninstaller validation

Removal now begins with registered Inno Setup compiler installations. The
official uninstaller must exit successfully and its uninstall registration
must disappear before AetherDrop cleanup begins. The graphical uninstaller
performs the same preflight in `InitializeUninstall`, so a failure prevents
Inno's own file-removal phase as well. `-RemoveInnoOnly` runs just that
preflight; `-SelfTest` and `-DryRun` never uninstall either program.

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

Both `AetherDropService` and legacy `AetherDropService` are removed. The script
waits for terminated application processes and the client's `--unregister`
operation before deleting files, retries temporary file locks and writes a
consistent UTF-8 log. Native PowerShell is selected when launched by a 32-bit
host. The service catalog self-test and 32-bit uninstall self-test also passed.

The actual end-to-end removal of service, driver, certificates, adapter and files
was deliberately not executed during validation. Administrative permissions,
locked files and the machine's installed driver state can still affect removal;
step errors are written to `%ProgramData%\AetherDrop-uninstall.log`.

Usage (PowerShell):

```powershell
.\uninstall.ps1 -SelfTest
.\uninstall.ps1 -DryRun -Target 'C:\Program Files\AetherDrop'
# Actual removal requires an administrator terminal:
.\uninstall.ps1 -Target 'C:\Program Files\AetherDrop'
```
