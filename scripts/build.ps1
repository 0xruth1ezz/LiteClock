param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [switch]$NoPublish
)
$ErrorActionPreference = 'Stop'
$clockRoot = Split-Path $PSScriptRoot -Parent
$clockSdk = Get-Command dotnet -ErrorAction SilentlyContinue
$clockDotnet = if ($clockSdk) { $clockSdk.Source } else { Join-Path $clockRoot '.tools/dotnet/dotnet.exe' }
if (-not (Test-Path -LiteralPath $clockDotnet)) {
    throw '请先安装 .NET 8 SDK（或更新版本），然后重新运行 scripts\build.ps1。https://dotnet.microsoft.com/download/dotnet/8.0'
}
$clockProject = Join-Path $clockRoot 'src/LiteClock/LiteClock.csproj'
if ($NoPublish) {
    & $clockDotnet build $clockProject -c $Configuration "-p:Platform=$Platform"
} else {
    $clockOutput = Join-Path $clockRoot "dist/LiteClock-$Platform"
    & $clockDotnet publish $clockProject -c $Configuration "-p:Platform=$Platform" -o $clockOutput
}
if ($LASTEXITCODE -ne 0) { throw 'LiteClock WinUI 3 build failed.' }
if (-not $NoPublish) {
    # Import a legacy profile only on the first build. Never replace a newer profile.
    $clockProfile = Join-Path $clockRoot 'settings.json'
    $clockPublishedProfile = Join-Path $clockOutput 'settings.json'
    if ((Test-Path -LiteralPath $clockProfile) -and -not (Test-Path -LiteralPath $clockPublishedProfile)) {
        Copy-Item -LiteralPath $clockProfile -Destination $clockPublishedProfile
    }
    Write-Output ("Built WinUI 3 app: " + (Join-Path $clockOutput 'LiteClock.exe'))
}
