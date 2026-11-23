# Appearance

The application selects light/dark appearance from Windows AppsUseLightTheme
at launch. Restart after changing the Windows app color mode. The diagnostic
environment variable AETHERDROP_APPEARANCE=light or dark can override the palette
for previews/tests without changing Windows preferences.

Palette.cs owns the shared semantic color equivalents. Theme.Build exposes
brush and gradient-color resources to WPF. Main/settings surfaces and their
control templates use dynamic palette resources; floating cards, toggles and
native tray-menu rendering use the same palette. Send/Wi-Fi pages reuse the
shared control styles. White button text, check marks and switch thumbs retain
their contrast; backgrounds, secondary text, borders and progress tracks adapt.
All dimensions, native corners and interaction animations are unchanged.

Validation: dark UiDebug.Smoke, FloatingUi.Smoke and Typography.Smoke; light
UiDebug.Smoke regression; native light/dark snapshots for main, settings, tray,
invitation, progress, completion, failure, send and Wi-Fi; native dark menu and
submenu captures. Tests do not change production settings or the OS theme.
