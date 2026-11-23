# Source and branding cleanup

Reviewed app/client, app/assets, installer, scripts, docs and tests against
project resources, code references and packaging scripts.

Removed:
- app/assets/AetherDrop-tray.png: byte-identical SHA-256 to AetherDrop-logo.png,
  no runtime or packaging reference. The generator no longer emits the duplicate.
- scripts/__pycache__/patch-engine.cpython-315.pyc: reproducible Python bytecode;
  source patch-engine.py remains. Python cache patterns added to .gitignore.

Legacy app/assets/nekodrop.ico and nekodrop.png were already absent before this
cleanup. The old PingFangSC directory is also absent; typography docs corrected.
All current source image assets represent AetherDrop branding.

Retained intentionally:
- logo150.png/logo44.png: used by the shell package manifest/build script.
- assets/icons: size-specific exports explicitly requested for Windows icons.
- AetherDrop.ico, tray ICO, main/logo-none PNG: application and shell resources.
- third_party, toolchains and build: dependencies, live outputs and diagnostic
  artifacts; absence of direct UI references does not make them unused source.
- Internal NekoDrop namespaces, service/pipe/protocol/adapter names, migration
  paths and upstream repository/update identifiers: compatibility dependencies.
- Original license, history and attribution documentation.

Chinese display strings remove extra spacing around 隔空投送. The Wi-Fi helper
copy no longer exposes the old adapter brand, while internal adapter discovery
continues using its actual name.
