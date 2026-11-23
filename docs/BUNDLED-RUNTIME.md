# Bundled .NET runtime

The Windows x64 client now builds self-contained by default, so Explorer/Send To
can launch its apphost without inheriting the development shell's DOTNET_ROOT.
Published installation payloads keep the existing compressed single-file mode,
including native libraries for extraction. Both the client and service embed
their runtime and do not require a separately installed .NET runtime.

The refreshed 0.1.35 payload includes Microsoft.NETCore.App 8.0.34 and
Microsoft.WindowsDesktop.App 8.0.34 for the client. These versions are recorded
in the generated runtimeconfig includedFrameworks list. Runtime updates require
republishing the application with updated runtime packs.

Validation: published AetherDrop.exe renders all nine snapshot screens with
DOTNET_ROOT/DOTNET_ROOT_X64 pointing at a nonexistent folder, a Windows-only
PATH and DOTNET_MULTILEVEL_LOOKUP=0. Service --selftest also passes in that
environment. The distributable ZIP contains the refreshed self-contained client
and service plus the previously staged radio/driver/Python dependencies.
