# Settings UI

SettingsWindow uses the floating popup's light blue/white palette, Microsoft
YaHei text, transparent-background logo and rounded surfaces independently of
the Windows dark/light theme. SettingsStyles.xaml owns the local control styles;
other application windows keep their existing theme.

The header has native vector close/minimize buttons and a draggable empty area.
The window remains resizable (minimum 520 by 500 DIPs), while its initial height
is bounded to the Windows work area. Native WindowChrome supplies resize edges;
there is no transparent-window text rasterization, text scaling or Mica layer.

All existing named fields and event handlers remain connected. Sections scroll
between a fixed header and fixed Save/Cancel footer. Input fields, radio/check
controls, language/card selectors, list and scrollbar use matching light styles.
Save semantics are unchanged; Cancel discards UI edits.

Verification: UiDebug.Smoke checks the local palette, actual language popup,
bottom-section reachability, stationary footer, minimum-size layout and Cancel
without production settings/registry changes. Set AETHERDROP_SETTINGS_CAPTURE_DIR
to save native top/bottom/compact screenshots. Typography.Smoke verifies native
Microsoft YaHei faces across the redesigned controls.
