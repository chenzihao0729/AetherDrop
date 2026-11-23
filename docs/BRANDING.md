# AetherDrop branding

The client executable and product name are **AetherDrop**. The supplied
`AirDrop_logo.png` is preserved as `app/assets/AetherDrop-logo.png`.

## Windows icon assets

- `app/assets/AetherDrop.ico`: desktop, taskbar, window, installer and executable
  icon. Contains 32-bit alpha frames at 16, 20, 24, 28, 32, 40, 48, 56, 64, 72,
  80, 96, 128, 192 and 256 pixels.
- `app/assets/AetherDrop-tray.ico`: original logo including its background for the notification
  area, with 16, 20, 24, 28, 32, 40, 48 and 64 pixel frames.
- `app/assets/AirDrop_logo_none.png`: supplied transparent logo used only in the tray popup header.
- `app/assets/icons/`: individual ICO sizes and desktop PNG previews.
- `app/assets/logo150.png` and `logo44.png`: Windows shell package assets.

All ICO frames use 32-bit DIB data and transparency masks. WPF resources are
embedded into `AetherDrop.exe`; no external icon file is needed at runtime.

To regenerate the assets with Python and Pillow:

```powershell
python scripts/generate-icons.py app/assets/AetherDrop-logo.png
```

## Compatibility

The original service name, radio process names, driver identifiers, pipe names,
settings storage and virtual adapter name remain unchanged so the branding
change can reuse existing hardware and settings. The visible service name,
notifications, Explorer verbs, shortcuts and installer product name use
AetherDrop. Main program filename: `AetherDrop.exe`.

Upstream automatic updates are disabled until an AetherDrop release feed is
configured, so an upstream installer cannot replace the branded build.
Upstream source attribution and original licenses remain intact.
