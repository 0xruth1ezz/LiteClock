param([string]$ExePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'dist/LiteClock-x64/LiteClock.exe'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ExePath)) { throw '请先运行 .\build.ps1。' }
$clockReport = Join-Path $PSScriptRoot 'self-test-results.txt'
$clockProcess = Start-Process -FilePath $ExePath -ArgumentList ('--self-test "' + $clockReport + '"') -WindowStyle Hidden -PassThru
if (-not $clockProcess.WaitForExit(60000)) {
    Stop-Process -Id $clockProcess.Id
    throw 'Core tests timed out after 60 seconds.'
}
$clockProcess.Refresh()
Get-Content -LiteralPath $clockReport
if ($clockProcess.ExitCode -ne 0) { throw 'Core tests failed.' }
& (Join-Path $PSScriptRoot 'run-number-tests.ps1') -ExePath $ExePath
