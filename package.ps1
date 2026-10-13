param([switch]$Quiet)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $env:PSModulePath=(Join-Path $PSHOME 'Modules')+[IO.Path]::PathSeparator+$env:PSModulePath
}
$identity=[Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($PSScriptRoot).ToLowerInvariant())
$sha=[Security.Cryptography.SHA256]::Create()
try { $packageKey=[BitConverter]::ToString($sha.ComputeHash($identity)).Replace('-','') } finally { $sha.Dispose() }
$packageGate=New-Object Threading.Mutex($false,('Local\helox-package-'+$packageKey))
$packageHeld=$false
try {
try { $packageHeld=$packageGate.WaitOne(0) } catch [Threading.AbandonedMutexException] { $packageHeld=$true }
if (!$packageHeld) { throw 'another package is running / wait and retry' }
& (Join-Path $PSScriptRoot 'build.ps1') -Quiet:$Quiet
$versionMatch = [regex]::Match([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\Program.cs')), 'const\s+string\s+Version\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"')
if (!$versionMatch.Success) { throw 'application version missing' }
$version = $versionMatch.Groups[1].Value
$dist = Join-Path $PSScriptRoot 'dist'
if ((Test-Path -LiteralPath $dist) -and ((Get-Item -LiteralPath $dist -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'linked package output / packaging stopped' }
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$work = Join-Path $dist ('package-' + [guid]::NewGuid().ToString('n'))
$stage = Join-Path $work 'helox-terminal'
$archive = Join-Path $dist ('helox-terminal-v' + $version + '.zip')
$checksum = $archive + '.sha256'
$published = @()
$keepWork = $false
try {
    New-Item -ItemType Directory -Force -Path (Join-Path $stage 'bin') | Out-Null
    foreach ($name in @('helox.exe','helox-startup.exe')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('bin\' + $name)) -Destination (Join-Path $stage ('bin\' + $name))
    }
    foreach ($name in @('launch.bat','README.md','LICENSE','build.ps1','package.ps1','verify-package.ps1','install-aim.ps1','maintenance.ps1')) {
        $source=Join-Path $PSScriptRoot $name
        if ((Get-Item -LiteralPath $source -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'linked package source / packaging stopped' }
        Copy-Item -LiteralPath $source -Destination (Join-Path $stage $name)
    }
    foreach ($folder in @('src','tests','docs')) {
        $source=Join-Path $PSScriptRoot $folder
        # Check links without following them before copying the source tree.
        $pending=New-Object 'Collections.Generic.Stack[string]';$pending.Push($source)
        while ($pending.Count -gt 0) {
            $entry=Get-Item -LiteralPath $pending.Pop() -Force
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'linked package source / packaging stopped' }
            if ($entry.PSIsContainer) { foreach ($child in Get-ChildItem -LiteralPath $entry.FullName -Force) { $pending.Push($child.FullName) } }
        }
        Copy-Item -LiteralPath $source -Destination $stage -Recurse -Force
    }
    $compiledVersion=& (Join-Path $stage 'bin\helox.exe') version --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $compiledVersion.version -ne $version) { throw 'compiled executable version differs from package source' }
    $files=@(Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{Path=$_.FullName.Substring($stage.Length+1).Replace('\','/');Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    })
    $manifest=[ordered]@{FormatVersion=1;Version=$version;Files=$files}
    [IO.File]::WriteAllText((Join-Path $stage 'release-manifest.json'),($manifest | ConvertTo-Json -Depth 5),(New-Object Text.UTF8Encoding($false)))
    & (Join-Path $stage 'verify-package.ps1') -Quiet
    $candidate=Join-Path $work 'release.zip'
    Compress-Archive -LiteralPath $stage -DestinationPath $candidate -CompressionLevel Optimal
    $hash=(Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash.ToLowerInvariant()
    $candidateHash=Join-Path $work 'release.sha256'
    [IO.File]::WriteAllText($candidateHash,($hash+'  '+[IO.Path]::GetFileName($archive)+"`r`n"),[Text.Encoding]::ASCII)
    # Keep the previous release and its checksum until both new files exist.
    foreach ($item in @(@{Candidate=$candidate;Destination=$archive},@{Candidate=$candidateHash;Destination=$checksum})) {
        $item.Backup=Join-Path $work ([IO.Path]::GetFileName($item.Destination)+'.previous')
        $item.Existed=Test-Path -LiteralPath $item.Destination
        if ($item.Existed) { [IO.File]::Replace($item.Candidate,$item.Destination,$item.Backup) }
        else { [IO.File]::Move($item.Candidate,$item.Destination) }
        $published+=$item
    }
    if (!$Quiet) { Write-Host ('packaged / '+[IO.Path]::GetFileName($archive)+"`nsha256 / "+$hash) }
    [pscustomobject]@{Version=$version;Archive=$archive;Checksum=$checksum;Sha256=$hash;Files=$files.Count}
} catch {
    try {
        for ($i=$published.Count-1;$i -ge 0;$i--) {
            $item=$published[$i]
            if ($item.Existed) { [IO.File]::Replace($item.Backup,$item.Destination,[NullString]::Value) }
            else { Remove-Item -LiteralPath $item.Destination -Force }
        }
    } catch {
        $keepWork=$true
        throw ('package rollback blocked / previous release files retained in '+$work)
    }
    throw
} finally {
    $resolved=[IO.Path]::GetFullPath($work)
    $allowed=[IO.Path]::GetFullPath($dist).TrimEnd('\')+'\'
    if (!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'invalid package cleanup path' }
    if (!$keepWork -and (Test-Path -LiteralPath $resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
} finally {
    if ($packageHeld) { $packageGate.ReleaseMutex() }
    $packageGate.Dispose()
}
