$ErrorActionPreference = 'Stop'
$clockFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$clockReferences = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationProvider.dll','WPF\UIAutomationTypes.dll')
$clockArgs = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001',('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),('/out:' + (Join-Path $PSScriptRoot 'LiteClock.exe')))
$clockArgs += '/win32icon:' + (Join-Path $PSScriptRoot 'LiteClock.ico')
$clockArgs += '/resource:' + (Join-Path $PSScriptRoot 'LiteClock.ico') + ',LiteClock.AppIcon'
foreach ($clockReference in $clockReferences) { $clockArgs += '/reference:' + (Join-Path $clockFramework $clockReference) }
$clockArgs += (Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs').FullName
& (Join-Path $clockFramework 'csc.exe') @clockArgs
if ($LASTEXITCODE -ne 0) { throw 'LiteClock build failed.' }
Write-Output 'Built LiteClock.exe'
