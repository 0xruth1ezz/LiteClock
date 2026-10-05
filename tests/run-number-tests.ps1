param([string]$ResultPath = (Join-Path $PSScriptRoot 'number-input-results.txt'))
$ErrorActionPreference = 'Stop'
$clockProject = Split-Path $PSScriptRoot -Parent
$clockFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$clockTestDir = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $clockTestDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $clockProject 'LiteClock.exe') -Destination (Join-Path $clockTestDir 'LiteClock.exe') -Force
$clockArgs = @('/nologo','/target:winexe','/codepage:65001',('/out:' + (Join-Path $clockTestDir 'NumberInputTests.exe')),('/reference:' + (Join-Path $clockTestDir 'LiteClock.exe')))
foreach ($clockReference in @('System.dll','System.Core.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationProvider.dll','WPF\UIAutomationTypes.dll')) { $clockArgs += '/reference:' + (Join-Path $clockFramework $clockReference) }
$clockArgs += Join-Path $PSScriptRoot 'NumberInputTests.cs'
& (Join-Path $clockFramework 'csc.exe') @clockArgs
if ($LASTEXITCODE -ne 0) { throw 'Number input test build failed.' }
$clockTest = Start-Process -FilePath (Join-Path $clockTestDir 'NumberInputTests.exe') -ArgumentList ('"' + [IO.Path]::GetFullPath($ResultPath) + '"') -WindowStyle Hidden -PassThru -Wait
Get-Content -LiteralPath $ResultPath
if ($clockTest.ExitCode -ne 0) { throw 'Number input tests failed.' }
