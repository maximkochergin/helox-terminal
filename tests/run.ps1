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
& $executable set wheel -1 --json | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'negative wheel must require the page keyword' }
$menu = "0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($menu -join "`n") -notmatch '1  acceleration') { throw 'numeric menu failed' }
if (@($menu).Count -gt 16) { throw 'home screen too long' }
$navigation = "1`n0`n6`n3`nhome`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($navigation -join "`n") -notmatch 'game acceleration\?') { throw 'menu navigation failed' }
$invalidInput = "2`nwrong`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($invalidInput -join "`n") -notmatch 'enter a valid number') { throw 'input recovery failed' }
$cancel = "2`n`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or @($cancel | Select-String '1  acceleration').Count -ne 2) { throw 'cancel must redraw home menu' }
$unsupportedJson = & $executable faq --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 1 -or !$unsupportedJson.error) { throw 'unsupported json command must return a json error' }
$caseInsensitive = & $executable PROFILE LIST --JSON | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'command casing must be consistent' }
Write-Host 'passed / command status contract and validation'
