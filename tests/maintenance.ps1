$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
. (Join-Path $project 'maintenance.ps1') -FunctionsOnly
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('helox-cleanup-test-'+[guid]::NewGuid().ToString('n'))
$root=Join-Path $fixture 'data'
$outside=Join-Path $fixture 'outside'
$lock=$null;$junction=$null
function Assert-Rejected([scriptblock]$Work,[string]$Name) {
    $rejected=$false
    try { & $Work } catch { $rejected=$true }
    if (!$rejected) { throw ('expected rejection: '+$Name) }
}
try {
    New-Item -ItemType Directory -Path (Join-Path $root 'backend') -Force | Out-Null
    New-Item -ItemType Directory -Path $outside -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $outside 'keep.txt'),'outside')
    [IO.File]::WriteAllText((Join-Path $root 'original.json'),'backup')
    $dll=Join-Path $root 'backend\wrapper.dll'
    [IO.File]::WriteAllText($dll,'locked fixture')
    Assert-Rejected { Remove-DataTree $outside $root } 'unexpected cleanup root'
    if (!(Test-Path -LiteralPath (Join-Path $outside 'keep.txt'))) { throw 'outside file removed' }
    $lock=[IO.File]::Open($dll,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    Assert-Rejected { Remove-DataTree $root $root } 'loaded backend'
    $script:resetCalls=0
    Assert-Rejected { Invoke-ResetPreparation $root $root {$script:resetCalls++} {$script:resetCalls++} {$script:resetCalls++} {$script:resetCalls++} } 'locked reset preflight'
    if ($script:resetCalls -ne 0) { throw 'locked reset mutated settings or uninstalled driver' }
    if ([IO.File]::ReadAllText((Join-Path $root 'original.json')) -ne 'backup') { throw 'backup removed before lock rejection' }
    $lock.Dispose();$lock=$null
    $script:resetCalls=0
    Assert-Rejected { Invoke-ResetPreparation $root $root {throw 'invalid aim backup'} {$script:resetCalls++} {$script:resetCalls++} {$script:resetCalls++} } 'invalid reset backup'
    if ($script:resetCalls -ne 0) { throw 'invalid backup allowed reset side effects' }
    $script:resetOrder=New-Object 'Collections.Generic.List[string]'
    [IO.File]::WriteAllText((Join-Path $root 'aim-before.json'),'fixture')
    Invoke-ResetPreparation $root $root {$script:resetOrder.Add('validate')} {$script:resetOrder.Add('windows')} {$script:resetOrder.Add('aim')} {$script:resetOrder.Add('uninstall')}
    if (($script:resetOrder -join ',') -ne 'validate,windows,aim,uninstall') { throw 'reset operation order incorrect' }
    $junction=Join-Path $root 'linked'
    New-Item -ItemType Junction -Path $junction -Target $outside | Out-Null
    Assert-Rejected { Remove-DataTree $root $root } 'linked subtree'
    if ([IO.File]::ReadAllText((Join-Path $outside 'keep.txt')) -ne 'outside') { throw 'junction target changed' }
    [IO.Directory]::Delete($junction,$false);$junction=$null
    (Get-Item -LiteralPath (Join-Path $root 'original.json')).IsReadOnly=$true
    Remove-DataTree $root $root
    if (Test-Path -LiteralPath $root) { throw 'fixture data not removed' }
    Remove-DataTree $root $root
    $exe=Join-Path $project 'bin\helox.exe'
    $gate=New-Object Threading.Mutex($false,'Local\helox-maintenance')
    $held=$gate.WaitOne(0)
    try {
        if (!$held) { throw 'maintenance gate unexpectedly busy' }
        $previousPreference=$ErrorActionPreference
        try { $ErrorActionPreference='Continue';$message=& $exe check 2>&1 | Out-String }
        finally { $ErrorActionPreference=$previousPreference }
        if ($LASTEXITCODE -ne 1 -or $message -notmatch 'cleanup running') { throw 'new session bypassed cleanup gate' }
        $blocked=& $exe status --json | ConvertFrom-Json
        if ($LASTEXITCODE -ne 1 -or $blocked.error -notmatch 'cleanup running') { throw 'blocked json session contract failed' }
    } finally { if ($held) { $gate.ReleaseMutex() };$gate.Dispose() }
    # A diagnostic reporting a hash failure must not load the real backend,
    # even if this machine already has a readable installed driver.
    $fakeApp=Join-Path $fixture 'app'
    New-Item -ItemType Directory -Path (Join-Path $fakeApp 'bin') -Force | Out-Null
    $fakeExe=Join-Path $fakeApp 'bin\helox.exe'
    Copy-Item -LiteralPath $exe -Destination $fakeExe
    [IO.File]::WriteAllText((Join-Path $fakeApp 'maintenance.ps1'),'Write-Output ''{"BackendPrepared":true,"PackageVerified":true,"BackendVerified":false,"Errors":[]}''')
    $unverified=& $fakeExe aim doctor --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $null -ne $unverified.KernelReadable -or $null -ne $unverified.KernelVersion) { throw 'unverified diagnostic loaded native bridge' }
    $health=& $fakeExe health --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $health.Aim.State -eq 'ready' -or $null -ne $health.Aim.Profile -or $null -ne $health.GameInputVerified) { throw 'health bypassed backend verification or claimed game testing' }
    $selection=& $exe status --json | ConvertFrom-Json
    if ($null -ne $selection.receiver) {
        $blockedAim=& $fakeExe aim status --json | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or $blockedAim.State -eq 'ready' -or $null -ne $blockedAim.Profile) { throw 'aim menu path bypassed backend verification' }
        $backendRoot=Join-Path (Join-Path $env:LOCALAPPDATA 'helox-terminal') 'rawaccel-1.7.1\RawAccel'
        if ((Test-Path -LiteralPath (Join-Path $backendRoot 'wrapper.dll')) -and (Test-Path -LiteralPath (Join-Path $backendRoot 'Newtonsoft.Json.dll'))) {
            if ($blockedAim.State -ne 'unavailable' -or $blockedAim.Note -notmatch 'unverified') { throw 'present backend did not enforce hash verification' }
        } elseif ($blockedAim.Note -notmatch 'install backend|backend incomplete') { throw 'absent backend was not reported correctly' }
    }
    Write-Host 'passed / cleanup boundaries, locked-file preservation, junction rejection, readonly files and session gate'
} finally {
    if ($null -ne $lock) { $lock.Dispose() }
    if ($null -ne $junction -and (Test-Path -LiteralPath $junction)) { [IO.Directory]::Delete($junction,$false) }
    $resolved=[IO.Path]::GetFullPath($fixture)
    $allowed=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'invalid fixture cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-DataTree $resolved $resolved }
}
# The session-gate assertion deliberately launches a command that exits 1.
# Do not propagate that expected exit code to a successful CI step/caller.
$global:LASTEXITCODE=0
