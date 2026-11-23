# Main window

The main window shares the light popup/settings palette and SettingsStyles.xaml
controls. It retains all existing request, device, file-drop, history, folder,
settings, Wi-Fi and report handlers. MainWindow owns local blue progress bars
and device-tile styles. File names stay hidden; selected files show a count.

The native WindowChrome header shows the real machine name and supports drag,
minimize, maximize/restore and close. Header/footer stay fixed around a scrolling
body; minimum size is 540 by 520 DIPs. The initial height is work-area bounded.
No Mica or scaling is applied. Empty-state labels refresh with the observable
collections, including the received-history collection.

UiDebugWindow includes a preview-main-window button that opens the real UI
without starting the engine. UiDebug.Smoke covers empty/populated states,
collection-driven updates, hidden names, Clear, and minimum-size footer layout.
Native screenshots are under build/main-ui-rehearsal when capture is enabled.
