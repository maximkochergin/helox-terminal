param([ValidateSet('Check','Reset')][string]$Action='Check',[int]$ParentId=0,[switch]$Interactive,[switch]$FunctionsOnly)
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $env:PSModulePath=(Join-Path $PSHOME 'Modules')+[IO.Path]::PathSeparator+$env:PSModulePath
}
$dataRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'helox-terminal'

function Get-DataTree([string]$Target,[string]$Expected) {
    $full=[IO.Path]::GetFullPath($Target).TrimEnd('\')
    $allowed=[IO.Path]::GetFullPath($Expected).TrimEnd('\')
    if (![string]::Equals($full,$allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'invalid cleanup target' }
    if (!(Test-Path -LiteralPath $full)) { return @() }
    # Preflight every entry before deleting anything. Never traverse junctions,
    # symlinks or other reparse points, including the data root itself.
    $pending=New-Object 'Collections.Generic.Stack[string]'
    $pending.Push($full)
    $entries=New-Object 'Collections.Generic.List[string]'
    while ($pending.Count -gt 0) {
        $path=$pending.Pop()
        $item=Get-Item -LiteralPath $path -Force -ErrorAction Stop
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'linked data path / cleanup stopped' }
        $resolved=[IO.Path]::GetFullPath($item.FullName)
        if ($resolved -ne $full -and !$resolved.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'cleanup path escaped data folder' }
        $entries.Add($resolved)
        if ($item.PSIsContainer) { foreach ($child in Get-ChildItem -LiteralPath $resolved -Force -ErrorAction Stop) { $pending.Push($child.FullName) } }
    }
    return $entries.ToArray()
}
function Test-DataDeleteAccess([string]$Target,[string]$Expected) {
    $tree=@(Get-DataTree $Target $Expected)
    if (!('HeloxDeleteProbe' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class HeloxDeleteProbe {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    public static void Check(string path) {
        using(var handle=CreateFile(path,0x10000,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero))
            if(handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(),"data file locked or inaccessible / cleanup stopped");
    }
}
'@
    }
    # A loaded backend or denied file must stop before backups are deleted.
    foreach ($path in $tree) { [HeloxDeleteProbe]::Check($path) }
    return $tree
}
function Remove-DataTree([string]$Target,[string]$Expected) {
    $tree=@(Test-DataDeleteAccess $Target $Expected)
    foreach ($path in $tree | Sort-Object Length -Descending) {
        $item=Get-Item -LiteralPath $path -Force -ErrorAction Stop
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'data path changed / cleanup stopped' }
        if ($item.PSIsContainer) { [IO.Directory]::Delete($path,$false) }
        else { Remove-Item -LiteralPath $path -Force -ErrorAction Stop }
    }
    if (Test-Path -LiteralPath $Target) { throw 'data cleanup incomplete' }
}
function Invoke-ResetPreparation([string]$Target,[string]$Expected,[scriptblock]$Validate,[scriptblock]$RestoreWindows,[scriptblock]$RestoreAim,[scriptblock]$Uninstall) {
    # Finish nonmutating checks before either restoration or driver removal.
    $null=Test-DataDeleteAccess $Target $Expected
    & $Validate
    if (Test-Path -LiteralPath (Join-Path $Target 'original.json')) { & $RestoreWindows }
    else { Write-Host 'no original windows backup / current windows settings left unchanged' }
    if (Test-Path -LiteralPath (Join-Path $Target 'aim-before.json')) { & $RestoreAim }
    & $Uninstall
}
function Get-AimCheck {
    $report=[ordered]@{BackendPrepared=$false;PackageVerified=$false;BackendVerified=$null;ServiceRegistered=$null;ServiceImage=$null;ServiceDeletionPending=$null;FilterRegistered=$null;DriverFilePresent=$false;DriverSignature=$null;DriverMatchesPackage=$null;PendingFilePresent=$false;Errors=@()}
    $backend=Join-Path $dataRoot 'rawaccel-1.7.1\RawAccel'
    $files=@('wrapper.dll','Newtonsoft.Json.dll','uninstaller.exe','driver/rawaccel.sys')
    $report.BackendPrepared=(@($files | Where-Object { !(Test-Path -LiteralPath (Join-Path $backend $_) -PathType Leaf) }).Count -eq 0)
    $driver=Join-Path $env:WINDIR 'System32\drivers\rawaccel.sys'
    $report.DriverFilePresent=Test-Path -LiteralPath $driver -PathType Leaf
    $report.PendingFilePresent=Test-Path -LiteralPath ($driver+'.tmp') -PathType Leaf
    try {
        $service='HKLM:\SYSTEM\CurrentControlSet\Services\rawaccel'
        $report.ServiceRegistered=Test-Path -LiteralPath $service -ErrorAction Stop
        if ($report.ServiceRegistered) { $serviceInfo=Get-ItemProperty -LiteralPath $service -ErrorAction Stop;$report.ServiceImage=$serviceInfo.ImagePath;$report.ServiceDeletionPending=$serviceInfo.DeleteFlag -eq 1 }
        else { $report.ServiceDeletionPending=$false }
        $class='HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e96f-e325-11ce-bfc1-08002be10318}'
        $report.FilterRegistered=@((Get-ItemProperty -LiteralPath $class -ErrorAction Stop).UpperFilters) -contains 'rawaccel'
    } catch { $report.Errors+=('registry: '+$_.Exception.Message) }
    if ($report.DriverFilePresent) {
        try { $report.DriverSignature=[string](Get-AuthenticodeSignature -LiteralPath $driver -ErrorAction Stop).Status }
        catch { $report.Errors+=('signature: '+$_.Exception.Message) }
    }
    $archive=Join-Path $dataRoot 'rawaccel-1.7.1\official.zip'
    if (Test-Path -LiteralPath $archive -PathType Leaf) {
        try {
            $report.PackageVerified=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -eq '770FE3AE0919CA3C4D412F58C985EB27F5434DECAD809F7E8206DE4E8852EEC4'
            if ($report.PackageVerified) {
                Add-Type -AssemblyName System.IO.Compression.FileSystem
                $zip=[IO.Compression.ZipFile]::OpenRead($archive)
                try {
                    $report.BackendVerified=$true
                    foreach ($relative in $files) {
                        $entry=$zip.GetEntry('RawAccel/'+$relative)
                        if ($null -eq $entry) { throw 'missing verified package entry' }
                        $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
                        try { $hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
                        finally { $stream.Dispose();$sha.Dispose() }
                        $local=Join-Path $backend $relative
                        if (!(Test-Path -LiteralPath $local -PathType Leaf) -or (Get-FileHash -LiteralPath $local).Hash -ne $hash) { $report.BackendVerified=$false }
                        if ($relative -eq 'driver/rawaccel.sys' -and $report.DriverFilePresent) { $report.DriverMatchesPackage=(Get-FileHash -LiteralPath $driver).Hash -eq $hash }
                    }
                } finally { $zip.Dispose() }
            }
        } catch { $report.BackendVerified=$null;$report.Errors+=('package: '+$_.Exception.Message) }
    }
    return [pscustomobject]$report
}
if ($FunctionsOnly) { return }
if ($Action -eq 'Check') { Get-AimCheck | ConvertTo-Json -Compress; return }

$result=0
try {
    $maintenanceGate=New-Object Threading.Mutex($false,'Local\helox-maintenance')
    $maintenanceHeld=$false
    try { $maintenanceHeld=$maintenanceGate.WaitOne(0) } catch [Threading.AbandonedMutexException] { $maintenanceHeld=$true }
    if (!$maintenanceHeld) { throw 'another cleanup is running / data kept' }
    if ($ParentId -le 0 -or $ParentId -eq $PID) { throw 'reset requires a closing helox session' }
    $parent=Get-Process -Id $ParentId -ErrorAction SilentlyContinue
    if ($null -ne $parent -and !$parent.WaitForExit(60000)) { throw 'helox did not close / data kept' }
    if (@(Get-Process -Name helox -ErrorAction SilentlyContinue).Count -gt 0) { throw 'close other helox sessions and retry / data kept' }
    $exe=Join-Path $PSScriptRoot 'bin\helox.exe'
    Invoke-ResetPreparation $dataRoot $dataRoot {
        & $exe maintenance-validate
        if ($LASTEXITCODE -ne 0) { throw 'reset backup validation failed / data kept' }
    } {
        # The cleanup gate blocks normal starts. This private worker flag only
        # restores a validated snapshot; it cannot bypass deletion safeguards.
        & $exe maintenance-restore
        if ($LASTEXITCODE -ne 0) { throw 'original windows settings could not be restored / data kept' }
    } {
        & $exe maintenance-aim-restore
        if ($LASTEXITCODE -ne 0) { throw 'original aim snapshot could not be restored / data kept' }
    } {
        & (Join-Path $PSScriptRoot 'install-aim.ps1') -Uninstall
    }
    $gates=@();$held=@()
    try {
        $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        foreach ($name in @('Local\helox-aim-installer','Local\helox-aim-settings',('Local\helox-settings-'+$sid))) {
            $gate=New-Object Threading.Mutex($false,$name);$gates+=$gate
            $acquired=$false
            try { $acquired=$gate.WaitOne(0) } catch [Threading.AbandonedMutexException] { $acquired=$true }
            if (!$acquired) { throw 'another helox operation is running / data kept' }
            $held+=$gate
        }
        Remove-DataTree $dataRoot $dataRoot
    } finally {
        foreach ($gate in $held) { $gate.ReleaseMutex() }
        foreach ($gate in $gates) { $gate.Dispose() }
    }
    Write-Host 'reset complete / profiles, backups, history, presets and downloaded backend removed'
    Write-Host 'restart windows if the driver was installed / extracted application files remain in their folder'
} catch {
    Write-Host ('reset failed / '+$_.Exception.Message)
    $result=1
} finally { if ($maintenanceHeld) { $maintenanceGate.ReleaseMutex() };if ($null -ne $maintenanceGate) { $maintenanceGate.Dispose() } }
if ($Interactive) { $null=Read-Host 'enter to close' }
exit $result
