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
    if ([IO.File]::ReadAllText((Join-Path $root 'original.json')) -ne 'backup') { throw 'backup removed before lock rejection' }
    $lock.Dispose();$lock=$null
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
    } finally { if ($held) { $gate.ReleaseMutex() };$gate.Dispose() }
    Write-Host 'passed / cleanup boundaries, locked-file preservation, junction rejection, readonly files and session gate'
} finally {
    if ($null -ne $lock) { $lock.Dispose() }
    if ($null -ne $junction -and (Test-Path -LiteralPath $junction)) { [IO.Directory]::Delete($junction,$false) }
    $resolved=[IO.Path]::GetFullPath($fixture)
    $allowed=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'invalid fixture cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-DataTree $resolved $resolved }
}
