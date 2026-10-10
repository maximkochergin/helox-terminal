param([switch]$PrepareOnly,[switch]$Uninstall)
$ErrorActionPreference = 'Stop'
# Prefer this host's built-ins when a parent passes PowerShell 7 module paths.
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $env:PSModulePath=(Join-Path $PSHOME 'Modules')+[IO.Path]::PathSeparator+$env:PSModulePath
}
$release = 'https://github.com/RawAccelOfficial/rawaccel/releases/download/v1.7.1/RawAccel_v1.7.1.zip'
$expected = '770FE3AE0919CA3C4D412F58C985EB27F5434DECAD809F7E8206DE4E8852EEC4'
$root = Join-Path $env:LOCALAPPDATA 'helox-terminal\rawaccel-1.7.1'
$archive = Join-Path $root 'official.zip'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$gate = New-Object Threading.Mutex($false, 'Local\helox-aim-installer')
$held = $false
$aimGate=$null;$aimHeld=$false
try {
if ($PrepareOnly -and $Uninstall) { throw 'choose prepare or uninstall' }
try { $held = $gate.WaitOne(0) } catch [Threading.AbandonedMutexException] { $held = $true }
if (!$held) { throw 'another aim setup is running; close it and retry' }
if ($Uninstall) {
    $aimGate=New-Object Threading.Mutex($false,'Local\helox-aim-settings')
    try { $aimHeld=$aimGate.WaitOne(0) } catch [Threading.AbandonedMutexException] { $aimHeld=$true }
    if (!$aimHeld) { throw 'aim settings busy / removal stopped' }
    $class = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e96f-e325-11ce-bfc1-08002be10318}'
    $installedDriver = Join-Path $env:WINDIR 'System32\drivers\rawaccel.sys'
    $service = 'HKLM:\SYSTEM\CurrentControlSet\Services\rawaccel'
    $filters = @((Get-ItemProperty -LiteralPath $class -ErrorAction Stop).UpperFilters)
    $serviceInfo = if (Test-Path -LiteralPath $service) { Get-ItemProperty -LiteralPath $service -ErrorAction Stop } else { $null }
    if ($null -ne $serviceInfo) {
        $image = [Environment]::ExpandEnvironmentVariables([string]$serviceInfo.ImagePath).Trim('"')
        $expectedImage = $installedDriver
        if ($image.StartsWith('\SystemRoot\',[StringComparison]::OrdinalIgnoreCase)) { $image = Join-Path $env:WINDIR $image.Substring(12) }
        if ($image.StartsWith('\??\',[StringComparison]::OrdinalIgnoreCase)) { $image = $image.Substring(4) }
        if ($serviceInfo.Type -ne 1 -or ![string]::Equals($image,$expectedImage,[StringComparison]::OrdinalIgnoreCase)) { throw 'rawaccel service has an unexpected image / removal stopped' }
    }
    if (!(Test-Path -LiteralPath $installedDriver) -and !(Test-Path -LiteralPath ($installedDriver+'.tmp')) -and $filters -notcontains 'rawaccel' -and $null -eq $serviceInfo) {
        Write-Host 'driver already uninstalled / restart if it is still loaded'
        return
    }
    if (!(Test-Path -LiteralPath $installedDriver) -and $filters -notcontains 'rawaccel' -and $null -ne $serviceInfo -and $serviceInfo.DeleteFlag -eq 1) {
        Write-Host 'driver already removed / service deletion pending / restart windows'
        return
    }
}
if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $download = Join-Path $root ([guid]::NewGuid().ToString('n') + '.tmp')
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $release -OutFile $download
        if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $expected) { throw 'official package hash mismatch; retry setup' }
        Move-Item -LiteralPath $download -Destination $archive -Force
    } finally { if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download } }
}
$stage = Join-Path $root ([guid]::NewGuid().ToString('n') + '.stage')
try {
    Expand-Archive -LiteralPath $archive -DestinationPath $stage
    foreach ($file in Get-ChildItem -LiteralPath $stage -File -Recurse) {
        $relative = $file.FullName.Substring($stage.Length + 1)
        $destination = Join-Path $root $relative
        # Loaded assemblies are locked by the open cli. Verified equal files need no replacement.
        if ((Test-Path -LiteralPath $destination) -and (Get-FileHash -LiteralPath $destination).Hash -eq (Get-FileHash -LiteralPath $file.FullName).Hash) { continue }
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        try { Copy-Item -LiteralPath $file.FullName -Destination $destination -Force }
        catch { throw 'backend file could not be replaced; close helox and run install-aim.ps1 again' }
    }
} finally {
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    $allowedRoot = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    if (!$resolvedStage.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'invalid extraction cleanup path' }
    if (Test-Path -LiteralPath $resolvedStage) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
}
$backend = Join-Path $root 'RawAccel'
$driver = Join-Path $backend 'driver\rawaccel.sys'
if ((Get-AuthenticodeSignature -LiteralPath $driver).Status -ne 'Valid') { throw 'driver signature verification failed' }
Write-Host 'verified / official raw accel 1.7.1 / signed driver'
if ($Uninstall) {
    $uninstaller = Join-Path $backend 'uninstaller.exe'
    # Both files are compared with the extracted pinned package above.
    $process = Start-Process -FilePath $uninstaller -WorkingDirectory $backend -Verb RunAs -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw 'official uninstaller failed / data kept' }
    $remaining = @((Get-ItemProperty -LiteralPath $class -ErrorAction Stop).UpperFilters)
    if ($remaining -contains 'rawaccel' -or (Test-Path -LiteralPath $installedDriver)) { throw 'driver removal incomplete / data kept' }
    # The upstream uninstaller does not delete the service. Do this only after
    # verifying filter/file removal; never stop a loaded mouse filter by force.
    if ((Test-Path -LiteralPath $service) -and (Get-ItemProperty -LiteralPath $service -ErrorAction Stop).DeleteFlag -ne 1) {
        # Recheck the service identity after the official process returns.
        $currentInfo=Get-ItemProperty -LiteralPath $service -ErrorAction Stop
        $currentImage=[Environment]::ExpandEnvironmentVariables([string]$currentInfo.ImagePath).Trim('"')
        if ($currentImage.StartsWith('\SystemRoot\',[StringComparison]::OrdinalIgnoreCase)) { $currentImage=Join-Path $env:WINDIR $currentImage.Substring(12) }
        if ($currentImage.StartsWith('\??\',[StringComparison]::OrdinalIgnoreCase)) { $currentImage=$currentImage.Substring(4) }
        if ($currentInfo.Type -ne 1 -or ![string]::Equals($currentImage,$installedDriver,[StringComparison]::OrdinalIgnoreCase)) { throw 'rawaccel service changed / removal stopped' }
        $sc = Join-Path $env:WINDIR 'System32\sc.exe'
        $deletion = Start-Process -FilePath $sc -ArgumentList @('delete','rawaccel') -Verb RunAs -WindowStyle Hidden -PassThru -Wait
        if ($deletion.ExitCode -ne 0) { throw 'service removal failed / data kept' }
    }
    if (Test-Path -LiteralPath $service) {
        $info = Get-ItemProperty -LiteralPath $service -ErrorAction Stop
        if ($info.DeleteFlag -ne 1) { throw 'service still registered / data kept' }
    }
    Write-Host 'driver removed / restart windows to unload it and finish pending file deletion'
    return
}
if ($PrepareOnly) { Write-Host 'prepared / driver not installed'; exit 0 }
$installer = Join-Path $backend 'installer.exe'
# The official installer requires UAC and a final keypress; its window is intentional.
$process = Start-Process -FilePath $installer -WorkingDirectory $backend -Verb RunAs -PassThru -Wait
if ($process.ExitCode -ne 0) { throw 'official installer failed; check runtime requirements and retry' }
$installedDriver = Join-Path $env:WINDIR 'System32\drivers\rawaccel.sys'
$filters = (Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e96f-e325-11ce-bfc1-08002be10318}' -Name UpperFilters).UpperFilters
if (!(Test-Path -LiteralPath $installedDriver) -or !(Get-Service rawaccel -ErrorAction SilentlyContinue) -or $filters -notcontains 'rawaccel') { throw 'installation incomplete' }
if ((Get-FileHash -LiteralPath $installedDriver).Hash -ne (Get-FileHash -LiteralPath $driver).Hash) { throw 'installed driver does not match verified package' }
Write-Host 'installed / restart windows, then 2 tune mouse / choose a feature'
} finally {
    if ($aimHeld) { $aimGate.ReleaseMutex() };if ($null -ne $aimGate) { $aimGate.Dispose() }
    if ($held) { $gate.ReleaseMutex() }; $gate.Dispose()
}
