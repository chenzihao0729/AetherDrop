# Microsoft YaHei UI fonts

All application-owned windows, popups, tooltips, the debug panel and tray menus
use the installed **Microsoft YaHei** family (微软雅黑).

- Body text, input fields and captions: native Normal weight.
- Titles, section headings, emphasized values and buttons: native Bold weight.

`Typography.cs` provides the family and semantic weights. No synthetic Medium,
Heavy or extra-light substitutions are requested. The Windows Forms tray menu
uses the same installed family. No PingFang font directory is currently present in the source assets; the
application uses the installed system family without bundled font files.

Device/file symbols keep their icon font; Windows-owned chrome and dialogs are
rendered by the operating system. Native layout, text hinting, separate shadows
and line-height insets remain in place to protect legibility and avoid clipping.

```powershell
. ./scripts/dev-env.ps1
dotnet run --project tests/Typography.Smoke/Typography.Smoke.csproj -c Release
```

The check verifies real Normal/Bold faces without synthetic weight, Chinese/
Latin/numeric coverage, all application window families and tray menu inheritance.
