$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('helox-build-test-' + [guid]::NewGuid().ToString('n'))
$lock = $null
function Invoke-FixtureBuild {
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $messages = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $fixture 'build.ps1') -Quiet 2>&1 | Out-String
        return @{Code=$LASTEXITCODE;Messages=$messages}
    } finally { $ErrorActionPreference = $previousPreference }
}
try {
    New-Item -ItemType Directory -Path (Join-Path $fixture 'src') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $project 'build.ps1') -Destination (Join-Path $fixture 'build.ps1')
    $source = Join-Path $fixture 'src\Program.cs'
    $valid = 'namespace Helox { class Program { static void Main() {} } class StartupEntry { static void Main() {} } }'
    [IO.File]::WriteAllText($source,$valid)
    $first = Invoke-FixtureBuild
    if ($first.Code -ne 0) { throw ('fixture build failed: ' + $first.Messages) }
    $executable = Join-Path $fixture 'bin\helox.exe'
    $startup = Join-Path $fixture 'bin\helox-startup.exe'
    $originalHash = (Get-FileHash -LiteralPath $executable).Hash
    $startupHash = (Get-FileHash -LiteralPath $startup).Hash
    $startupBytes=[IO.File]::ReadAllBytes($startup)
    $peOffset=[BitConverter]::ToInt32($startupBytes,0x3c)
    if ([BitConverter]::ToUInt16($startupBytes,$peOffset+24+68) -ne 2) { throw 'startup helper must use the hidden Windows GUI subsystem' }
    [IO.File]::WriteAllText($source,'invalid csharp source')
    $failed = Invoke-FixtureBuild
    if ($failed.Code -eq 0 -or (Get-FileHash -LiteralPath $executable).Hash -ne $originalHash -or (Get-FileHash -LiteralPath $startup).Hash -ne $startupHash) { throw 'compiler failure damaged previous executables' }
    [IO.File]::WriteAllText($source,'namespace Helox { class Program { static void Main() {} } }')
    $helperFailed = Invoke-FixtureBuild
    if ($helperFailed.Code -eq 0 -or (Get-FileHash -LiteralPath $executable).Hash -ne $originalHash -or (Get-FileHash -LiteralPath $startup).Hash -ne $startupHash) { throw 'startup compiler failure damaged previous executables' }
    [IO.File]::WriteAllText($source,$valid)
    $lock = [IO.File]::Open($executable,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    $locked = Invoke-FixtureBuild
    if ($locked.Code -eq 0 -or $locked.Messages -notmatch 'close helox and retry' -or (Get-FileHash -LiteralPath $executable).Hash -ne $originalHash -or (Get-FileHash -LiteralPath $startup).Hash -ne $startupHash) { throw 'locked executable recovery failed' }
    $lock.Dispose();$lock=$null
    $again = Invoke-FixtureBuild
    if ($again.Code -ne 0 -or @(Get-ChildItem -LiteralPath (Join-Path $fixture 'bin') -Directory).Count -ne 0) { throw 'build retry or staging cleanup failed' }
    $sha=[Security.Cryptography.SHA256]::Create()
    try { $buildKey=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($fixture).ToLowerInvariant()))).Replace('-','') } finally { $sha.Dispose() }
    $gate=New-Object Threading.Mutex($false,('Local\helox-build-'+$buildKey));$held=$gate.WaitOne(0)
    try {
        if (!$held) { throw 'fixture build gate busy' }
        $busy=Invoke-FixtureBuild
        if ($busy.Code -eq 0 -or $busy.Messages -notmatch 'another build is running') { throw 'concurrent build was not blocked' }
    } finally { if ($held) { $gate.ReleaseMutex() };$gate.Dispose() }
    Write-Host 'passed / two-output compiler preservation, locked executable rollback, retry and concurrent build gate'
} finally {
    if ($null -ne $lock) { $lock.Dispose() }
    $resolved = [IO.Path]::GetFullPath($fixture)
    $allowed = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'invalid test cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
$global:LASTEXITCODE=0
