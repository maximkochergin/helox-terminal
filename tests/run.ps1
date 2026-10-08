$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
& (Join-Path (Split-Path $PSScriptRoot -Parent) 'build.ps1')
& (Join-Path $PSScriptRoot 'maintenance.ps1')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'uninstall.ps1')
if ($LASTEXITCODE -ne 0) { throw 'uninstall regression tests failed' }
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
$doctor=& $executable aim doctor --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $null -eq $doctor.BackendPrepared -or !$doctor.PSObject.Properties['KernelReadable'] -or !$doctor.PSObject.Properties['SelectedDeviceStack']) { throw 'driver doctor contract failed' }
if ($null -ne $doctor.SelectedDeviceStack) {
    if (!$doctor.SelectedDeviceStack.Source) { throw 'device stack source missing' }
    if ($doctor.SelectedDeviceStack.Error -and $null -ne $doctor.SelectedDeviceStack.RawAccelPresent) { throw 'unreadable pnp stack must remain unknown' }
    if (!$doctor.SelectedDeviceStack.Error -and (!$doctor.SelectedDeviceStack.InstanceId -or !$doctor.SelectedDeviceStack.Services)) { throw 'device stack evidence missing' }
}
$afterDoctor=& $executable status --json | ConvertFrom-Json
if (($afterDoctor.windows | ConvertTo-Json -Compress) -ne ($status.windows | ConvertTo-Json -Compress) -or ($afterDoctor.gameAcceleration | ConvertTo-Json -Compress) -ne ($status.gameAcceleration | ConvertTo-Json -Compress)) { throw 'doctor changed settings' }
$cancel="6`n5`n0`n6`n6`n0`n8`n10`n0`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($cancel -join "`n") -notmatch 'type reset' -or ($cancel -join "`n") -notmatch 'type uninstall') { throw 'cleanup menu cancellation failed' }
foreach ($arguments in @(@('cleanup'),@('cleanup','--confirm','--json'),@('aim','uninstall','--json'))) {
    & $executable @arguments | Out-Null
    if ($LASTEXITCODE -ne 1) { throw 'unsupported destructive command accepted' }
}
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
if (($menu -join "`n") -notmatch '/ desktop' -or ($menu -join "`n") -notmatch 'aim       ') { throw 'home must distinguish desktop settings from aim driver settings' }
$navigation = "1`n0`n6`n3`nhome`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($navigation -join "`n") -notmatch 'game acceleration\?') { throw 'menu navigation failed' }
$invalidInput = "2`nwrong`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($invalidInput -join "`n") -notmatch 'enter a valid number') { throw 'input recovery failed' }
$cancel = "2`n`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or @($cancel | Select-String '1  acceleration').Count -ne 2) { throw 'cancel must redraw home menu' }
$unsupportedJson = & $executable faq --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 1 -or !$unsupportedJson.error) { throw 'unsupported json command must return a json error' }
$interactiveJson = "status --JSON`nfaq --json`nstatus --json --json`nhome`n0" | & $executable
$jsonRecords = @($interactiveJson | ForEach-Object { if ($_ -match '(\{.*\})') { $matches[1] | ConvertFrom-Json } })
if ($LASTEXITCODE -ne 0 -or $jsonRecords.Count -ne 3 -or !$jsonRecords[0].windows -or $jsonRecords[1].error -ne 'json is not supported for this command' -or $jsonRecords[2].error -ne 'use --json once') { throw 'interactive json commands and errors failed' }
if (@($interactiveJson | Select-String '1  acceleration').Count -ne 2) { throw 'json format leaked into the next interactive command' }
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
$events = & $executable aim events --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or !$events.Source -or !$events.PSObject.Properties['AimApplicationCrashes'] -or !$events.PSObject.Properties['Errors']) { throw 'event diagnostic contract failed' }
$badAim = & $executable aim smooth maybe --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 1 -or !$badAim.error) { throw 'aim invalid toggle validation failed' }
if ($aimStatus.State -eq 'ready') {
    $curvePreview = & $executable aim curve preview 0.8 2 24 1.8 1.5 --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $curvePreview.applied -or $curvePreview.response.Readback.Mode -ne 'lut' -or $curvePreview.response.HorizontalSamples.Count -ne 8 -or $curvePreview.curve.base -ne 0.8) { throw 'personal curve preview failed' }
    $afterPreview = & $executable aim status --json | ConvertFrom-Json
    if (($afterPreview | ConvertTo-Json -Compress) -ne ($aimStatus | ConvertTo-Json -Compress)) { throw 'curve preview changed the driver' }
    $builderCancel = "8`n15`n2`n0`n6`n0`n0" | & $executable
    if ($LASTEXITCODE -ne 0 -or ($builderCancel -join "`n") -notmatch 'curve preview / official engine / nothing applied' -or ($builderCancel -join "`n") -match 'curve applied') { throw 'builder zero-start preview/cancel failed' }
    $response = & $executable aim response --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $response.Readback.Profile -ne $aimStatus.Profile -or $response.AfterFlickY.Count -ne 16 -or $response.ProcessedIntervalMs -le 0 -or !$response.Source) { throw 'response simulation contract failed' }
    $expectedCounts=@(1,8,24,40,80,160,400,800)
    if ($response.HorizontalSamples.Count -ne $expectedCounts.Count) { throw 'response speed sweep missing' }
    for ($pointIndex=0;$pointIndex -lt $expectedCounts.Count;$pointIndex++) {
        $point=$response.HorizontalSamples[$pointIndex]
        if ($point.InputCounts -ne $expectedCounts[$pointIndex] -or [Math]::Abs($point.InputCountsPerMs-$point.InputCounts/$response.ProcessedIntervalMs) -gt .000001 -or [double]::IsNaN($point.OutputRatio) -or [double]::IsInfinity($point.OutputRatio)) { throw 'response speed sweep invalid' }
    }
    if ($response.HorizontalSamples[1].OutputRatio -ne $response.SmallMotionRatio -or $response.HorizontalSamples[7].OutputRatio -ne $response.FastMotionRatio) { throw 'response sweep changed legacy ratio contract' }
    $responseMenu = "8`n14`n0" | & $executable
    if ($LASTEXITCODE -ne 0 -or ($responseMenu -join "`n") -notmatch 'current profile / simulation / read only' -or ($responseMenu -join "`n") -notmatch 'horizontal counts/report') { throw 'response menu failed' }
} else {
    $response = & $executable aim response --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 1 -or !$response.error -or $response.applied) { throw 'response requires an inspectable active profile' }
}
if ($aimStatus.State -ne 'ready') {
    $missingDriver = & $executable aim precision on --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 1 -or !$missingDriver.error -or $missingDriver.applied) { throw 'absent backend must not claim applied' }
    $missingResume = & $executable aim resume --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 1 -or !$missingResume.error -or $missingResume.applied) { throw 'resume requires an active driver' }
}
$aimMenu = "8`n0`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($aimMenu -join "`n") -notmatch 'precision on') { throw 'aim menu navigation failed' }
$smoothChoice = "8`n3`n0`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($smoothChoice -join "`n") -notmatch 'light 2 ms' -or ($smoothChoice -join "`n") -match 'applied /') { throw 'smoothing choice or cancellation failed' }
$precisionChoice = "8`n1`n0`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($precisionChoice -join "`n") -notmatch 'steady 1.2x' -or ($precisionChoice -join "`n") -match 'applied /') { throw 'precision choice or cancellation failed' }
if (($aimMenu -join "`n") -notmatch '11  stability on' -or ($aimMenu -join "`n") -notmatch '13  tracking preset') { throw 'aim refinement menus missing' }
if (($aimMenu -join "`n") -notmatch '15  curve builder' -or ($aimMenu -join "`n") -notmatch '16  angle snapping') { throw 'curve and snap menus missing' }
$snapCancel = "8`n16`n0`n0" | & $executable
if ($LASTEXITCODE -ne 0 -or ($snapCancel -join "`n") -notmatch 'optional, not riot certified' -or ($snapCancel -join "`n") -match 'applied /') { throw 'snap cancellation failed' }
foreach ($arguments in @(@('aim','curve','preview','0','3','30','1.4','1'),@('aim','curve','apply','1','30','3','1.4','1'),@('aim','curve','apply','1','3','30','4','1'),@('aim','curve','preview','1','3','30','1.4','0'),@('aim','curve','apply','1','3','30','1.4'),@('aim','curve'),@('aim','snap','on','6'),@('aim','snap','on','-1'),@('aim','snap','off','1'),@('aim','snap','on','NaN'))) {
    $invalid = & $executable @arguments --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 1 -or !$invalid.error -or $invalid.applied) { throw 'invalid curve/snap arguments accepted' }
}
foreach ($badStrength in @(@('aim','smooth','on','0'),@('aim','smooth','on','13'),@('aim','smooth','off','4'),@('aim','precision','on','4'),@('aim','precision','on','1'),@('aim','precision','on','1.9'),@('aim','precision','on','NaN'),@('aim','precision','on','Infinity'),@('aim','precision','off','1.4'),@('aim','stability','on','8'),@('aim','stability','maybe'),@('aim','tracking','off'),@('aim','response','extra'))) {
    $invalidStrength = & $executable @badStrength --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 1 -or !$invalidStrength.error -or $invalidStrength.applied) { throw 'aim command validation failed' }
}
$afterAimValidation = & $executable aim status --json | ConvertFrom-Json
if (($afterAimValidation | ConvertTo-Json -Compress) -ne ($aimStatus | ConvertTo-Json -Compress)) { throw 'invalid or cancelled aim choices changed driver settings' }
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
    if (($menuPreview -join "`n") -notmatch 'already matches / no changes') { throw 'identical profile preview failed' }
    $testSpeed = if ($beforeUndo.windows.Speed -eq 10) { 11 } else { 10 }
    & $executable set speed $testSpeed --json | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'undo test change failed' }
    $changedPreview = & $executable profile show $profileName
    if ($LASTEXITCODE -ne 0 -or ($changedPreview -join "`n") -notmatch ('speed / ' + $testSpeed + ' -> ' + $beforeUndo.windows.Speed + ' / 20')) { throw 'profile preview difference failed' }
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
