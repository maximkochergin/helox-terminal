$ErrorActionPreference='Stop'
Import-Module Microsoft.PowerShell.Management,Microsoft.PowerShell.Utility,Microsoft.PowerShell.Archive -ErrorAction Stop
$project=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('helox-uninstall-test-'+[guid]::NewGuid().ToString('n'))
$previousLocal=$env:LOCALAPPDATA;$previousWindows=$env:WINDIR
$junction=$null;$cacheLock=$null
# Mock all registry reads and elevated process launches. System paths resolve
# inside this temporary fixture; no real driver, service or filter is modified.
function Test-Path {
    param([string]$LiteralPath,[string]$Path,[string]$PathType)
    if (!$LiteralPath) { $LiteralPath=$Path }
    if ($LiteralPath -eq 'HKLM:\SYSTEM\CurrentControlSet\Services\rawaccel') { return $global:fixtureUninstallservicePresent }
    if ($LiteralPath.StartsWith('HKLM:')) { throw 'unexpected registry access in fixture' }
    if ($PathType) { return Microsoft.PowerShell.Management\Test-Path -LiteralPath $LiteralPath -PathType $PathType }
    return Microsoft.PowerShell.Management\Test-Path -LiteralPath $LiteralPath
}
function Get-ItemProperty {
    param([string]$LiteralPath)
    if ($LiteralPath -eq 'HKLM:\SYSTEM\CurrentControlSet\Services\rawaccel') {
        return [pscustomobject]@{Type=1;ImagePath=$global:fixtureUninstallimage;DeleteFlag=$(if($global:fixtureUninstallmode -eq 'pending') {1} else {0})}
    }
    if ($LiteralPath -eq 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e96f-e325-11ce-bfc1-08002be10318}') {
        return [pscustomobject]@{UpperFilters=$(if ($global:fixtureUninstallfilterPresent) { @('mouclass','rawaccel') } else { @('mouclass') })}
    }
    throw 'unexpected registry read in fixture'
}
function Get-FileHash {
    param([string]$LiteralPath,[string]$Algorithm)
    if ($global:fixtureCacheChanged -and $LiteralPath.EndsWith('\RawAccel\uninstaller.exe') -and $LiteralPath -notmatch '\.stage\\') { return [pscustomobject]@{Hash='changed-cache'} }
    return [pscustomobject]@{Hash='770FE3AE0919CA3C4D412F58C985EB27F5434DECAD809F7E8206DE4E8852EEC4'}
}
function Get-AuthenticodeSignature { param([string]$LiteralPath);return [pscustomobject]@{Status='Valid'} }
function Invoke-WebRequest { throw 'fixture attempted a network request' }
function Expand-Archive {
    param([string]$LiteralPath,[string]$DestinationPath)
    New-Item -ItemType Directory -Path (Join-Path $DestinationPath 'RawAccel\driver') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $DestinationPath 'RawAccel\driver\rawaccel.sys'),'fixture')
    [IO.File]::WriteAllText((Join-Path $DestinationPath 'RawAccel\uninstaller.exe'),'fixture')
}
function Start-Process {
    param([string]$FilePath,[string]$WorkingDirectory,[string]$Verb,[string[]]$ArgumentList,[string]$WindowStyle,[switch]$PassThru,[switch]$Wait)
    if (!$FilePath.StartsWith($fixture+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'fixture attempted a real process launch' }
    $global:fixtureUninstalllaunches++
    if ($FilePath.EndsWith('\uninstaller.exe')) {
        if ($global:fixtureUninstallmode -ne 'filter-remains') {
            $global:fixtureUninstallfilterPresent=$false
            Remove-Item -LiteralPath $global:fixtureUninstalldriver -Force
        }
        if ($global:fixtureUninstallmode -eq 'service-changed') { $global:fixtureUninstallimage='C:\unrelated.sys' }
        return [pscustomobject]@{ExitCode=0}
    }
    if ($FilePath.EndsWith('\sc.exe') -and ($ArgumentList -join ' ') -eq 'delete rawaccel') {
        if ($global:fixtureUninstallmode -eq 'service-fails') { return [pscustomobject]@{ExitCode=5} }
        $global:fixtureUninstallservicePresent=$false
        return [pscustomobject]@{ExitCode=0}
    }
    throw 'unexpected fixture process'
}
try {
    $env:LOCALAPPDATA=Join-Path $fixture 'local'
    $env:WINDIR=Join-Path $fixture 'windows'
    $global:fixtureUninstalldriver=Join-Path $env:WINDIR 'System32\drivers\rawaccel.sys'
    New-Item -ItemType Directory -Path (Split-Path $global:fixtureUninstalldriver -Parent) -Force | Out-Null
    $root=Join-Path $env:LOCALAPPDATA 'helox-terminal'
    New-Item -ItemType Directory -Path (Join-Path $root 'rawaccel-1.7.1') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $root 'original.json'),'keep backup')
    [IO.File]::WriteAllText((Join-Path $root 'rawaccel-1.7.1\official.zip'),'fixture')
    foreach ($global:fixtureUninstallmode in @('unexpected-image','filter-remains','service-fails','service-changed','success','absent','pending')) {
        $global:fixtureUninstallservicePresent=$global:fixtureUninstallmode -ne 'absent';$global:fixtureUninstallfilterPresent=$global:fixtureUninstallmode -notin @('absent','pending');$global:fixtureUninstalllaunches=0
        $global:fixtureUninstallimage=if ($global:fixtureUninstallmode -eq 'unexpected-image') { 'C:\unrelated.sys' } else { '\SystemRoot\System32\drivers\rawaccel.sys' }
        if ($global:fixtureUninstallmode -notin @('absent','pending')) { [IO.File]::WriteAllText($global:fixtureUninstalldriver,'fixture') }
        $failed=$false
        try { & (Join-Path $project 'install-aim.ps1') -Uninstall } catch { $failed=$true }
        if ($failed -ne ($global:fixtureUninstallmode -notin @('success','absent','pending'))) { throw ('unexpected uninstall result: '+$global:fixtureUninstallmode) }
        if ($global:fixtureUninstallmode -eq 'unexpected-image' -and $global:fixtureUninstalllaunches -ne 0) { throw 'unexpected service was modified' }
        if ($global:fixtureUninstallmode -eq 'service-changed' -and $global:fixtureUninstalllaunches -ne 1) { throw 'changed service was deleted' }
        if ($global:fixtureUninstallmode -eq 'absent' -and $global:fixtureUninstalllaunches -ne 0) { throw 'absent driver launched uninstaller' }
        if ($global:fixtureUninstallmode -eq 'pending' -and $global:fixtureUninstalllaunches -ne 0) { throw 'pending deletion retried service removal' }
        if ([IO.File]::ReadAllText((Join-Path $root 'original.json')) -ne 'keep backup') { throw 'uninstall removed user backup' }
    }
    $global:fixtureUninstalllaunches=0
    $cache=Join-Path $root 'rawaccel-1.7.1\RawAccel'
    [IO.File]::WriteAllText((Join-Path $cache 'settings.json'),'keep upstream settings')
    & (Join-Path $project 'install-aim.ps1') -PrepareOnly
    if ($global:fixtureUninstalllaunches -ne 0 -or [IO.File]::ReadAllText((Join-Path $cache 'settings.json')) -ne 'keep upstream settings') { throw 'preparation launched installer or changed user settings' }
    $cacheLock=[IO.File]::Open((Join-Path $cache 'uninstaller.exe'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    & (Join-Path $project 'install-aim.ps1') -PrepareOnly
    $global:fixtureCacheChanged=$true
    $blocked=$false
    try { & (Join-Path $project 'install-aim.ps1') -PrepareOnly } catch { $blocked=$_.Exception.Message -match 'backend file could not be replaced' }
    if (!$blocked) { throw 'locked changed cache was not rejected' }
    $global:fixtureCacheChanged=$false;$cacheLock.Dispose();$cacheLock=$null
    $outside=Join-Path $fixture 'outside';New-Item -ItemType Directory -Path $outside | Out-Null
    [IO.File]::WriteAllText((Join-Path $outside 'rawaccel.sys'),'outside keep')
    Remove-Item -LiteralPath (Join-Path $cache 'driver\rawaccel.sys') -Force
    [IO.Directory]::Delete((Join-Path $cache 'driver'),$false)
    $junction=Join-Path $cache 'driver'
    New-Item -ItemType Junction -Path $junction -Target $outside | Out-Null
    $blocked=$false
    try { & (Join-Path $project 'install-aim.ps1') -PrepareOnly } catch { $blocked=$_.Exception.Message -match 'linked backend path' }
    if (!$blocked -or [IO.File]::ReadAllText((Join-Path $outside 'rawaccel.sys')) -ne 'outside keep') { throw 'linked cache repair escaped the fixture' }
    [IO.Directory]::Delete($junction,$false);$junction=$null
    Write-Host 'passed / mocked uninstall identity, filter readback, removal failure, preparation, settings preservation, locked cache and linked-path rejection'
} finally {
    if ($null -ne $cacheLock) { $cacheLock.Dispose() }
    $global:fixtureCacheChanged=$false
    if ($null -ne $junction -and (Microsoft.PowerShell.Management\Test-Path -LiteralPath $junction)) { [IO.Directory]::Delete($junction,$false) }
    $env:LOCALAPPDATA=$previousLocal;$env:WINDIR=$previousWindows
    $resolved=[IO.Path]::GetFullPath($fixture);$allowed=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'invalid fixture cleanup path' }
    if (Microsoft.PowerShell.Management\Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
$global:LASTEXITCODE=0
