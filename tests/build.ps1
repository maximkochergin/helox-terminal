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
    $valid = 'class Fixture { static void Main() {} }'
    [IO.File]::WriteAllText($source,$valid)
    $first = Invoke-FixtureBuild
    if ($first.Code -ne 0) { throw ('fixture build failed: ' + $first.Messages) }
    $executable = Join-Path $fixture 'bin\helox.exe'
    $originalHash = (Get-FileHash -LiteralPath $executable).Hash
    [IO.File]::WriteAllText($source,'invalid csharp source')
    $failed = Invoke-FixtureBuild
    if ($failed.Code -eq 0 -or (Get-FileHash -LiteralPath $executable).Hash -ne $originalHash) { throw 'compiler failure damaged previous executable' }
    [IO.File]::WriteAllText($source,$valid)
    $lock = [IO.File]::Open($executable,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    $locked = Invoke-FixtureBuild
    if ($locked.Code -eq 0 -or $locked.Messages -notmatch 'close helox and retry' -or (Get-FileHash -LiteralPath $executable).Hash -ne $originalHash) { throw 'locked executable recovery failed' }
    $lock.Dispose();$lock=$null
    $again = Invoke-FixtureBuild
    if ($again.Code -ne 0 -or @(Get-ChildItem -LiteralPath (Join-Path $fixture 'bin') -Directory).Count -ne 0) { throw 'build retry or staging cleanup failed' }
    Write-Host 'passed / build failure preservation, locked executable and retry'
} finally {
    if ($null -ne $lock) { $lock.Dispose() }
    $resolved = [IO.Path]::GetFullPath($fixture)
    $allowed = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'invalid test cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
