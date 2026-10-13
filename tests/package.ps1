$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('helox-package-test-'+[guid]::NewGuid().ToString('n'))
$checksumLock=$null;$junction=$null
function Assert-PackageRejected([scriptblock]$Work,[string]$Name) {
    $rejected=$false;try { & $Work | Out-Null } catch { $rejected=$true }
    if (!$rejected) { throw ('expected package rejection / '+$Name) }
}
try {
    foreach ($folder in @('src','tests','docs')) { New-Item -ItemType Directory -Path (Join-Path $fixture $folder) -Force | Out-Null }
    foreach ($name in @('launch.bat','build.ps1','package.ps1','verify-package.ps1','install-aim.ps1','maintenance.ps1','LICENSE')) { Copy-Item -LiteralPath (Join-Path $project $name) -Destination (Join-Path $fixture $name) }
    [IO.File]::WriteAllText((Join-Path $fixture 'README.md'),'fixture documentation')
    [IO.File]::WriteAllText((Join-Path $fixture 'docs\manual.md'),'fixture manual')
    [IO.File]::WriteAllText((Join-Path $fixture 'tests\fixture.txt'),'fixture checks')
    [IO.File]::WriteAllText((Join-Path $fixture 'src\Program.cs'),'namespace Helox { class Program { internal const string Version="0.20.0"; static void Main() { System.Console.WriteLine("{\"version\":\"0.20.0\"}"); } } class StartupEntry { static void Main() {} } }')
    $package=& (Join-Path $fixture 'package.ps1') -Quiet
    if ($package.Version -ne '0.20.0' -or (Get-FileHash -LiteralPath $package.Archive -Algorithm SHA256).Hash -ne $package.Sha256 -or [IO.File]::ReadAllText($package.Checksum).Trim() -ne ($package.Sha256+'  helox-terminal-v0.20.0.zip')) { throw 'package identity or archive checksum failed' }
    $unpacked=Join-Path $fixture 'unpacked'
    Expand-Archive -LiteralPath $package.Archive -DestinationPath $unpacked
    $release=Join-Path $unpacked 'helox-terminal'
    $verify=Join-Path $release 'verify-package.ps1'
    & $verify -Quiet
    foreach ($name in @('bin\helox.exe','bin\helox-startup.exe','package.ps1','release-manifest.json','src\Program.cs','docs\manual.md','tests\fixture.txt')) { if (!(Test-Path -LiteralPath (Join-Path $release $name) -PathType Leaf)) { throw ('release content missing / '+$name) } }
    $readme=Join-Path $release 'README.md';$original=[IO.File]::ReadAllBytes($readme)
    [IO.File]::WriteAllText($readme,'changed fixture documentation')
    Assert-PackageRejected { & $verify -Quiet } 'changed file'
    Remove-Item -LiteralPath $readme -Force
    Assert-PackageRejected { & $verify -Quiet } 'missing file'
    [IO.File]::WriteAllBytes($readme,$original)
    $extra=Join-Path $release 'unexpected.txt';[IO.File]::WriteAllText($extra,'extra')
    Assert-PackageRejected { & $verify -Quiet } 'unexpected file'
    Remove-Item -LiteralPath $extra -Force
    $manifestPath=Join-Path $release 'release-manifest.json';$manifestBytes=[IO.File]::ReadAllBytes($manifestPath)
    $manifest=[IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
    $manifest.Files[0].Path='../outside.txt'
    [IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 5))
    Assert-PackageRejected { & $verify -Quiet } 'manifest traversal'
    [IO.File]::WriteAllBytes($manifestPath,$manifestBytes)
    $outside=Join-Path $fixture 'outside';New-Item -ItemType Directory -Path $outside | Out-Null
    [IO.File]::WriteAllText((Join-Path $outside 'keep.txt'),'outside')
    $junction=Join-Path $release 'linked';New-Item -ItemType Junction -Path $junction -Target $outside | Out-Null
    Assert-PackageRejected { & $verify -Quiet } 'linked release folder'
    [IO.Directory]::Delete($junction,$false);$junction=$null
    & $verify -Quiet
    $archiveHash=(Get-FileHash -LiteralPath $package.Archive).Hash
    $checksumHash=(Get-FileHash -LiteralPath $package.Checksum).Hash
    $checksumLock=[IO.File]::Open($package.Checksum,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    Assert-PackageRejected { & (Join-Path $fixture 'package.ps1') -Quiet } 'locked checksum publication'
    if ((Get-FileHash -LiteralPath $package.Archive).Hash -ne $archiveHash -or (Get-FileHash -LiteralPath $package.Checksum).Hash -ne $checksumHash) { throw 'failed publication damaged previous package or checksum' }
    $checksumLock.Dispose();$checksumLock=$null
    if (@(Get-ChildItem -LiteralPath (Join-Path $fixture 'dist') -Directory).Count -ne 0) { throw 'package staging directory leaked' }
    $sha=[Security.Cryptography.SHA256]::Create()
    try { $packageKey=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($fixture).ToLowerInvariant()))).Replace('-','') } finally { $sha.Dispose() }
    $gate=New-Object Threading.Mutex($false,('Local\helox-package-'+$packageKey));$held=$gate.WaitOne(0)
    try {
        if (!$held) { throw 'fixture package gate busy' }
        $beforeBinary=(Get-FileHash -LiteralPath (Join-Path $fixture 'bin\helox.exe')).Hash
        $previousPreference=$ErrorActionPreference
        try { $ErrorActionPreference='Continue';$message=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $fixture 'package.ps1') -Quiet 2>&1 | Out-String;$code=$LASTEXITCODE }
        finally { $ErrorActionPreference=$previousPreference }
        if ($code -eq 0 -or $message -notmatch 'another package is running' -or (Get-FileHash -LiteralPath (Join-Path $fixture 'bin\helox.exe')).Hash -ne $beforeBinary -or (Get-FileHash -LiteralPath $package.Archive).Hash -ne $archiveHash) { throw 'concurrent package was not blocked before build/publication' }
    } finally { if ($held) { $gate.ReleaseMutex() };$gate.Dispose() }
    Write-Host 'passed / complete archive, SHA256, extracted manifest, changed/missing/extra/linked files, traversal, publication rollback, staging cleanup and concurrent package gate'
} finally {
    if ($null -ne $checksumLock) { $checksumLock.Dispose() }
    if ($null -ne $junction -and (Test-Path -LiteralPath $junction)) { [IO.Directory]::Delete($junction,$false) }
    $resolved=[IO.Path]::GetFullPath($fixture);$allowed=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'invalid fixture cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
$global:LASTEXITCODE=0
