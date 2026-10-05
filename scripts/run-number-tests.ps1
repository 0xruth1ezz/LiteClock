param(
    [string]$ResultPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/tests/number-input-results.txt'),
    [string]$ExePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'dist/LiteClock-x64/LiteClock.exe')
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ExePath)) { throw '请先运行 .\scripts\build.ps1。' }
$clockResult = [IO.Path]::GetFullPath($ResultPath)
New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($clockResult)) | Out-Null
$clockProfile = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/tests/profiles/' + [Guid]::NewGuid().ToString('N'))
$clockTestArgs = '--ui-test "' + $clockResult + '" --settings-dir "' + $clockProfile + '"'
$clockTest = Start-Process -FilePath $ExePath -ArgumentList $clockTestArgs -WindowStyle Hidden -PassThru
if (-not $clockTest.WaitForExit(60000)) {
    Stop-Process -Id $clockTest.Id
    throw 'WinUI tests timed out after 60 seconds.'
}
$clockTest.Refresh()
Get-Content -LiteralPath $clockResult
if ($clockTest.ExitCode -ne 0) { throw 'WinUI number input tests failed.' }
