# Dot-source this file to enable the project's local development tools.
$repoDir = Split-Path $PSScriptRoot -Parent
$sdkDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet-sdk'
$pythonDir = Join-Path $repoDir 'build\python'
$depsDir = Join-Path $repoDir 'third_party'
$paths = @($sdkDir, $pythonDir, "$depsDir\portable-git\cmd", "$depsDir\portable-git\ucrt64\bin", "$depsDir\winlibs\mingw64\bin", "$depsDir\vcpkg")
$available = @($paths | Where-Object { Test-Path -LiteralPath $_ })
$remaining = @($env:PATH.Split(';') | Where-Object { $_ -and $_ -notin $available })
$env:PATH = (@($available) + @($remaining)) -join ';'
if (Test-Path -LiteralPath "$depsDir\vcpkg\vcpkg.exe") {
    $env:VCPKG_ROOT = "$depsDir\vcpkg"
}
# Upstream C++ headers are UTF-8; avoid CP936 warnings becoming build errors.
if ($env:CL -notmatch '(?i)(^|\s)/utf-8(\s|$)') { $env:CL = ($env:CL + ' /utf-8').Trim() }
Write-Output 'Local development tools enabled for this PowerShell session.'
