$ErrorActionPreference = 'Stop'
& (Join-Path (Split-Path $PSScriptRoot -Parent) 'build.ps1')
$executable = Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\helox.exe'
& $executable selftest
if ($LASTEXITCODE -ne 0) { throw 'selftest failed' }
$status = & $executable status --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $null -eq $status.windows.Speed) { throw 'status contract failed' }
if ($null -ne $status.hardwareDpi -or $null -ne $status.hardwarePollingHz) { throw 'unsupported hardware values must remain null' }
& $executable set speed 0 --json | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'invalid argument exit code failed' }
Write-Host 'passed / command status contract and validation'
