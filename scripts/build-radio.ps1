# Builds the Intel radio daemon (winawdl_radio.exe) and its self-test (MSVC by
# default, -Clang for clang-cl), copies the firmware of every card it knows
# (iwxradio/src/cards.h: Qu/QuZ hr+jf, cc, so, ty, with their PNVMs) beside it
# and runs the self-test.
param([switch]$Clang)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\toolchain.ps1"
$repo = Split-Path $PSScriptRoot -Parent
$tools = Initialize-Toolchain
$compiler = $tools.Cl
if ($Clang) {
  if (-not (Test-Path $tools.ClangCl)) { throw "clang-cl not found at $($tools.ClangCl)" }
  $compiler = $tools.ClangCl
}
$build = "$repo\build\iwxradio"
& $tools.CMake -S "$repo\iwxradio" -B $build -G Ninja "-DCMAKE_MAKE_PROGRAM=$($tools.Ninja)" `
  "-DCMAKE_CXX_COMPILER=$compiler" -DCMAKE_BUILD_TYPE=Release
if ($LASTEXITCODE -ne 0) { throw "cmake configure failed ($LASTEXITCODE)" }
& $tools.CMake --build $build
if ($LASTEXITCODE -ne 0) { throw "build failed ($LASTEXITCODE)" }

$fwSource = "$repo\third_party\ForbiddenItlwm\itlwm\firmware"
$fwDest = "$build\firmware"
New-Item -ItemType Directory -Force $fwDest | Out-Null
foreach ($f in 'iwlwifi-Qu*-68.ucode', 'iwlwifi-cc-a0-68.ucode', 'iwlwifi-so-a0-*-68.ucode', 'iwlwifi-ty-a0-*-68.ucode', 'iwlwifi-*.pnvm') {
  Get-ChildItem $fwSource -Filter $f | Copy-Item -Destination $fwDest -Force
}
$be200Fw = "$repo\third_party\linux-firmware-iwlwifi-be200\iwlwifi-gl-c0-fm-c0-100.ucode"
if (Test-Path $be200Fw) {
  Copy-Item $be200Fw $fwDest -Force
}
Copy-Item "$repo\third_party\ForbiddenItlwm\iwlwifi-firmware-license" "$fwDest\LICENSE.iwlwifi-firmware" -Force

& "$build\iwx_selftest.exe" $fwDest
if ($LASTEXITCODE -ne 0) { throw 'self-test failed' }
Get-ChildItem $build -Filter *.exe | Select-Object Name, Length
