param([switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
$release = 'https://github.com/RawAccelOfficial/rawaccel/releases/download/v1.7.1/RawAccel_v1.7.1.zip'
$expected = '770FE3AE0919CA3C4D412F58C985EB27F5434DECAD809F7E8206DE4E8852EEC4'
$root = Join-Path $env:LOCALAPPDATA 'helox-terminal\rawaccel-1.7.1'
$archive = Join-Path $root 'official.zip'
New-Item -ItemType Directory -Force -Path $root | Out-Null
if (!(Test-Path -LiteralPath $archive)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $release -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) { throw 'official package hash mismatch; remove official.zip and retry' }
Expand-Archive -LiteralPath $archive -DestinationPath $root -Force
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
