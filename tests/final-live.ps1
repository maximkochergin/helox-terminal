# Run locally with games closed. Temporarily writes driver, Windows and owned HKCU Run value, then restores all.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$executable = Join-Path $projectRoot 'bin\helox.exe'
$probe = Join-Path $projectRoot 'bin\final-live-probe.exe'
& $compiler /nologo /target:exe /platform:anycpu "/reference:$executable" /reference:System.Web.Extensions.dll "/out:$probe" (Join-Path $PSScriptRoot 'FinalLiveProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'final live probe build failed' }
& $probe
if ($LASTEXITCODE -ne 0) { throw 'final live verification failed' }
