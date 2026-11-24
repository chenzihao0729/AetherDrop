# Build and run the offline UI simulator directly from source.
param([ValidateSet('zh','en','auto')][string]$Language = 'zh', [switch]$Watch, [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\dev-env.ps1"
$repoDir = Split-Path $PSScriptRoot -Parent
$sdkDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet-sdk'
$dotnetExe = Join-Path $sdkDir 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExe)) { throw '.NET SDK is missing. Install .NET 8 SDK first.' }
$env:DOTNET_ROOT = $sdkDir
$env:DOTNET_ROOT_X64 = $sdkDir
$project = Join-Path $repoDir 'app\client\AetherDrop.csproj'
if ($Watch) {
    $env:DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER = '1'
    & $dotnetExe watch --project $project run --configuration Debug --no-launch-profile -- --ui-debug --lang $Language
} else {
    $runArgs = @('run','--project',$project,'--configuration','Debug','--no-launch-profile')
    if ($NoBuild) { $runArgs += '--no-build' }
    & $dotnetExe @runArgs -- --ui-debug --lang $Language
}
exit $LASTEXITCODE
