param([switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
$release = 'https://github.com/RawAccelOfficial/rawaccel/releases/download/v1.7.1/RawAccel_v1.7.1.zip'
$expected = '770FE3AE0919CA3C4D412F58C985EB27F5434DECAD809F7E8206DE4E8852EEC4'
$root = Join-Path $env:LOCALAPPDATA 'helox-terminal\rawaccel-1.7.1'
$archive = Join-Path $root 'official.zip'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$gate = New-Object Threading.Mutex($false, 'Local\helox-aim-installer')
$held = $false
try {
try { $held = $gate.WaitOne(0) } catch [Threading.AbandonedMutexException] { $held = $true }
if (!$held) { throw 'another aim setup is running; close it and retry' }
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
if ($PrepareOnly) { Write-Host 'prepared / driver not installed'; exit 0 }
$installer = Join-Path $backend 'installer.exe'
# The official installer requires UAC and a final keypress; its window is intentional.
$process = Start-Process -FilePath $installer -WorkingDirectory $backend -Verb RunAs -PassThru -Wait
$installedDriver = Join-Path $env:WINDIR 'System32\drivers\rawaccel.sys'
$filters = (Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e96f-e325-11ce-bfc1-08002be10318}' -Name UpperFilters).UpperFilters
if (!(Test-Path -LiteralPath $installedDriver) -or !(Get-Service rawaccel -ErrorAction SilentlyContinue) -or $filters -notcontains 'rawaccel') { throw 'installation incomplete' }
if ((Get-FileHash -LiteralPath $installedDriver).Hash -ne (Get-FileHash -LiteralPath $driver).Hash) { throw 'installed driver does not match verified package' }
Write-Host 'installed / restart windows, then 8 aim tools / choose a feature'
} finally { if ($held) { $gate.ReleaseMutex() }; $gate.Dispose() }
