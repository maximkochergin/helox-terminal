$ErrorActionPreference = 'Stop'
& (Join-Path (Split-Path $PSScriptRoot -Parent) 'build.ps1')
$executable = Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\helox.exe'
& $executable selftest
if ($LASTEXITCODE -ne 0) { throw 'selftest failed' }
$beforeCheck = & $executable status --json | ConvertFrom-Json
& $executable check
if ($LASTEXITCODE -ne 0) { throw 'analysis checks failed' }
$afterCheck = & $executable status --json | ConvertFrom-Json
if (($beforeCheck.windows | ConvertTo-Json -Compress) -ne ($afterCheck.windows | ConvertTo-Json -Compress)) { throw 'analysis checks changed windows settings' }
$status = & $executable status --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $null -eq $status.windows.Speed) { throw 'status contract failed' }
if ($null -ne $status.hardwareDpi -or $null -ne $status.hardwarePollingHz) { throw 'unsupported hardware values must remain null' }
if ($status.receiver.Path) {
    $historyPath = Join-Path $env:LOCALAPPDATA 'helox-terminal\rate.json'
    $historyBytes = if (Test-Path -LiteralPath $historyPath) { [IO.File]::ReadAllBytes($historyPath) } else { $null }
    try {
        New-Item -ItemType Directory -Force -Path (Split-Path $historyPath -Parent) | Out-Null
        [IO.File]::WriteAllText($historyPath,(@{DevicePath=$status.receiver.Path;ActiveHz=0;MeasuredUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Compress))
        $damagedHistory = & $executable status --json | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or $null -ne $damagedHistory.lastRate) { throw 'status accepted incomplete rate history' }
    } finally {
        if ($null -eq $historyBytes) { if (Test-Path -LiteralPath $historyPath) { Remove-Item -LiteralPath $historyPath } }
        else { [IO.File]::WriteAllBytes($historyPath,[byte[]]$historyBytes) }
    }
}
& $executable set speed 0 --json | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'invalid argument exit code failed' }
& $executable set wheel -1 --json | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'negative wheel must require the page keyword' }
$menu = "0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($menu -join "`n") -notmatch '1  acceleration') { throw 'numeric menu failed' }
if (@($menu).Count -gt 18) { throw 'home screen too long' }
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
$missingName = 'missing-' + [guid]::NewGuid().ToString('n').Substring(0,20)
$missingProfile = & $executable profile apply $missingName --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 1 -or $missingProfile.error -ne 'profile not found / use profile list') { throw 'missing profile recovery message failed' }
$exitArgs = "exit extra`nhelp`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($exitArgs -join "`n") -notmatch 'unknown choice' -or ($exitArgs -join "`n") -notmatch 'advanced|gradual fast-motion') { throw 'invalid exit arguments must not close the terminal' }
$dpiJson = & $executable dpi --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 1 -or !$dpiJson.error) { throw 'interactive dpi must reject json mode' }
$dossierScreen = "7`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($dossierScreen -join "`n") -notmatch 'unavailable / sensor dpi') { throw 'status menu navigation failed' }
if ($status.receiver.TrustCandidate -and ($null -eq $status.dossier.Model.LengthMm -or !$status.dossier.Hid)) { throw 'dossier device metadata missing' }
$aimStatus = & $executable aim status --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or !$aimStatus.State) { throw 'aim status contract failed' }
$badAim = & $executable aim smooth maybe --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 1 -or !$badAim.error) { throw 'aim invalid toggle validation failed' }
if ($aimStatus.State -ne 'ready') {
    $missingDriver = & $executable aim precision on --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 1 -or !$missingDriver.error -or $missingDriver.applied) { throw 'absent backend must not claim applied' }
    $missingResume = & $executable aim resume --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 1 -or !$missingResume.error -or $missingResume.applied) { throw 'resume requires an active driver' }
}
$aimMenu = "8`n0`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($aimMenu -join "`n") -notmatch 'precision on') { throw 'aim menu navigation failed' }
$devices = & $executable devices --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'device list failed' }
if ($devices.Count -gt 0) {
    $selectFirst = "6`n1`n1`nstatus`n0" | & $executable
    $firstName = if ($devices[0].Product) { $devices[0].Product.ToLowerInvariant() } else { 'mouse device' }
    if ($LASTEXITCODE -ne 0 -or ($selectFirst -join "`n") -notmatch ('selected / ' + [regex]::Escape($firstName))) { throw 'first mouse menu selection failed' }
    $cancelMouse = "6`n1`n0`n0" | & $executable
    if ($LASTEXITCODE -ne 0 -or ($cancelMouse -join "`n") -match 'selected / ') { throw 'mouse menu cancel changed selection' }
}
$dataRoot = Join-Path $env:LOCALAPPDATA 'helox-terminal'
$recoveryFiles = @{}
foreach ($file in @('original.json', 'undo.json')) {
    $path = Join-Path $dataRoot $file
    $recoveryFiles[$path] = if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllBytes($path) } else { $null }
}
$profileName = 'test-' + [guid]::NewGuid().ToString('n').Substring(0,24)
$profilePath = Join-Path $dataRoot ('profiles\' + $profileName + '.json')
$beforeUndo = & $executable status --json | ConvertFrom-Json
try {
    & $executable profile save $profileName --json | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'locked profile save failed' }
    $preview = & $executable profile show $profileName --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or ($preview | ConvertTo-Json -Compress) -ne ($beforeUndo.windows | ConvertTo-Json -Compress)) { throw 'profile preview contract failed' }
    $afterPreview = & $executable status --json | ConvertFrom-Json
    if (($afterPreview.windows | ConvertTo-Json -Compress) -ne ($beforeUndo.windows | ConvertTo-Json -Compress)) { throw 'profile preview changed settings' }
    $profileNames = & $executable profile list --json | ConvertFrom-Json
    $profileNumber = [array]::IndexOf(@($profileNames),$profileName) + 1
    $menuPreview = "5`n5`n$profileNumber`n0" | & $executable
    if ($LASTEXITCODE -ne 0 -or ($menuPreview -join "`n") -notmatch ('profile / ' + $profileName) -or ($menuPreview -join "`n") -match 'applied /') { throw 'profile preview menu failed' }
    $testSpeed = if ($beforeUndo.windows.Speed -eq 10) { 11 } else { 10 }
    & $executable set speed $testSpeed --json | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'undo test change failed' }
    $undone = & $executable undo --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$undone.undone -or ($undone.readback | ConvertTo-Json -Compress) -ne ($beforeUndo.windows | ConvertTo-Json -Compress)) { throw 'undo did not restore previous settings' }
    & $executable set speed $beforeUndo.windows.Speed --json | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'undo no-op test failed' }
    $redone = & $executable undo --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $redone.readback.Speed -ne $testSpeed) { throw 'no-op replaced undo or repeated undo failed' }
} finally {
    & $executable set speed $beforeUndo.windows.Speed --json | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'undo test settings restore failed' }
    foreach ($path in $recoveryFiles.Keys) {
        if ($null -eq $recoveryFiles[$path]) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path } }
        else { [IO.File]::WriteAllBytes($path, [byte[]]$recoveryFiles[$path]) }
    }
    if (Test-Path -LiteralPath $profilePath) { Remove-Item -LiteralPath $profilePath }
}
Write-Host 'passed / command status contract, profile preview, undo and validation'
